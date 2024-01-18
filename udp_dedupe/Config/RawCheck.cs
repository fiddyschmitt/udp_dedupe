using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace udp_dedupe.Config
{
    public class RawCheck
    {
        public string Filter = "";
        public int TimeWindowInMilliseconds = 5000;
        public string? PayloadBytesToInspect;
    }
}
