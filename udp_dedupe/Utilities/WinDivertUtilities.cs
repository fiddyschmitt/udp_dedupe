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

            if (!WinDivert.WinDivertHelperCheckFilter(filter, WinDivertLayer.Network, out string errorMessage, ref errorPos))
            {
                throw new Exception($"Filter string is invalid at position {errorPos}.\nError Message:\n{errorMessage}");
            }

            return true;
        }
    }
}
