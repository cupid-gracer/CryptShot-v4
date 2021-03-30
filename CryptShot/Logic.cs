using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptShot
{
    class Logic
    {
        public class Settings
        {
            private static readonly string path = "settings.json";

            public static Settings Load()
            {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
            }

            public void Save()
            {
                string contents = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, contents);
            }

            public string APIKey { get; set; }
            public string APISecret { get; set; }

            public string Quantity { get; set; }
            public string TakeProfitRate { get; set; }
            public string LimitPriceRate { get; set; }
            public string StopLossRate { get; set; }
            public bool HasBNB { get; set; }
            public string BuyLimitPriceRate { get; set; }
            public string BuyType { get; set; }
            public string LoginKey { get; set; }
            public string SellType { get; set; }
            public string LimitSellRate { get; set; }
            public string MarketSellDelaySec { get; set; }
            public string isStreaming { get; set; }
            public string isRefreshOrder { get; set; }

            

        }

        public static void colorOutput(ConsoleColor color, string output)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] - ");
            Console.ForegroundColor = color;
            Console.WriteLine(output);
            Console.ResetColor();
        }

        public static void printTitle()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine(@"  ______                                _     _                  ");
            Console.WriteLine(@" / _____)                      _       | |   | |            _    ");
            Console.WriteLine(@"| /        ____  _   _  ____  | |_      \ \  | | _    ___  | |_  ");
            Console.WriteLine(@"| |       / ___)| | | ||  _ \ |  _)      \ \ | || \  / _ \ |  _) ");
            Console.WriteLine(@"| \_____ | |    | |_| || | | || |__  _____) )| | | || |_| || |__ ");
            Console.WriteLine(@" \______)|_|     \__  || ||_/  \___)(______/ |_| |_| \___/  \___)");
            Console.WriteLine(@"                (____/ |_|                                       ");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;

        }
    }
}
