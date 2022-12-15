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
        private const string DiskName = "TrrpDisk";
        private const string DefalutPath = @"C:\MySyncDir\TrrpDisk";

        private static List<SyncDirInfo> _syncedDirs;
        private static RemoteFolderManager.RemoteFolderManagerClient client;

        public static async void SendMessage(Message message)
        {
            try
            {
                var response = await client.actionFAsync(new Msg
                {
                    Id = _syncedDirs.Find(dir => message.AbsPath.Contains(dir.Path)).Id,
                    File = ByteString.CopyFrom(message.File),
                    NewPath = message.NewPath,
                    Path = message.Path,
                    Type = (int)message.Type
                });
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
            
            _syncedDirs ??= new List<SyncDirInfo>();

            //для каждой сихронизируемой папки добавляем обработчик, следящий за изменением файл.системы
            bool nativeDirExists = false;
            foreach (var dir in _syncedDirs)
            {
                if (dir.CreatedByClient)
                {
                    nativeDirExists = true;
                }
                dir.FsWorker = new FileSystemWorker(dir.Path);
                dir.MsgHandler = new MessageHandler(new FilesSystemCreator(dir.Path));
            }

            //создаем папку для синхронизации у клиента
            if (!nativeDirExists)
            {
                Console.Write("Укажите путь, где будут хранится общие папки: ");
                String userDiskPath = Path.Join(Console.ReadLine(), DiskName);
                if (!IsValidFilePath(userDiskPath))
                {
                    Console.WriteLine("Указанный путь некорректен, используется путь по умолчанию - " + DefalutPath);
                    userDiskPath = DefalutPath;
                }

                Directory.CreateDirectory(userDiskPath);

                //если не удалось подсоединиться к диспетчеру, то через некоторое время бросится Exception
                Resp? response;
                try
                {
                    response = await client.actionFAsync(new Msg { Type = (int)MsgType.CreateDisk });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Не удалось подключить к сервису синхронизации\n" + ex.Message);
                    return;
                }
                
                if (response.Code == 1) // синхронизируемая папка создана на сервера
                {
                    _syncedDirs.Add(new SyncDirInfo(response.DiskId, userDiskPath, true));
                    File.WriteAllText("index.json", JsonConvert.SerializeObject(_syncedDirs));
                }
                Console.WriteLine($"Ответ сервера: {response.Code}, {response.DiskId}");
            }

            //создаем поток, в котором будет происходить синхронизация
            Task.Factory.StartNew(() => 
                new ClientSyncSocket(int.Parse(ConfigurationManager.AppSettings.Get("Port"))).Run(_syncedDirs), 
                TaskCreationOptions.LongRunning);
 

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
                            _syncedDirs.Add(new SyncDirInfo(id, path, false));
                            File.WriteAllText("index.json", JsonConvert.SerializeObject(_syncedDirs));
                            Console.WriteLine("Синхронизируемая папка успешно добавлена!");
                        }
                        else
                        {
                            Console.WriteLine("Ошибка. Некорректный путь");
                        }
                        
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