using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace QDN
{
    class Util
    {
        private static string IPV4Address = String.Empty;

        private static Settings Config = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("Settings.json"));

        private static string LookupIP()
        {
            foreach (IPAddress v in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (v.AddressFamily == AddressFamily.InterNetwork)
                {
                    IPV4Address = v.ToString();
                    break;
                }
            }
            return IPV4Address;
        }

        public static string GetIP()
            => (IPV4Address == String.Empty) ? LookupIP() : IPV4Address;

        public static void ThrowError(string error)
        {
            Console.WriteLine(error);
            Console.ReadLine();
        }

        public static void DebugPrint(params object[] values)
        {
            if (Config.Debug)
            {
                foreach (var v in values)
                {
                    Console.Write(v + " ");
                }
                Console.WriteLine("\n");
            }
        }

        public static void LogError(params object[] values)
        {
            string error = "";
            foreach(var v in values)
                error += v + " ";

            error += "\n";
            Console.WriteLine(error);
            File.AppendAllText("Errors.txt", error);
        }
    }
}