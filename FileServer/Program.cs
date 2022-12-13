using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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
        
        public static void Main(string[] args)
        {
            var hostName = ConfigurationManager.AppSettings.Get("HostName");
            var queueName = ConfigurationManager.AppSettings.Get("QueueName");
            var username = ConfigurationManager.AppSettings.Get("Username");
            var password = ConfigurationManager.AppSettings.Get("Password");
            _foldersPath = ConfigurationManager.AppSettings.Get("FoldersPath");
            
            var port = int.Parse(ConfigurationManager.AppSettings.Get("SocketPingPort"));
            Task.Factory.StartNew(() => PingCheck(port), TaskCreationOptions.LongRunning);

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
                while (true)
                {
                    var handler = serverSocket.Accept();
                    using (var networkStream = new NetworkStream(handler))
                    {
                        var data = Encoding.Unicode.GetBytes("Ok");
                        handler.Send(data);
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}