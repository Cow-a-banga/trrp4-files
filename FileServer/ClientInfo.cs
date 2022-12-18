using System;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace FileSystemWork
{
    public class ClientInfo:IDisposable
    {
        public string ClientAddress { get; set; }
        [JsonIgnore]
        public Socket Socket { get; set; }
        [JsonIgnore]
        public IPEndPoint IpPoint { get; set; }
        
        public ClientInfo(){}

        public ClientInfo(string ip, int port)
        {
            ClientAddress = ip;
            IpPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            RecreateSocket();
        }

        public void RecreateSocket()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Dispose()
        {
            Socket.Dispose();
        }
    }
}