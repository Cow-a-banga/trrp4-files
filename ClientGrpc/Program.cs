using System.Configuration;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using FileSystemWork;
using Google.Protobuf;
using Grpc.Net.Client;
using FileStream = System.IO.FileStream;

namespace Client
{
    public static class Program
    {
        private const int Bufsize = 41943040;
        private const string DiskName = "TrrpDisk";
        private const string DefalutPath = @"C:\MySyncDir\TrrpDisk";

        private static List<SyncDirInfo> _syncedDirs = new();
        private static List<FileSystemWorker> _fsWorkers;
        private static RemoteFolderManager.RemoteFolderManagerClient client;

        private static async void SendMessage(Message message)
        {
            try
            {
                using var sendCall = client.actionF();

                if (message.Type is MsgType.CreateFile or MsgType.ChangeFile)
                {
                    for (int i = 0; i < message.File.Length / Bufsize + 1; i++)
                    {
                        byte[] partOfFile = new byte[Bufsize];
                        Array.Copy(message.File, i * Bufsize + 1, 
                            partOfFile, 0, Bufsize);
                        await sendCall.RequestStream.WriteAsync(new Msg
                        {
                            Id = _syncedDirs.Find(dir => message.Path.Contains(dir.Path)).Id,
                            File = message.File.Length <= Bufsize ? 
                                ByteString.CopyFrom(message.File) : ByteString.CopyFrom(partOfFile),
                            NewPath = message.NewPath,
                            Path = message.Path,
                            Type = (int)message.Type
                        });
                    }
                }
                else
                {
                    await sendCall.RequestStream.WriteAsync(new Msg
                    {
                        Id =  _syncedDirs.Find(dir => message.Path.Contains(dir.Path)).Id,
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
            using var channel = GrpcChannel.ForAddress(ConfigurationManager.AppSettings.Get("Address")!);
            client = new RemoteFolderManager.RemoteFolderManagerClient(channel);

            //считываем папки, которые синхронизируются у клиента
            using (FileStream fs = new FileStream("index.json", FileMode.OpenOrCreate))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    try
                    {
                        _syncedDirs = JsonConvert.DeserializeObject<List<SyncDirInfo>>(sr.ReadToEnd());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Не удалось загрузить информацию о синхронизируемых папках\n" + ex.Message);
                    }
                }
                
            }

            _fsWorkers = new List<FileSystemWorker>();

            bool nativeDirExists = false;
            //для каждой сихронизируемой папки добавляем обработчик, следящий за изменением файл.системы
            foreach (var dir in _syncedDirs)
            {
                if (dir.CreatedByClient)
                {
                    nativeDirExists = true;
                }

                var fsWorker = new FileSystemWorker(dir.Path);
                fsWorker.Notify += SendMessage;
                _fsWorkers.Add(fsWorker);
            }

            //создаем папку для синхронизации у клиента
            if (!nativeDirExists)
            {
                Console.Write("Укажите путь, где будут хранится общие папки: ");
                String userDiskPath = Path.Join(Console.ReadLine(), DiskName);
                if (!IsValidFilePath(userDiskPath))
                    userDiskPath = DefalutPath;
                Directory.CreateDirectory(userDiskPath);

                //если не удалось подсоединиться к диспетчеру, то через некоторое время бросится Exception
                using var sendCall = client.actionF();

                await sendCall.RequestStream.WriteAsync(new Msg { Type = (int)MsgType.CreateDisk });
                await sendCall.RequestStream.CompleteAsync();

                var response = await sendCall;
                //TODO: обработка разных кодов
                if (response.Code == 1)
                {
                    _syncedDirs.Add(new SyncDirInfo { Id = response.DiskId, Path = userDiskPath });
                    File.WriteAllText("index.json", JsonConvert.SerializeObject(_syncedDirs));
                }

                Console.WriteLine($"Ответ сервера: {response.Code} {response.DiskId}");
            }

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
                        Console.WriteLine("Название синхронизируемой папки - ID папки");
                        _syncedDirs.ForEach(dir => Console.WriteLine($"{dir.Path} - {dir.Id}"));
                        break;
                    case "2":
                        Console.Write("Введите ID папки другого клиента: ");
                        string id = Console.ReadLine();
                        Console.Write("Укажите путь, где будут хранится синхронизируемая папка: ");
                        string path = Path.Join(Console.ReadLine(), DiskName);
                        if (IsValidFilePath(path) && _syncedDirs.Find(dir => path.Contains(dir.Path)) == null)
                        {
                            Directory.CreateDirectory(path);
                            _syncedDirs.Add(new SyncDirInfo { Id = id, Path = path, CreatedByClient = false });
                            File.WriteAllText("index.json", JsonConvert.SerializeObject(_syncedDirs));
                            Console.WriteLine("Синхронизируемая папка успешно добавлена!");
                        }
                        else Console.WriteLine("Ошибка. Некорректный путь");
                        
                        //TODO: вызов удаленной процедуры по клонированию папки на клиент
                        
                        break;
                    default:
                        Console.WriteLine("Ошибка. Пункт отсутствует в меню.");
                        break;
                }
            } while (true);
        }
    }
}