using System;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using FileSystemWork;
using Grpc.Net.Client;

namespace Client
{
    public static class Program
    {
        private static string _path = @"C:\MySyncDir";
        private static RemoteFolderManager.RemoteFolderManagerClient client;

        private static async void SendMessage(Message message)
        {
            using var sendCall = client.actionF();
            
            await sendCall.RequestStream.WriteAsync(new Msg());

            await sendCall.RequestStream.CompleteAsync();
            var response = await sendCall;
            Console.WriteLine($"Ответ сервера: {response.Code}");
        }

        private static bool IsValidFilePath(string path)
        {
            var invalidChars = string.Join("", Path.GetInvalidPathChars());
            var regex = new Regex("[" + Regex.Escape(string.Join("", invalidChars)) + "]");

            return !regex.IsMatch(path);
        }
    
        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("В качестве аргумента необходимо передать путь до папки, которую необходимо синхронизовать");
                Directory.CreateDirectory(_path);
            }
            else if (!Directory.Exists(args[0]))
            {
                _path = IsValidFilePath(args[0]) ? args[0] : _path;
                Directory.CreateDirectory(_path);
            }

            var fsWorker = new FileSystemWorker(_path);
            fsWorker.Notify += SendMessage; 
            
            using var channel = GrpcChannel.ForAddress(ConfigurationManager.AppSettings.Get("Address"));
            var client = new RemoteFolderManager.RemoteFolderManagerClient(channel);
            
            
            Console.WriteLine("Нажмите любую клавишу, чтобы завершить работу клиента");
            Console.ReadKey();
        }
    }
}