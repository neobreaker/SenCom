using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SenCom.Common
{
    class Const
    {
        public const UInt16 CALIBRATION_NUM = 7;

        //modbus
        public const byte FUNC_READHOLDINGREG = 0x03;
        public const byte FUNC_WRITEHOLDINGREG = 0x10;
    }
}
