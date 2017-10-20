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
            byte[] hold_reg = new byte[13];
            byte[] coil_reg = new byte[8];
            UInt16 len = 0, display = 0, detect = 0;
            short crc = 0;

            try
            {
                display = UInt16.Parse(this.tbxDisplay.Text);
                detect = UInt16.Parse(this.tbxDetect.Text);
                hold_reg[len++] = m_addr;
                hold_reg[len++] = Const.FUNC_WRITEHOLDINGREG;     //function code
                hold_reg[len++] = 0x00;
                hold_reg[len++] = 0x5a;     // register idx  0x5a+1
                hold_reg[len++] = 0x00;
                hold_reg[len++] = 0x02;     // one register
                hold_reg[len++] = 0x04;     // 4 byte
                hold_reg[len++] = (byte)((display & 0xff00) >> 8);
                hold_reg[len++] = (byte)(display & 0xff);
                hold_reg[len++] = (byte)((detect & 0xff00) >> 8);
                hold_reg[len++] = (byte)(detect & 0xff);

                crc = Util.CRC16Modbus(hold_reg, len);
                Util.UpdateCRC16(hold_reg, len, crc);

                len = 0;
                coil_reg[len++] = m_addr;
                coil_reg[len++] = Const.FUNC_WRITECOILREG;
                coil_reg[len++] = 0x00;
                coil_reg[len++] = 0x00;         //register idx 0x00+1
                coil_reg[len++] = 0xff;        
                coil_reg[len++] = 0x00;         // data

                crc = Util.CRC16Modbus(coil_reg, len);
                Util.UpdateCRC16(coil_reg, len, crc);

                lock (SensorSerialPort.m_snd_queue)
                {
                    SensorSerialPort.m_snd_queue.Enqueue(hold_reg);
                    SensorSerialPort.m_snd_queue.Enqueue(coil_reg);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("数据无效");
            }
        }

        private void btnCalibration2_Click(object sender, RoutedEventArgs e)
        {
            byte[] hold_reg = new byte[13];
            byte[] coil_reg = new byte[8];
            UInt16 len = 0, display = 0, detect = 0;
            short crc = 0;

            try
            {
                display = UInt16.Parse(this.tbxDisplay.Text);
                detect = UInt16.Parse(this.tbxDetect.Text);
                hold_reg[len++] = m_addr;
                hold_reg[len++] = Const.FUNC_WRITEHOLDINGREG;     //function code
                hold_reg[len++] = 0x00;
                hold_reg[len++] = 0x5a;     // register idx  0x5a+1
                hold_reg[len++] = 0x00;
                hold_reg[len++] = 0x02;     // one register
                hold_reg[len++] = 0x04;     // 4 byte
                hold_reg[len++] = (byte)((display & 0xff00) >> 8);
                hold_reg[len++] = (byte)(display & 0xff);
                hold_reg[len++] = (byte)((detect & 0xff00) >> 8);
                hold_reg[len++] = (byte)(detect & 0xff);

                crc = Util.CRC16Modbus(hold_reg, len);
                Util.UpdateCRC16(hold_reg, len, crc);

                len = 0;
                coil_reg[len++] = m_addr;
                coil_reg[len++] = Const.FUNC_WRITECOILREG;
                coil_reg[len++] = 0x00;
                coil_reg[len++] = 0x01;         //register idx 0x01+1
                coil_reg[len++] = 0xff;
                coil_reg[len++] = 0x00;         // data

                crc = Util.CRC16Modbus(coil_reg, len);
                Util.UpdateCRC16(coil_reg, len, crc);

                lock (SensorSerialPort.m_snd_queue)
                {
                    SensorSerialPort.m_snd_queue.Enqueue(hold_reg);
                    SensorSerialPort.m_snd_queue.Enqueue(coil_reg);
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
