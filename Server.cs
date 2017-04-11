using System;
using System.Net;
using Newtonsoft.Json;
using System.Threading;
using System.Net.Sockets;
using MaxMind.GeoIP2;
using System.IO;
using MaxMind.GeoIP2.Responses;
using System.Collections.Generic;
using System.Text;

namespace QDN
{
    class Server
    {
        [JsonProperty]
        public string TargetIP;

        [JsonProperty]
        public int TargetPort;

        [JsonProperty]
        public string BindIP;

        [JsonProperty]
        public int BindPort;

        [JsonProperty]
        public bool ShowMaster = false;


        private Settings Config = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("Settings.json"));
        private DatabaseReader GeoIP = new DatabaseReader("GeoLite2-City.mmdb", MaxMind.Db.FileAccessMode.Memory);
        private SourceServerQuery Query;
        private ServerInfoResponse InfoCache;
        private DateTime LastCache = DateTime.Now;

        private Thread PollingThread;

        private IPAddress _TargetIP;
        private IPAddress _BindIP;

        private IPAddress SteamIP;
        private int SteamPort = 27011;
        private EndPoint SteamEndpoint;
        private DateTime LastHeartbeat = DateTime.Now;

        private Socket Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private EndPoint SocketEndpoint;

        private static readonly byte[] Heartbeat = new byte[] { 0x71 };

        public void Init()
        {
            if (!IPAddress.TryParse(TargetIP, out _TargetIP))
                Util.ThrowError("Invalid TargetIP!");

            if (BindIP == "")
                BindIP = Util.GetIP();

            if (!IPAddress.TryParse(BindIP, out _BindIP))
                Util.ThrowError("Invalid BindIP!");

            try
            {
                IPAddress.TryParse(Dns.GetHostEntry("hl2master.steampowered.com").AddressList[0].ToString(), out SteamIP);

                Query = new SourceServerQuery(_TargetIP, TargetPort);

                BuildCache();

                SocketEndpoint = new IPEndPoint(_BindIP, BindPort);

                Socket.Bind(SocketEndpoint);

                if (ShowMaster)
                    SteamEndpoint = new IPEndPoint(SteamIP, SteamPort);

                PollingThread = new Thread(Poll);
                PollingThread.Start();
                 
                Console.WriteLine($"Started on {_BindIP}:{BindPort} targeting {_TargetIP}:{TargetPort} {(ShowMaster ? "on" : "off")} master list");
            }
            catch (Exception e)
            {
                Util.ThrowError("Could not create ServerMirror:\n\n" + e.Message);
            }
        }

        private bool IsValidIP(string ip)
        {
            if (!Config.EnableSubdivisionWhitelist && !Config.EnableSubdivisionBlacklist && !Config.EnableContinentWhitelist && !Config.EnableContinentBlacklist)
                return true;

            try
            {
                CityResponse resp = GeoIP.City(ip);
              
                Util.DebugPrint(resp.Continent);
                Util.DebugPrint(resp.MostSpecificSubdivision.Name);

                if (Config.EnableSubdivisionWhitelist && resp.MostSpecificSubdivision != null && resp.MostSpecificSubdivision.Name != null) // If you've hit this you're on US-West and we only want to respond if you pass this check
                {
                    string sub = resp.MostSpecificSubdivision.Name.ToString();
                    foreach (string v in Config.SubdivisionWhitelist)
                        if (v == sub)
                            return true;

                    return false;
                }

                if (Config.EnableSubdivisionBlacklist && resp.MostSpecificSubdivision != null && resp.MostSpecificSubdivision.Name != null)
                    foreach (string v in Config.SubdivisionBlacklist)
                        if (v == resp.MostSpecificSubdivision.Name.ToString())
                            return false;

                if (Config.EnableContinentWhitelist && resp.Continent != null)
                    foreach (string v in Config.ContinentWhitelist)
                        if (v == resp.Continent.ToString())
                            return true;

                if (Config.EnableContinentBlacklist) // If it hits this state it's the main US-East mirror and either you're located in US east or all checks have failed
                {
                    if (resp.Continent != null)
                        foreach (string v in Config.ContinentBlacklist)
                            if (v == resp.Continent.ToString())
                                return false;

                    return true;
                }
                    
            }
            catch (Exception e)
            {
                Util.LogError($"IsValidIP({ip}):\n{e.Message}\n{e.StackTrace}\n\n");
            }

            return false;
        }

        private void BuildCache()
        {
            LastCache = DateTime.Now.AddSeconds(10);

            try
            {
                InfoCache = Query.GetServerInformation();


                Util.DebugPrint("Built cache");
            }
            catch (Exception e)
            {
                Console.WriteLine("Cache polling error:\n\n" + e.Message);
            }
        }

        private void SendHeartbeat()
        {
            Socket.SendTo(Heartbeat, SteamEndpoint);
            LastHeartbeat = DateTime.Now.AddMinutes(4);
            Console.WriteLine("Send steam master heartneat");
        }

        private void PollSocket()
        {
            try
            {
                if (Socket.Available < 1)
                    return;

                byte[] packet = new byte[1024];
                Socket.ReceiveFrom(packet, ref SocketEndpoint);

                if (packet[0] == 0xFF && packet[1] == 0xFF && packet[2] == 0xFF && packet[3] == 0xFF)
                {
                    switch (packet[4])
                    {
                        case 0x73: // Master list
                            Console.WriteLine("Sent steam master query"); // nice meme https://facepunch.com/showthread.php?t=701929
                            string masterReturn = "0\n\\protocol\\7\\challenge\\" + BitConverter.ToInt32(packet, 6) + "\\gameaddr\\" + BindIP + ":" + BindPort + "\\players\\" + InfoCache.Players + "\\max\\" + InfoCache.MaxPlayers + "\\bots\\0\\gamedir\\garrysmod\\map\\" + InfoCache.Map + "\\password\\0\\os\\l\\lan\\0\\region\\255\\gametype\\" + InfoCache.Game + "\\type\\d\\secure\\1\\version\\" + InfoCache.Version + "\\product\\garrysmod\n";
                            byte[] bytea = Encoding.ASCII.GetBytes(masterReturn);
                            Socket.SendTo(bytea, SocketEndpoint);
                            break;

                        case 0x54: // a2s_info
                            string ip = SocketEndpoint.ToString();
                            ip = ip.Substring(0, ip.IndexOf(':'));

                            if (IsValidIP(ip))
                            {
                                Util.DebugPrint("Sent a2s_info");
                                Socket.SendTo(InfoCache.Data, SocketEndpoint);
                            }
                            else
                            {
                                Util.DebugPrint("Ignoring a2s_info");
                            }
                            break;

                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Query polling error:\n\n" + e.Message);
            }
        }

        private void Poll()
        {
            while (true)
            {
                PollSocket();

                if (LastCache < DateTime.Now)
                    BuildCache();

                if (ShowMaster && LastHeartbeat < DateTime.Now)
                    SendHeartbeat();

                Thread.Sleep(1);
            }
        }
    }
}
