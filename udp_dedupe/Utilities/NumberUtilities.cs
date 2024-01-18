using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace udp_dedupe
{
    public static class NumberUtilities
    {
        public static IEnumerable<int> ExtractNumbers(string numberList)
        {
            if (string.IsNullOrEmpty(numberList) || numberList.Trim().Equals("*"))
            {
                return new List<int>();
            }

            var listRegex = new Regex("(.*?)-(.*)");
            IEnumerable<int> result = new List<int>();

            string[] tokens = numberList.Split(',');

            tokens
                .Select(t => t.Trim())
                .ToList()
                .ForEach(item =>
                {
                    if (int.TryParse(item, out int port))
                    {
                        //It's a normal port
                        var l = new List<int> { port };
                        result = result.Concat(l.AsEnumerable<int>());
                    }
                    else
                    {
                        Match m = listRegex.Match(item);

                        if (m.Success)
                        {
                            int startPort = int.Parse(m.Groups[1].Value.Trim());
                            int endPort = int.Parse(m.Groups[2].Value.Trim());

                            result = result.Concat(Enumerable.Range(startPort, endPort - startPort + 1));
                        }
                    }
                });

            return result;
        }
    }
}
