using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
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
        private static int _syncPort;
        
        public static void Main(string[] args)
        {
            var hostName = ConfigurationManager.AppSettings.Get("HostName");
            var queueName = ConfigurationManager.AppSettings.Get("QueueName");
            var username = ConfigurationManager.AppSettings.Get("Username");
            var password = ConfigurationManager.AppSettings.Get("Password");
            _foldersPath = ConfigurationManager.AppSettings.Get("FoldersPath");
            _clients = new Dictionary<string, List<ClientInfo>>();
            _syncPort = int.Parse(ConfigurationManager.AppSettings.Get("SocketSyncPort"));
            var port = int.Parse(ConfigurationManager.AppSettings.Get("SocketPingPort"));
            Task.Factory.StartNew(() => PingCheck(port), TaskCreationOptions.LongRunning);

            if (!Directory.Exists(_foldersPath))
            {
                Directory.CreateDirectory(_foldersPath);
            }

            var queue = new MessageQueue(hostName, queueName, username, password, HandleQueueMessage);
            queue.Start();
        }

        private static void HandleQueueMessage(object? model, BasicDeliverEventArgs args)
        {
            try
            {
                var body = args.Body.ToArray();
                var str = Encoding.UTF8.GetString(body);
                var serverMessage = JsonConvert.DeserializeObject<ServerMessage>(str);
                var ipWithoutVersion = serverMessage.Ip.Substring(serverMessage.Ip.IndexOf(':') + 1);
                var ipWithoutPort = ipWithoutVersion.Substring(0, ipWithoutVersion.IndexOf(':'));
                var message = new Message(serverMessage.Path, serverMessage.AbsPath, serverMessage.Type)
                {
                    Id = serverMessage.Id,
                    NewPath = serverMessage.NewPath,
                    Ip = ipWithoutPort,
                    File = Convert.FromBase64String(serverMessage.File)
                };
                
                if(!_clients.ContainsKey(message.Id))
                    _clients.Add(message.Id, new List<ClientInfo>());

                if (_clients[message.Id].FirstOrDefault(x => x.ClientAddress == message.Ip) == null)
                {
                    ClientInfo client = null;
                    try
                    {
                        client = new ClientInfo(message.Ip, _syncPort);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    
                    if(client != null)
                        _clients[message.Id].Add(client);
                }
                    

                if (message.Type == MsgType.LoadDisk)
                {
                    var pathToDir = Path.Combine(_foldersPath, message.Id);
                    var zipPath = $"{pathToDir}.zip";
                    ClientInfo client = null;
                    try
                    {
                        client = _clients[message.Id].FirstOrDefault(x => x.ClientAddress == message.Ip);
                        if (client == null)
                            return;
                        
                        ZipFile.CreateFromDirectory(pathToDir, zipPath);
                        
                        client.Socket.Connect(client.IpPoint);
                        client.Socket.Send(Encoding.UTF8.GetBytes(message.Id));
                        byte[] data = new byte[1024];
                        var length = client.Socket.Receive(data);
                        var answer = Encoding.UTF8.GetString(data);
                        client.Socket.SendFile(zipPath);
                        length = client.Socket.Receive(data);
                        answer = Encoding.UTF8.GetString(data);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    finally
                    {
                        File.Delete(zipPath);
                        if (client != null && client.Socket.Connected)
                        {
                            client.Socket.Shutdown(SocketShutdown.Both);
                            client.Socket.Close();
                            client.RecreateSocket();
                        }
                            
                    }
                    return;
                }
                
                var dirPath =  message.Type == MsgType.CreateDisk ? _foldersPath : Path.Combine(_foldersPath, message.Id);
                var creator = new FilesSystemCreator(dirPath);
                var handler = new MessageHandler(creator);
                handler.Handle(message);

                foreach (var client in _clients[message.Id].Where(x => x.ClientAddress != message.Ip))
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