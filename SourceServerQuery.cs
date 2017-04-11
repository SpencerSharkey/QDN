using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace QDN
{
    class ServerInfoResponse
    {
        public byte[] Data { get; set; }

        public String IP { get; set; }
        public int Port { get; set; }

        public String Name { get; set; }
        public String Map { get; set; }
        public String Directory { get; set; }
        public String Game { get; set; }
        public short AppID { get; set; }
        public int Players { get; set; }
        public int MaxPlayers { get; set; }
        public int Bots { get; set; }
        public Boolean Dedicated { get; set; }
        public String OS { get; set; }
        public Boolean Password { get; set; }
        public Boolean Secure { get; set; }
        public String Version { get; set; }
    }

    class SourceServerQuery
    {
        // this will hold our ip address as an object (required by Socket)
        private IPEndPoint remote;
        // used for single-packet responses (mainly A2S_INFO, A2S_PLAYER and Challenge)
        private Socket socket;
        // multi-packet responses (currently only A2S_RULES)

        // send & receive timeouts
        private int send_timeout = 2500;
        private int receive_timeout = 2500;

        // raw response returned from the server
        private byte[] raw_data;

        private int offset = 0;

        // constants
        private readonly byte[] A2S_HEADER = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        private readonly byte A2S_INFO = 0x54;
        private readonly byte[] A2S_INFO_STUB = new byte[] { 0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00 };

        public SourceServerQuery(String ip, int port)
        {
            this.remote = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public SourceServerQuery(IPAddress ip, int port)
        {
            this.remote = new IPEndPoint(ip, port);
        }

        /// <summary>
        /// Retrieve general information from the Server via A2S_Info.
        /// See https://developer.valvesoftware.com/wiki/Server_queries#A2S_INFO for more Information
        /// </summary>
        /// <returns>A ServerInfoResponse Object containing the publically available data</returns>
        public ServerInfoResponse GetServerInformation()
        {
            // open socket if not already open
            this.GetSocket();
            // reset our pointer
            this.offset = 6;

            ServerInfoResponse sr = new ServerInfoResponse();

            // construct request byte-array
            byte[] request = new byte[A2S_HEADER.Length + A2S_INFO_STUB.Length + 1];
            Array.Copy(this.A2S_HEADER, 0, request, 0, A2S_HEADER.Length);
            request[A2S_HEADER.Length] = this.A2S_INFO;
            Array.Copy(this.A2S_INFO_STUB, 0, request, A2S_HEADER.Length + 1, A2S_INFO_STUB.Length);

            this.socket.Send(request);

            this.raw_data = new byte[512];

            try
            {
                this.socket.Receive(this.raw_data);

                for (int i = this.raw_data.Length; i-- > 0;) // work smarter not harder ok
                {
                    if (this.raw_data[i] != 0x00)
                    {
                        Array.Resize(ref this.raw_data, i + 7);
                        break;
                    }
                }

                // read data
                sr.Data = this.raw_data;
                sr.Name = this.ReadString().Trim().Replace("\0", string.Empty);
                sr.Map = this.ReadString().Trim().Replace("\0", string.Empty);
                sr.Directory = this.ReadString().Trim().Replace("\0", string.Empty);
                sr.Game = this.ReadString().Trim().Replace("\0", string.Empty);
                sr.AppID = this.ReadInt16();
                sr.Players = this.ReadByte();
                sr.MaxPlayers = this.ReadByte();
                sr.Bots = this.ReadByte();
                sr.Dedicated = (this.ReadChar() == 'd') ? true : false;
                sr.OS = (this.ReadChar() == 'l') ? "Linux" : "Windows";
                sr.Password = (this.ReadByte() == 1) ? true : false;
                sr.Secure = (this.ReadByte() == 1) ? true : false;
                sr.Version = this.ReadString().Trim().Replace("\0", string.Empty);
            }
            catch (SocketException)
            {
                sr.Name = "N/A (request timed out)";
                sr.Map = "N/A";
                sr.Directory = "N/A";
                sr.Game = "N/A";
                sr.AppID = -1;
                sr.Players = 0;
                sr.MaxPlayers = 0;
                sr.Bots = -1;
                sr.Dedicated = false;
                sr.OS = "N/A";
                sr.Password = false;
                sr.Secure = false;
                sr.Version = "N/A";
            }

            return sr;
        }

        /// <summary>
        /// Close all currently open socket/UdpClient connections
        /// </summary>
        public void CleanUp()
        {
            if (this.socket != null) this.socket.Close();
        }

        /// <summary>
        /// Set the IP and Port used in this Object.
        /// </summary>
        /// <param name="ip">The Server IP</param>
        /// <param name="port">The Server Port</param>
        public void SetAddress(String ip, int port)
        {
            this.remote = new IPEndPoint(IPAddress.Parse(ip), port);

            if (this.socket != null)
            {
                this.socket.Close();
                this.socket = null;
            }
        }

        /// <summary>
        /// Sets the Send Timeout on both the Socket and the Client
        /// </summary>
        /// <param name="timeout"></param>
        public void SetSendTimeout(int timeout)
        {
            this.send_timeout = timeout;
        }

        /// <summary>
        /// Sets the Receive Timeout on both the Socket and the Client
        /// </summary>
        /// <param name="timeout"></param>
        public void SetReceiveTimeout(int timeout)
        {
            this.receive_timeout = timeout;
        }

        /// <summary>
        /// Open up a new Socket-based connection to a server, if not already open.
        /// </summary>
        private void GetSocket()
        {
            if (this.socket == null)
            {
                this.socket = new Socket(
                            AddressFamily.InterNetwork,
                            SocketType.Dgram,
                            ProtocolType.Udp);

                this.socket.SendTimeout = this.send_timeout;
                this.socket.ReceiveTimeout = this.receive_timeout;

                this.socket.Connect(this.remote);
            }
        }

        /// <summary>
        /// Determine whetever or not a message is compressed.
        /// Simply detects if the most significant bit is 1.
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <returns>true, if message is compressed, false otherwise</returns>
        private bool PacketIsCompressed(int value)
        {
            return (value & 0x8000) != 0;
        }

        /// <summary>
        /// Determine whetever or not a message is split up.
        /// </summary>
        /// <param name="paket">The value to check</param>
        /// <returns>true, if message is split up, false otherwise</returns>
        private bool PacketIsSplit(int paket)
        {
            return (paket == -2);
        }


        /// <summary>
        /// Read a single byte value from our raw data.
        /// </summary>
        /// <returns>A single Byte at the next Offset Address</returns>
        private Byte ReadByte()
        {
            byte[] b = new byte[1];
            Array.Copy(this.raw_data, this.offset, b, 0, 1);

            this.offset++;
            return b[0];
        }

        /// <summary>
        /// Read all remaining Bytes from our raw data.
        /// Used for multi-packet responses.
        /// </summary>
        /// <returns>All remaining data</returns>
        private Byte[] ReadBytes()
        {
            int size = (this.raw_data.Length - this.offset - 4);
            if (size < 1) return new Byte[] { };

            byte[] b = new byte[size];
            Array.Copy(this.raw_data, this.offset, b, 0, this.raw_data.Length - this.offset - 4);

            this.offset += (this.raw_data.Length - this.offset - 4);
            return b;
        }

        /// <summary>
        /// Read a 32-Bit Integer value from the next offset address.
        /// </summary>
        /// <returns>The Int32 Value found at the offset address</returns>
        private Int32 ReadInt32()
        {
            byte[] b = new byte[4];
            Array.Copy(this.raw_data, this.offset, b, 0, 4);

            this.offset += 4;
            return BitConverter.ToInt32(b, 0);
        }

        /// <summary>
        /// Read a 16-Bit Integer (also called "short") value from the next offset address.
        /// </summary>
        /// <returns>The Int16 Value found at the offset address</returns>
        private Int16 ReadInt16()
        {
            byte[] b = new byte[2];
            Array.Copy(this.raw_data, this.offset, b, 0, 2);

            this.offset += 2;
            return BitConverter.ToInt16(b, 0);
        }

        /// <summary>
        /// Read a Float value from the next offset address.
        /// </summary>
        /// <returns>The Float Value found at the offset address</returns>
        private float ReadFloat()
        {
            byte[] b = new byte[4];
            Array.Copy(this.raw_data, this.offset, b, 0, 4);

            this.offset += 4;
            return BitConverter.ToSingle(b, 0);
        }

        /// <summary>
        /// Read a single char value from the next offset address.
        /// </summary>
        /// <returns>The Char found at the offset address</returns>
        private Char ReadChar()
        {
            byte[] b = new byte[1];
            Array.Copy(this.raw_data, this.offset, b, 0, 1);

            this.offset++;
            return (char)b[0];
        }

        /// <summary>
        /// Read a String until its end starting from the next offset address.
        /// Reading stops once the method detects a 0x00 Character at the next position (\0 terminator)
        /// </summary>
        /// <returns>The String read</returns>
        private String ReadString()
        {
            byte[] cache = new byte[1] { 0x01 };
            String output = "";

            while (cache[0] != 0x00)
            {
                if (this.offset == this.raw_data.Length) break; // fixes Valve's inability to code a proper query protocol
                Array.Copy(this.raw_data, this.offset, cache, 0, 1);
                this.offset++;
                output += Encoding.UTF8.GetString(cache);
            }

            return output;
        }
    }
}