using System;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using FileSystemWork;
using Google.Protobuf;
using Grpc.Net.Client;

namespace Client
{
    public static class Program
    {
        private static int BUFSIZE = 41943040;
        private static Dictionary<string, string> syncedDirs = new();
        private static string _diskName = "TrrpDisk";
        private static string _defalutPath = @"C:\MySyncDir\TrrpDisk";
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
    
        public static async Task Main()
        {
            using var channel = GrpcChannel.ForAddress(ConfigurationManager.AppSettings.Get("Address"));
            client = new RemoteFolderManager.RemoteFolderManagerClient(channel);

            string userDiskPath = _defalutPath;
            if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings.Get("PathToDisk")))
            {
                //var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
                //File.WriteAllText(@"D:\myJson.json", json);
            }
            else
            {
                Console.Write("Укажите путь, где будут хранится общие папки: ");
                userDiskPath = Path.Join(Console.ReadLine(), _diskName);
                if (!IsValidFilePath(userDiskPath))
                    userDiskPath = _defalutPath;
                Directory.CreateDirectory(userDiskPath);
                ConfigurationManager.AppSettings.Set("PathToDisk", userDiskPath);
                
                //Если не удалось подсоединиться к диспетчеру, то через некоторое время бросится Exception
                using var sendCall = client.actionF();
                
                await sendCall.RequestStream.WriteAsync(new Msg {Type = (int) MsgType.CreateDisk});
                await sendCall.RequestStream.CompleteAsync();
                
                var response = await sendCall;
                //TODO: обработка разных кодов
                if (response.Code == 1)
                {
                    string diskId = response.DiskId;
                    syncedDirs.Add(userDiskPath, diskId);
                }

                Console.WriteLine($"Ответ сервера: {response.Code} {response.DiskId}");
            }

            var fsWorker = new FileSystemWorker(userDiskPath);
            fsWorker.Notify += SendMessage;

            do
            {
                Console.Write("\nУправление диском\n" +
                                  "0 - Завершение работы\n" +
                                  "1 - Посмотреть доступные папки\n" +
                                  "2 - Добавить отслеживаемую папку\n" +
                                  "Выбор: ");
                string userChoice = Console.ReadLine();
                switch (userChoice)
                {
                    case "0":
                        Console.WriteLine("Завершение работы..");
                        return;
                    case "1":
                        break;
                    case "2":
                        Console.WriteLine("Введите ID папки другого клиента: ");
                        int id = int.Parse(Console.ReadLine());
                        break;
                    default:
                        Console.WriteLine("Ошибка. Пункт отсутствует в меню.");
                        break;
                }

            } while (true);
        }
    }
}