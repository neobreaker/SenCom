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
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using System.Runtime.InteropServices;

namespace SenCom
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort m_com = new SerialPort();                    
        private Queue<byte[]> m_com_queue = new Queue<byte[]>();        //串口 不完整数据帧 缓存队列
        private int m_total_rcv = 0;                                // 接收数据总长度
        private Timer m_tim_com = null;                        //串口帧结束检测定时器
        private delegate void TimerDispatcherDelegate();        //串口帧结束检测
        private HoldingReg m_holding_reg = null;        
        private int m_display_idx = 0;               //display chart x
        private int m_detect_idx = 0;               // detect chart x
        private bool m_is_started = false;          //自动发送串口命令是否启动
        private UInt64 m_total_snd_cnt = 0;
        private UInt64 m_total_rcv_cnt = 0;
        private ObservableDataSource<Point> m_chart_display = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> m_chart_detect = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> m_chart_cfg = new ObservableDataSource<Point>();

        private DispatcherTimer m_tim_autosend = null;        //自动发送串口命令定时器

        private string[] m_com_tbl =  { "COM1", "COM2", "COM3", "COM4" };
        private string[] m_baud_tbl = { "4800", "9600", "38400", "115200" };
        private string[] m_addr_tbl = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16" };

        private byte[] m_auto_snd = { 0x03, 0x03, 0x00, 0x00, 0x00, 0x33, 0x04, 0x3d };
        private Queue<byte[]> m_snd_queue = new Queue<byte[]>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UIInit();
            SerialPortInit();
            AutoSendTimerInit();
        }

        private void SerialPortInit()
        {

            m_com.PortName = this.cbCom.SelectedValue.ToString();
            m_com.BaudRate = Int32.Parse(this.cbBaud.SelectedValue.ToString());
            m_com.Parity = Parity.None;
            m_com.DataBits = 8;
            m_com.StopBits = StopBits.One;

            m_com.ReadBufferSize = 1024;
            m_com.WriteBufferSize = 1024;

            m_com.DataReceived += new SerialDataReceivedEventHandler(SerialPortReceive);

            m_tim_com = new Timer();
            m_tim_com.Elapsed += new ElapsedEventHandler(ComTimedEvent);
            m_tim_com.Enabled = false;
            m_tim_com.AutoReset = false;
        }

        private void UIInit()
        {
            string str = "";
            this.cbCom.ItemsSource = m_com_tbl;
            this.cbBaud.ItemsSource = m_baud_tbl;
            this.cbAddr.ItemsSource = m_addr_tbl;
            this.cbCom.SelectedIndex = 0;
            this.cbBaud.SelectedIndex = 0;
            this.cbAddr.SelectedIndex = 0;

            Util.HexToString(m_auto_snd, m_auto_snd.Length, ref str);
            this.tbComCmd.Text = str;
            this.tbkComRevNum.Text = m_total_rcv.ToString();

            this.cpSensorDisplay.AddLineGraph(m_chart_display, Colors.Green);
            this.cpSensorDetect.AddLineGraph(m_chart_display, Colors.Green);
            this.cpSensorCfg.AddLineGraph(m_chart_cfg, Colors.Green);
        }

        private void AutoSendTimerInit()
        {
            m_tim_autosend = new DispatcherTimer();
            m_tim_autosend.Interval = TimeSpan.FromSeconds(1);
            m_tim_autosend.Tick += new EventHandler(AutoSend);
        }

        private void AutoSend(object sender, EventArgs e)
        {
            byte[] bytes_to_snd;

            try
            {
                lock (m_snd_queue)
                {
                    if (m_snd_queue.Count > 0)
                    {
                        bytes_to_snd = m_snd_queue.Dequeue();
                        m_com.Write(bytes_to_snd, 0, bytes_to_snd.Length);

                        if (m_is_started && bytes_to_snd[1] == Const.FUNC_READHOLDINGREG)
                        {
                            m_snd_queue.Enqueue(m_auto_snd);
                            m_total_snd_cnt++;
                            m_tim_autosend.Interval = TimeSpan.FromSeconds(1);
                            m_tim_autosend.Start();
                        }
                        this.tbkComSndCnt.Text = m_total_snd_cnt.ToString();
                    }
                }
                m_tim_com.Stop();
                m_tim_com.Interval = 300;     // 300ms receive time out
                m_tim_com.Start();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
            }
        }   

        private void SerialPortReceive(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] rcv_buffer;

            rcv_buffer = new byte[m_com.BytesToRead];
            m_com.Read(rcv_buffer, 0, rcv_buffer.Length);

            m_com_queue.Enqueue(rcv_buffer);

            m_tim_com.Stop();
            m_tim_com.Interval = 100;     //min 40
            m_tim_com.Start();
            
        }

        private void ComTimedEvent(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                new TimerDispatcherDelegate(UpdateUI));
        }

        private void UpdateChartDisplay(HoldingReg hr)
        {
            Point point;
            point = new Point(m_display_idx++, hr.m_hold_reg.sensor_value);
            m_chart_display.AppendAsync(base.Dispatcher, point);

        }

        private void UpdateChartDetect(HoldingReg hr)
        {
            Point point;
            point = new Point(m_detect_idx++, hr.m_hold_reg.sensor_detect);
            m_chart_detect.AppendAsync(base.Dispatcher, point);
        }

        private void UpdateChartCFG(HoldingReg hr)
        {
            int i = 0;
            Point point;

            for (i = 0; i < hr.m_hold_reg.calibration_num; i++)
            {
                point = new Point(hr.m_hold_reg.detect[i], hr.m_hold_reg.display[i]);
                m_chart_cfg.AppendAsync(base.Dispatcher, point);
            }
            

        }

        private void UpdateSensorInfo(HoldingReg hr)
        {
            this.tbSensorType.Text = hr.m_hold_reg.sensor_id.ToString();
            this.tbSensorStatus.Text = hr.m_hold_reg.sensor_status.ToString();
            this.tbUpLimit.Text = hr.m_hold_reg.up_limit.ToString();
            this.tbLowLimit.Text = hr.m_hold_reg.low_limit.ToString();
            this.tbSignalType.Text = hr.m_hold_reg.signaltype.ToString();
            this.tbUpLimit.Text = hr.m_hold_reg.up_limit.ToString();
            this.tbWarnCompare.Text = hr.m_hold_reg.warn_compare.ToString();
            this.tbWarnThreshold.Text = hr.m_hold_reg.warn_threshold.ToString();
            this.tbModbusAddr.Text = hr.m_hold_reg.modbusaddr.ToString();
        }

        private void UpdateSensorCalibration(HoldingReg hr)
        {
            this.tbDisplay1.Text = hr.m_hold_reg.display[0].ToString();
            this.tbDisplay2.Text = hr.m_hold_reg.display[1].ToString();
            this.tbDisplay3.Text = hr.m_hold_reg.display[2].ToString();
            this.tbDisplay4.Text = hr.m_hold_reg.display[3].ToString();
            this.tbDisplay5.Text = hr.m_hold_reg.display[4].ToString();
            this.tbDisplay6.Text = hr.m_hold_reg.display[5].ToString();
            this.tbDisplay7.Text = hr.m_hold_reg.display[6].ToString();

            this.tbDetect1.Text = hr.m_hold_reg.detect[0].ToString();
            this.tbDetect2.Text = hr.m_hold_reg.detect[1].ToString();
            this.tbDetect3.Text = hr.m_hold_reg.detect[2].ToString();
            this.tbDetect4.Text = hr.m_hold_reg.detect[3].ToString();
            this.tbDetect5.Text = hr.m_hold_reg.detect[4].ToString();
            this.tbDetect6.Text = hr.m_hold_reg.detect[5].ToString();
            this.tbDetect7.Text = hr.m_hold_reg.detect[6].ToString();

            this.tbDetectCalibration.Text = hr.m_hold_reg.detect_calibration.ToString();
            this.tbCalibrationNum.Text = hr.m_hold_reg.calibration_num.ToString();

        }

        private void UpdateSensorValue(HoldingReg hr)
        {
            this.tbDisplayValue.Text = hr.m_hold_reg.sensor_value.ToString();
            this.tbDetectValue1.Text = hr.m_hold_reg.sensor_detect.ToString();
            this.tbDetectValue2.Text = hr.m_hold_reg.sensor_detect2.ToString();
        }

        private void UpdateUI()
        {
            int rcv_len = 0;
            string rcv_str = "";
            byte[] rcv_buffer = null;
            byte[] incomplete_frame = new byte[1024];
            byte[] complete_frame = new byte[1024];
 
            while (m_com_queue.Count > 0)
            {
                rcv_buffer = m_com_queue.Dequeue();
                Array.Copy(rcv_buffer, 0, complete_frame, rcv_len, rcv_buffer.Length);

                if (rcv_len == 0)
                {
                    Array.Copy(rcv_buffer, 3, incomplete_frame, 0, rcv_buffer.Length - 3);     // first 3 byte frame header
                }
                else
                {
                    Array.Copy(rcv_buffer, 0, incomplete_frame, rcv_len - 3, rcv_buffer.Length);
                }
                rcv_len += rcv_buffer.Length;
                
            }
            Util.HexToString(complete_frame, rcv_len, ref rcv_str);
            if (this.tbComData.Text.Length > 5 * 1024)
            {
                this.tbComData.Text = rcv_str;
                m_total_rcv = rcv_len;
            }
            else
            {
                this.tbComData.Text += rcv_str;
                m_total_rcv += rcv_len;
            }

            this.tbkComRevNum.Text = m_total_rcv.ToString();

            if (Util.CRC16Modbus(complete_frame, rcv_len) == 0)
            {
                switch (complete_frame[1])
                {
                    case Const.FUNC_READHOLDINGREG:
                    if (rcv_len == 107)
                    {
                        m_holding_reg = new HoldingReg(incomplete_frame, rcv_len - 5);     //first 3 byte frame header and last 2 byte crc16

                        UpdateChartDisplay(m_holding_reg);
                        UpdateChartDetect(m_holding_reg);
                        UpdateChartCFG(m_holding_reg);
                        UpdateSensorInfo(m_holding_reg);
                        UpdateSensorCalibration(m_holding_reg);
                        UpdateSensorValue(m_holding_reg);

                        m_total_rcv_cnt++;
                    }
                    break;
                case Const.FUNC_WRITEHOLDINGREG:
                    ;
                    break;
                }
                
            }

            this.tbkComRevCnt.Text = m_total_rcv_cnt.ToString();
            this.tbkComLostRate.Text = String.Format("{0:F2}%", (100.0)*(m_total_snd_cnt - m_total_rcv_cnt) / m_total_snd_cnt);

        }

        private void ResetStatics()
        {
            m_total_rcv_cnt = 0;
            m_total_snd_cnt = 0;
            m_display_idx = 0;
            m_detect_idx = 0;

            this.m_chart_display.Collection.Clear();
            this.m_chart_detect.Collection.Clear();
            this.m_chart_cfg.Collection.Clear();

        }

        private void OpenCom()
        {
            if (!m_com.IsOpen)
                m_com.Open();

            m_is_started = true;
            this.btnStartOrStop.Content = "Stop";

            ResetStatics();

            m_tim_autosend.Interval = TimeSpan.FromSeconds(1);
            m_tim_autosend.Start();
            m_snd_queue.Enqueue(m_auto_snd);
        }

        private void CloseCom()
        {
            if (m_com.IsOpen)
                m_com.Close();

            m_is_started = false;
            this.btnStartOrStop.Content = "Start";
        }

        private void btnStartOrStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!m_is_started)
                {
                    OpenCom();
                }
                else
                {
                    CloseCom();
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("无法打开串口,请检测此串口是否有效或被其他占用！");  
            }
        }

        private void cbCom_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CloseCom();
            m_com.PortName = this.cbCom.SelectedValue.ToString();
            
        }

        private void cbBaud_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CloseCom();
            m_com.BaudRate = Int32.Parse(this.cbBaud.SelectedValue.ToString());
        }

        private void cbAddr_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string str = "";
            CloseCom();

            m_auto_snd[0] = Convert.ToByte(this.cbAddr.SelectedValue.ToString(), 10);

            short crc16 = Util.CRC16Modbus(m_auto_snd, m_auto_snd.Length - 2);
            Util.UpdateCRC16(m_auto_snd, m_auto_snd.Length - 2, crc16);

            Util.HexToString(m_auto_snd, m_auto_snd.Length, ref str);
            this.tbComCmd.Text = str;
        }

        private void btnWarnSet_Click(object sender, RoutedEventArgs e)
        {
            byte[] bytes_to_snd = new byte[11];
            UInt16 len = 0, warn = 0;
            short crc = 0;

            try
            {
                warn = UInt16.Parse(this.tbWarnThreshold.Text);
                bytes_to_snd[len++] = Convert.ToByte(this.cbAddr.SelectedValue.ToString(), 10);     //slaver address
                bytes_to_snd[len++] = Const.FUNC_WRITEHOLDINGREG;     //function code
                bytes_to_snd[len++] = 0x00;
                bytes_to_snd[len++] = 0x16;     // register idx  0x16+1
                bytes_to_snd[len++] = 0x00;
                bytes_to_snd[len++] = 0x01;     // one register
                bytes_to_snd[len++] = 0x02;     // two byte
                bytes_to_snd[len++] = (byte)((warn & 0xff00) >> 8);
                bytes_to_snd[len++] = (byte)(warn & 0xff);

                crc = Util.CRC16Modbus(bytes_to_snd, len);
                Util.UpdateCRC16(bytes_to_snd, len, crc);

                lock (m_snd_queue)
                {
                    m_snd_queue.Enqueue(bytes_to_snd);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("报警数据无效");  
            }
            
        }

    }
}
