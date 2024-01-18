using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinDivertSharp;

namespace udp_dedupe.Utilities
{
    public static class WinDivertUtilities
    {
        public static bool IsFilterValid(string filter)
        {
            uint errorPos = 0;
            var result = WinDivert.WinDivertHelperCheckFilter(filter, WinDivertLayer.Network, out _, ref errorPos);

            return result;
        }
    }
}
