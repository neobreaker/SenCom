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
using System.Configuration;

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
        private bool m_is_com_opened = false;       //串口是否已打开
        private bool m_is_started = false;          //自动发送串口命令是否启动
        private UInt64 m_total_snd_cnt = 0;         //总计发送次数    命令除外
        private UInt64 m_total_rcv_cnt = 0;         //总计接收次数    命令除外
        private bool m_is_timeout = false;          //检测串口超时    主要用于命令超时检测
        private ObservableDataSource<Point> m_chart_display = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> m_chart_detect = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> m_chart_detect2 = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> m_chart_cfg = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> m_chart_cfg2 = new ObservableDataSource<Point>();

        private DispatcherTimer m_tim_autosend = null;        //自动发送串口命令定时器

        private string[] m_com_tbl =  { "COM1", "COM2", "COM3", "COM4" };
        private string[] m_baud_tbl = { "4800", "9600", "38400", "115200" };
        private string[] m_addr_tbl = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16" };
        private Dictionary<string, string> m_sensor_type = new Dictionary<string, string>();

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
            this.cpSensorDetect.AddLineGraph(m_chart_detect, Colors.Green);
            this.cpSensorCfg.AddLineGraph(m_chart_cfg, Colors.Green);

            this.cpSensorDetect.AddLineGraph(m_chart_detect2, Colors.Blue);
            this.cpSensorCfg.AddLineGraph(m_chart_cfg2, Colors.Blue);
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

                        if (m_is_started)
                        {
                            m_snd_queue.Enqueue(m_auto_snd);
                            m_tim_autosend.Interval = TimeSpan.FromSeconds(1);
                            m_tim_autosend.Start();
                        }

                        switch(bytes_to_snd[1])
                        {
                        case Const.FUNC_READHOLDINGREG:
                            m_total_snd_cnt++;
                            this.tbkComSndCnt.Text = m_total_snd_cnt.ToString();
                            break;
                        case Const.FUNC_WRITEHOLDINGREG:
                            if (bytes_to_snd[3] != 0x13)        //alter addr cmd doesn't has a response
                                m_is_timeout = true;
                            break;
                        }

                        m_tim_com.Stop();
                        m_tim_com.Interval = 300;     // 300ms receive time out
                        m_tim_com.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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
            point = new Point(m_detect_idx, hr.m_hold_reg.sensor_detect);
            m_chart_detect.AppendAsync(base.Dispatcher, point);

            if (hr != null && (hr.m_hold_reg.sensor_id == 1002))
            {
                point = new Point(m_detect_idx, hr.m_hold_reg.sensor_detect2);
                m_chart_detect2.AppendAsync(base.Dispatcher, point);
            }

            m_detect_idx++;
        }

        private void UpdateChartCFG(HoldingReg hr)
        {
            int i = 0;
            Point point;

            m_chart_cfg.Collection.Clear();
            m_chart_cfg2.Collection.Clear();

            if (hr.m_hold_reg.sensor_id == 1002)
            {
                for (i = 0; i < 4; i++)
                {
                    point = new Point(hr.m_hold_reg.detect[i], hr.m_hold_reg.display[i]);
                    m_chart_cfg.AppendAsync(base.Dispatcher, point);
                }

                for (i = 4; i < 7; i++)
                {
                    point = new Point(hr.m_hold_reg.detect[i], hr.m_hold_reg.display[i]/10);
                    m_chart_cfg2.AppendAsync(base.Dispatcher, point);
                }
            }
            else
            {
                for (i = 0; i < hr.m_hold_reg.calibration_num; i++)
                {
                    point = new Point(hr.m_hold_reg.detect[i], hr.m_hold_reg.display[i]);
                    m_chart_cfg.AppendAsync(base.Dispatcher, point);
                }
            }
            
        }

        private void UpdateSensorInfo(HoldingReg hr)
        {
            this.tbSensorType.Text = ConfigurationManager.AppSettings[hr.m_hold_reg.sensor_id.ToString()];
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

        private int DoFrame(byte[] frame, int frame_len)
        {
            byte[] data = new byte[1024];
            int data_len = frame_len - 5;

            if (frame_len <= 5)
            {
                return -1;
            }
            else
            {
                Array.Copy(frame, 3, data, 0, data_len);
            }

            if (Util.CRC16Modbus(frame, frame_len) == 0)
            {
                switch (frame[1])
                {
                    case Const.FUNC_READHOLDINGREG:
                        if (frame_len == 107)
                        {
                            m_holding_reg = new HoldingReg(data, data_len);     //first 3 byte frame header and last 2 byte crc16

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
                        m_is_timeout = false;
                        if (frame_len == 8)
                        {
                            MessageBox.Show("写入成功");
                        }
                        else
                        {
                            MessageBox.Show("写入失败");
                        }
                        break;
                }
            }
            return 0;
        }

        private void UpdateUI()
        {
            int rcv_len = 0;
            string rcv_str = "";
            byte[] rcv_buffer = null;
            byte[] complete_frame = new byte[1024];
 
            while (m_com_queue.Count > 0)
            {
                rcv_buffer = m_com_queue.Dequeue();
                Array.Copy(rcv_buffer, 0, complete_frame, rcv_len, rcv_buffer.Length);
                rcv_len += rcv_buffer.Length;
            }

            DoFrame(complete_frame, rcv_len);

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

            if (m_is_timeout)
            {
                MessageBox.Show("写入超时");
                m_is_timeout = false;
            }

            this.tbkComRevCnt.Text = m_total_rcv_cnt.ToString();
            this.tbkComLostRate.Text = String.Format("{0:F2}%", (100.0) * (m_total_snd_cnt - m_total_rcv_cnt) / m_total_snd_cnt);
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
            this.m_chart_cfg2.Collection.Clear();

        }

        private void OpenCom()
        {
            try
            {
                if (!m_com.IsOpen)
                    m_com.Open();
                this.btnOpenOrClose.Content = "关闭串口";
                m_is_com_opened = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }

        private void CloseCom()
        {
            try
            {
                if (m_com.IsOpen)
                    m_com.Close();
                this.btnOpenOrClose.Content = "打开串口";
                m_is_com_opened = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }

        private void StartAutoSnd()
        {
            m_is_started = true;
            this.btnStartOrStop.Content = "Stop";

            ResetStatics();

            m_tim_autosend.Interval = TimeSpan.FromSeconds(1);
            m_tim_autosend.Start();
            m_snd_queue.Enqueue(m_auto_snd);
        }

        private void StopAutoSnd()
        {
            m_is_started = false;
            this.btnStartOrStop.Content = "Start";
        }

        private void btnStartOrStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (m_is_com_opened)
                {
                    if (!m_is_started)
                    {
                        StartAutoSnd();
                    }
                    else
                    {
                        StopAutoSnd();
                    }
                }
                else
                {
                    MessageBox.Show("串口未打开");
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }

        private void cbCom_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopAutoSnd();
            CloseCom();
            m_com.PortName = this.cbCom.SelectedValue.ToString();
            
        }

        private void cbBaud_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopAutoSnd();
            CloseCom();
            m_com.BaudRate = Int32.Parse(this.cbBaud.SelectedValue.ToString());
        }

        private void cbAddr_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string str = "";
            StopAutoSnd();

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
                bytes_to_snd[len++] = 0x10;     // register idx  0x10+1
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

        private void btnOpenOrClose_Click(object sender, RoutedEventArgs e)
        {
            if (!m_is_com_opened)
            {
                OpenCom();
            }
            else
            {
                CloseCom(); 
            }
        }

        private void btnModbusAddrSet_Click(object sender, RoutedEventArgs e)
        {
            byte[] bytes_to_snd = new byte[11];
            UInt16 len = 0, addr = 0;
            short crc = 0;

            try
            {
                addr = UInt16.Parse(this.tbModbusAddr.Text);
                bytes_to_snd[len++] = Convert.ToByte(this.cbAddr.SelectedValue.ToString(), 10);     //slaver address
                bytes_to_snd[len++] = Const.FUNC_WRITEHOLDINGREG;     //function code
                bytes_to_snd[len++] = 0x00;
                bytes_to_snd[len++] = 0x13;     // register idx  0x13+1
                bytes_to_snd[len++] = 0x00;
                bytes_to_snd[len++] = 0x01;     // one register
                bytes_to_snd[len++] = 0x02;     // two byte
                bytes_to_snd[len++] = (byte)((addr & 0xff00) >> 8);
                bytes_to_snd[len++] = (byte)(addr & 0xff);

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
