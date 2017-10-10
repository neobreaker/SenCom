using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.IO.Ports;
using System.Timers;
using System.Windows.Threading;
using SenCom.Common;
using SenCom.Model;

namespace SenCom
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort m_com = new SerialPort();
        private Queue<byte[]> m_com_queue = new Queue<byte[]>();
        private int m_total_rcv = 0;
        private Timer m_com_timer = null;
        private delegate void TimerDispatcherDelegate();
        private HoldingReg m_holding_reg = null;

        public MainWindow()
        {
            InitializeComponent();

            SerialPortInit();
            UIInit();
        }

        private void SerialPortInit()
        {

            m_com.PortName = "COM1";
            m_com.BaudRate = 4800;
            m_com.Parity = Parity.None;
            m_com.DataBits = 8;
            m_com.StopBits = StopBits.One;

            m_com.ReadBufferSize = 1024;
            m_com.WriteBufferSize = 1024;

            m_com.DataReceived += new SerialDataReceivedEventHandler(SerialPortReceive);

            m_com_timer = new Timer();
            m_com_timer.Elapsed += new ElapsedEventHandler(ComTimedEvent);
            m_com_timer.Enabled = false;
            m_com_timer.AutoReset = false;
        }

        private void UIInit()
        {
            this.tbkComRevNum.Text = m_total_rcv.ToString();
        }

        private void SerialPortReceive(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] rcv_buffer;

            rcv_buffer = new byte[m_com.BytesToRead];
            m_com.Read(rcv_buffer, 0, rcv_buffer.Length);

            m_com_queue.Enqueue(rcv_buffer);

            m_com_timer.Stop();
            m_com_timer.Interval = 1000;
            m_com_timer.Start();
            
        }

        private void ComTimedEvent(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                new TimerDispatcherDelegate(updateUI));
        }

        private void updateUI()
        {
            int i = 0, rcv_len = 0;
            string rcv_str = "";
            byte[] rcv_buffer = null;
            byte[] frame = new byte[1024];
 
            while (m_com_queue.Count > 0)
            {
                rcv_buffer = m_com_queue.Dequeue();

                if (rcv_len == 0)
                {
                    Array.Copy(rcv_buffer, 3, frame, 0, rcv_buffer.Length - 3);
                }
                else
                {
                    Array.Copy(rcv_buffer, 0, frame, rcv_len-3, rcv_buffer.Length);
                }

                Util.HexToString(rcv_buffer, rcv_buffer.Length, ref rcv_str);
                this.tbComData.Text += rcv_str;

                rcv_len += rcv_buffer.Length;
                m_total_rcv += rcv_buffer.Length;
            }
            this.tbkComRevNum.Text = m_total_rcv.ToString();

            m_holding_reg = new HoldingReg(frame, rcv_len - 5);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            byte[] snd_buffer = { 0x03, 0x03, 0x00, 0x00, 0x00, 0x33, 0x04, 0x3d };
            try
            {
                if(!m_com.IsOpen)
                    m_com.Open();
                m_com.Write(snd_buffer, 0, snd_buffer.Length);
            }
            catch(Exception ex)
            {
                MessageBox.Show("无法打开串口,请检测此串口是否有效或被其他占用！");  
            }
        }
    }
}
