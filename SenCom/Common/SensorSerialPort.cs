using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;

namespace SenCom.Common
{
    class SensorSerialPort
    {
        public static SerialPort m_serialport = new SerialPort();
        public static Queue<byte[]> m_snd_queue = new Queue<byte[]>();

    }
}
