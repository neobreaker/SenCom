using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SenCom.Common
{
    class Util
    {
        public static void HexToString(byte[] data, int len, ref string hexstr)
        {
            hexstr = "";
            for (int i = 0; i < len; i++)
            {
                string tmpstr = Convert.ToString(data[i], 16);
                if (tmpstr.Length == 1)
                    tmpstr = "0" + tmpstr;
                hexstr = hexstr + tmpstr + " ";
            }
        }
    }
}
