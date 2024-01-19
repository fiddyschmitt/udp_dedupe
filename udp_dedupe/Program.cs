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
using udp_dedupe.Utilities;

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
                Checks = new List<Check>()
                {
                    new Check()
                    {
                        TimeWindowInMilliseconds = 5000,
                        Filter = "udp && udp.DstPort == 15000",
                        //Filter = "udp"
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

            if (settings == null)
            {
                Console.WriteLine($"Settings could not be loaded.");
                return;
            }

            var invalidCount = settings
                                .Checks
                                .Select(check =>
                                {
                                    var filterIsValid = WinDivertUtilities.IsFilterValid(check.Filter);
                                    if (!filterIsValid)
                                    {
                                        Console.WriteLine($"Invalid filter: {check.Filter}");
                                    }

                                    return filterIsValid;
                                })
                                .Where(valid => !valid)
                                .Count();
            
            if (invalidCount > 0)
            {
                return;
            }

            var checkers = settings
                                .Checks
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
