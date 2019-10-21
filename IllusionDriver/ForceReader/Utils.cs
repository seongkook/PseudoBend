using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForceReaderStudy
{
    static class Utils
    {
        public static double lbToN(double lb)
        {
            double mag = 4.448221628;
            return lb * mag;
        }
        public static double lbInToNm(double lbin)
        {
            double mag = 0.112985;
            return lbin * mag;
        }
    }
}
