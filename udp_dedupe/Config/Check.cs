using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using udp_dedupe.Utilities;

namespace udp_dedupe.Config
{
    public class Check
    {
        RawCheck RawCheck { get; }
        public string Filter => RawCheck.Filter;
        public int TimeWindowInMilliseconds => RawCheck.TimeWindowInMilliseconds;

        public List<int> BytesToInspect;

        public Check(RawCheck rawCheck)
        {
            RawCheck = rawCheck;

            WinDivertUtilities.IsFilterValid(Filter);

            BytesToInspect = NumberUtilities.ExtractNumbers(rawCheck.PayloadBytesToInspect).ToList();
        }
    }
}
