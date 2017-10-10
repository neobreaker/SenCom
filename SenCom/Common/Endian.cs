using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SenCom.Common
{
    public static class Endian
    {

        private static void swap(ref byte lhs, ref byte rhs)
        {
            byte tmp = lhs;
            lhs = rhs;
            rhs = tmp;
        }

        public static void SwapBit16(ref UInt16 n)
        {
            byte[] bin = BitConverter.GetBytes(n);
            swap(ref bin[0], ref bin[1]);
            n = BitConverter.ToUInt16(bin, 0);
        }

        public static void SwapBit32(ref float n)
        {
            byte[] bin = BitConverter.GetBytes(n);
            swap(ref bin[0], ref bin[1]);
            swap(ref bin[2], ref bin[3]);
            n = BitConverter.ToSingle(bin, 0);

        }

        public static void SwapBit32(ref int n)
        {
            byte[] bin = BitConverter.GetBytes(n);
            swap(ref bin[0], ref bin[1]);
            swap(ref bin[2], ref bin[3]);
            n = BitConverter.ToInt32(bin, 0);
        }
        
    }
}
