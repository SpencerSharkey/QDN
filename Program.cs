using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QDN
{
    class Program
    {
        private static List<Server> Servers;

        private static void UpdateDatabaseFile(string file, string url)
        {
            using (var client = new WebClient())
            {
                using (MemoryStream originalFileStream = new MemoryStream(client.DownloadData(url)))
                using (FileStream decompressedFileStream = File.Create(file))
                using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                    decompressionStream.CopyTo(decompressedFileStream);

                Console.WriteLine($"Updated {file}!");
            }
        }

        static void Main(string[] args)
        {
            UpdateDatabaseFile("GeoLite2-City.mmdb", "http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz");

            if (!File.Exists("Errors.txt"))
                File.Create("Errors.txt");

            if (!File.Exists("Settings.json"))
                File.WriteAllText("Settings.json", JsonConvert.SerializeObject(new Settings(), Formatting.Indented));

            if (!File.Exists("Servers.json"))
                File.WriteAllText("Servers.json", "[]");

            Servers = JsonConvert.DeserializeObject<List<Server>>(File.ReadAllText("Servers.json"));

            foreach (Server v in Servers)
                v.Init();

            Console.ReadLine();
        }
    }
}
