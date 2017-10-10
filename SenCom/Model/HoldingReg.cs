using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using SenCom.Common;

namespace SenCom.Model
{
    

    class HoldingReg
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HoldingRegStruct
        {
            public float sensor_value;
            public UInt16 sensor_id;
            public UInt16 sensor_status;
            public int sensor_detect;
            public int sensor_detect2;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] reserved;
            public int low_limit;
            public int up_limit;
            public UInt16 warn_threshold;
            public UInt16 warn_compare;
            public UInt16 signaltype;
            public UInt16 modbusaddr;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 7, ArraySubType = UnmanagedType.U4)]
            public int[] detect;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 7, ArraySubType = UnmanagedType.U4)]
            public int[] display;
            public UInt16 calibration_num;
            public int detect_calibration;
             
        };


        public HoldingRegStruct m_hold_reg;
        
        public HoldingReg(byte[] bin, int len)
        {
            int size = Marshal.SizeOf(m_hold_reg);
            IntPtr bufferPtr = Marshal.AllocHGlobal(size);
            if (len == size)
            {
                Marshal.Copy(bin, 0, bufferPtr, size);
                m_hold_reg = (HoldingRegStruct)Marshal.PtrToStructure(bufferPtr, typeof(HoldingRegStruct));
                ChangeRegEndian();
            }
            
        }

        private void ChangeRegEndian()
        {
            int i = 0;
            Endian.SwapBit32(ref m_hold_reg.sensor_value);
            Endian.SwapBit16(ref m_hold_reg.sensor_id);
            Endian.SwapBit16(ref m_hold_reg.sensor_status);
            Endian.SwapBit32(ref m_hold_reg.sensor_detect);
            Endian.SwapBit32(ref m_hold_reg.sensor_detect2);

            Endian.SwapBit32(ref m_hold_reg.low_limit);
            Endian.SwapBit32(ref m_hold_reg.up_limit);
            Endian.SwapBit16(ref m_hold_reg.warn_threshold);
            Endian.SwapBit16(ref m_hold_reg.warn_compare);
            Endian.SwapBit16(ref m_hold_reg.signaltype);
            Endian.SwapBit16(ref m_hold_reg.modbusaddr);

            for (i = 0; i < Const.CALIBRATION_NUM; i++)
            {
                Endian.SwapBit32(ref m_hold_reg.detect[i]);
                Endian.SwapBit32(ref m_hold_reg.display[i]);
            }

            Endian.SwapBit16(ref m_hold_reg.calibration_num);
            Endian.SwapBit32(ref m_hold_reg.detect_calibration);
        }

    }
}
