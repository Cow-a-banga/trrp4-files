using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileSystemWork;
using Google.Protobuf;
using Grpc.Net.Client;

namespace Client
{
    public static class Program
    {
        private static int BUFSIZE = 41943040;
        private static string _path = @"C:\MySyncDir";
        private static RemoteFolderManager.RemoteFolderManagerClient client;

        private static async void SendMessage(Message message)
        {
            try
            {
                using var sendCall = client.actionF();

                if (message.Type == MsgType.CreateFile || message.Type == MsgType.ChangeFile)
                {
                    for (int i = 0; i < message.File.Length / BUFSIZE + 1; i++)
                    {
                        //Console.WriteLine(message.File.Skip(i * BUFSIZE).Take(BUFSIZE).Cast<ByteString>());
                        await sendCall.RequestStream.WriteAsync(new Msg
                        {
                            Id = "0",
                            File = message.File.Length > 0 
                                ? ByteString.CopyFrom(message.File.Skip(i * BUFSIZE).Take(BUFSIZE).ToArray())
                                : ByteString.CopyFrom(message.File),
                            NewPath = "",
                            Path = message.Path,
                            Type = (int)message.Type
                        });
                    }
                }
                else
                {
                   
                    await sendCall.RequestStream.WriteAsync(new Msg
                    {
                        Id = "0",
                        File = ByteString.Empty,
                        NewPath = "",
                        Path = message.Path,
                        Type = (int)message.Type
                    });

                }

                await sendCall.RequestStream.CompleteAsync();
                var response = await sendCall;
                Console.WriteLine($"Ответ сервера: {response.Code}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка " + ex.Message);
            }
            
        }

        private static bool IsValidFilePath(string path)
        {
            var invalidChars = string.Join("", Path.GetInvalidPathChars());
            var regex = new Regex("[" + Regex.Escape(string.Join("", invalidChars)) + "]");

            return !regex.IsMatch(path);
        }
    
        public static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress(ConfigurationManager.AppSettings.Get("Address"));
            client = new RemoteFolderManager.RemoteFolderManagerClient(channel);
            
            /*if (args.Length == 0)
            {
                Console.WriteLine("В качестве аргумента необходимо передать путь до папки, которую необходимо синхронизовать");
                Directory.CreateDirectory(_path);
                
                using var sendCall = client.actionF();
            
                await sendCall.RequestStream.WriteAsync(new Msg {Id = 0, Type = (int) MsgType.CreateDisk});

                await sendCall.RequestStream.CompleteAsync();
                var response = await sendCall;
                Console.WriteLine($"Ответ сервера: {response.Code}");
            }*/
            if (!Directory.Exists(_path))
            {
                //_path = IsValidFilePath(args[0]) ? args[0] : _path;
                Directory.CreateDirectory(_path);
                using var sendCall = client.actionF();
            
                await sendCall.RequestStream.WriteAsync(new Msg {Id = "0", Type = (int) MsgType.CreateDisk});

                await sendCall.RequestStream.CompleteAsync();
                var response = await sendCall;
                Console.WriteLine($"Ответ сервера: {response.Code}");
            }
            
            var fsWorker = new FileSystemWorker(_path);
            fsWorker.Notify += SendMessage;

            Console.WriteLine("Нажмите любую клавишу, чтобы завершить работу клиента");
            Console.ReadKey();
        }
    }
}