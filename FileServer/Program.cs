using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Client;
using FileSystemWork;
using Newtonsoft.Json;
using RabbitMQ.Client.Events;

namespace FileServer
{
    public static class Program
    {
        private static string _foldersPath;
        private static Dictionary<string, List<ClientInfo>> _clients;
        private const int _port = 6457;
        
        public static void Main(string[] args)
        {
            var hostName = ConfigurationManager.AppSettings.Get("HostName");
            var queueName = ConfigurationManager.AppSettings.Get("QueueName");
            var username = ConfigurationManager.AppSettings.Get("Username");
            var password = ConfigurationManager.AppSettings.Get("Password");
            _foldersPath = ConfigurationManager.AppSettings.Get("FoldersPath");
            _clients = new Dictionary<string, List<ClientInfo>>();
            
            var port = int.Parse(ConfigurationManager.AppSettings.Get("SocketPingPort"));
            Task.Factory.StartNew(() => PingCheck(port), TaskCreationOptions.LongRunning);

            if (!Directory.Exists(_foldersPath))
            {
                Directory.CreateDirectory(_foldersPath);
            }

            var queue = new MessageQueue(hostName, queueName, username, password, HandleQueueMessage);
            queue.Start();
            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        private static void HandleQueueMessage(object? model, BasicDeliverEventArgs args)
        {
            try
            {
                var body = args.Body.ToArray();
                var str = Encoding.UTF8.GetString(body);
                var message = JsonConvert.DeserializeObject<Message>(str);
                var dirPath = message.Id == null ? _foldersPath : Path.Combine(_foldersPath, message.Id);
                var creator = new FilesSystemCreator(dirPath);
                var handler = new MessageHandler(creator);
                handler.Handle(message);
                
                if(!_clients.ContainsKey(message.Id))
                    _clients.Add(message.Id, new List<ClientInfo>());
                
                if(_clients[message.Id].FirstOrDefault(x => x.ClientAddress == message.ClientAddress) == null)
                    _clients[message.Id].Add(new ClientInfo(message.ClientAddress, _port));

                foreach (var client in _clients[message.Id].Where(x => x.ClientAddress != message.ClientAddress))
                {
                    try
                    {
                        var length = Encoding.UTF8.GetBytes($"{body.Length}");
                        client.Socket.Send(length);
                        client.Socket.Send(body);
                    }
                    catch (Exception e)
                    {
                        client.Dispose();
                        _clients[message.Id].Remove(client);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void PingCheck(int port)
        {
            var ipPoint = new IPEndPoint(IPAddress.Any, port);
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
             try
            {
                serverSocket.Bind(ipPoint);
                serverSocket.Listen(25);
                Console.WriteLine("Server started");
                while (true)
                {
                    var handler = serverSocket.Accept();
                    using (var networkStream = new NetworkStream(handler))
                    {
                        Console.WriteLine("Was pinged");
                        using (var reader = new BinaryReader(networkStream, Encoding.Unicode, true))
                        {
                            var lengthBytes = new byte[100];
                            int readBytes = handler.Receive(lengthBytes);
                            var data = Encoding.UTF8.GetBytes("Ok");
                            handler.Send(data);
                        }
                    }
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
             
            Console.WriteLine("Server exit");
        }
    }
}