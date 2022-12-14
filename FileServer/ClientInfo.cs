using System;
using System.Net;
using System.Net.Sockets;

namespace FileSystemWork
{
    public class ClientInfo:IDisposable
    {
        public string ClientAddress { get; set; }
        public Socket Socket { get; set; }

        public ClientInfo(string ip, int port)
        {
            ClientAddress = ip;
            var ipPoint = new IPEndPoint(IPAddress.Any, port);
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket.Bind(ipPoint);
        }

        public void Dispose()
        {
            Socket.Dispose();
        }
    }
}