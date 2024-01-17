using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using udp_dedupe.Config;
using udp_dedupe.Network;

namespace udp_dedupe
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"Please provide settings file as arg");
                return;
            }

            var tmp = new Settings()
            {
                Checks = new List<RawCheck>()
                {
                    new RawCheck()
                    {
                        TimeWindowInMilliseconds = 5000,
                        //Filter = "udp && udp.DstPort == 15000",
                        Filter = "udp",
                        PayloadBytesToInspect = "0-3, 5-6, 10, 13"
                    }
                }
            };
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(tmp, Formatting.Indented));

            var settingsFilename = args[0];
            if (!File.Exists(settingsFilename))
            {
                Console.WriteLine($"File does not exist: {settingsFilename}");
                return;
            }

            var settingsJson = File.ReadAllText(settingsFilename);
            var settings = JsonConvert.DeserializeObject<Settings>(settingsJson);

            var checks = settings
                            .Checks
                            .Select(rawCheck => new Check(rawCheck))
                            .ToList();

            var checkers = checks
                            .Select(check => new Checker(check))
                            .ToList();

            checkers
                .ForEach(checker => checker.Start());

            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
