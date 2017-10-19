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
using System.Windows.Shapes;
using SenCom.Common;

namespace SenCom.Dialog
{
    /// <summary>
    /// WinCalibration.xaml 的交互逻辑
    /// </summary>
    public partial class WinCalibration : Window
    {
        private byte m_addr;

        public WinCalibration()
        {
            InitializeComponent();
        }

        public WinCalibration(byte addr)
        {
            InitializeComponent();
            m_addr = addr;
        }

        private void btnCalibration1_Click(object sender, RoutedEventArgs e)
        {
            byte[] bytes_to_snd = new byte[13];
            UInt16 len = 0, display = 0, detect = 0;
            short crc = 0;

            try
            {
                display = UInt16.Parse(this.tbxDisplay.Text);
                detect = UInt16.Parse(this.tbxDetect.Text);
                bytes_to_snd[len++] = m_addr;
                bytes_to_snd[len++] = Const.FUNC_WRITEHOLDINGREG;     //function code
                bytes_to_snd[len++] = 0x00;
                bytes_to_snd[len++] = 0x5a;     // register idx  0x5a+1
                bytes_to_snd[len++] = 0x00;
                bytes_to_snd[len++] = 0x02;     // one register
                bytes_to_snd[len++] = 0x04;     // 4 byte
                bytes_to_snd[len++] = (byte)((display & 0xff00) >> 8);
                bytes_to_snd[len++] = (byte)(display & 0xff);
                bytes_to_snd[len++] = (byte)((detect & 0xff00) >> 8);
                bytes_to_snd[len++] = (byte)(detect & 0xff);

                crc = Util.CRC16Modbus(bytes_to_snd, len);
                Util.UpdateCRC16(bytes_to_snd, len, crc);

                lock (SensorSerialPort.m_snd_queue)
                {
                    SensorSerialPort.m_snd_queue.Enqueue(bytes_to_snd);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("数据无效");
            }
        }
    }
}
