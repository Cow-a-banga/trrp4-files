using System.Configuration;
using System.Net.NetworkInformation;
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
        private static RemoteFolderManager.RemoteFolderManagerClient _client;
        
        public static async void SendFileSystemChangeMessage(Message message)
        {
            try
            {
                var response = await _client.actionFAsync(new Msg
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
                Console.WriteLine("Ошибка. " + ex.Message);
            }
        }

        public static async Task<Resp?> SendMessage(Msg msg)
        {
            Resp? response;
            try
            {
                response = await _client.actionFAsync(msg);
                Console.WriteLine($"Ответ сервера: {response.Code}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Неизвестная ошибка\n" + ex.Message);
                return null;
            }

            return response;
        }

        // проверка доступа к интернету
        private static async void PingDispatcher()
        {
            var ping = new Ping();
            bool replyIpStatus = true, prevReplyIpStatus;
            while (true)
            {
                prevReplyIpStatus = replyIpStatus;
                try
                {
                    ping.Send("google.com");
                    replyIpStatus = true;
                }
                catch (Exception)
                {
                    replyIpStatus = false;
                }

                if (!prevReplyIpStatus && replyIpStatus) // соединение восстановлено, синхронизируемся
                {
                    Console.WriteLine("Восстановлено соединение с диспетчером. Происходит синхронизация.. " +
                                      "Не отключайте Ваше устройство");
                    foreach (var dir in _syncedDirs)
                    {
                        await SendMessage(new Msg
                        {
                            Id = dir.Id,
                            Type = (int)MsgType.LoadDisk
                        });
                    }
                }

                Thread.Sleep(35000);
            }
        }
        
        private static void ReadSharedDirsFromFile()
        {
            using FileStream fs = new FileStream("index.json", FileMode.OpenOrCreate);
            using StreamReader sr = new StreamReader(fs);
            try
            {
                _syncedDirs = JsonConvert.DeserializeObject<List<SyncDirInfo>>(sr.ReadToEnd());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось загрузить информацию о синхронизируемых папках\n" + ex.Message);
                _syncedDirs = new List<SyncDirInfo>();
            }
        }

        private static void SyncSharedDirs()
        {
            var actualSyncedDirs = new List<SyncDirInfo>(_syncedDirs);
            foreach (var dir in _syncedDirs)
            {
                // если синхронизируемая папка была удалена
                if (!Directory.Exists(dir.Path))
                {
                    dir.Dispose();
                    actualSyncedDirs.Remove(dir);
                }
            }
            _syncedDirs = actualSyncedDirs;
            File.WriteAllText("index.json", JsonConvert.SerializeObject(_syncedDirs));
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
            _client = new RemoteFolderManager.RemoteFolderManagerClient(channel);

            //считываем папки, которые синхронизируются у клиента
            ReadSharedDirsFromFile();

            //фоновый поток, где проверяем соединение
            Task.Factory.StartNew(PingDispatcher, TaskCreationOptions.LongRunning);

            //фоновый поток, в котором будет происходить синхронизация папок с сервером
            Task.Factory.StartNew(() => 
                new ClientSyncSocket(int.Parse(ConfigurationManager.AppSettings.Get("Port")))
                    .Run(_syncedDirs), TaskCreationOptions.LongRunning);

            //для каждой сихронизируемой папки добавляем обработчик, следящий за изменением файл.системы
            bool nativeDirExists = false;
            foreach (var dir in _syncedDirs)
            {
                if (dir.CreatedByClient)
                {
                    nativeDirExists = true;
                }

                if (!Directory.Exists(dir.Path)) //если синхронизируемая папка была удалена
                    break;

                dir.FsWorker = new FileSystemWorker(dir.Path);
                dir.MsgHandler = new MessageHandler(new FilesSystemCreator(dir.Path));

                var dispatcherResponse = await SendMessage(new Msg
                {
                    Id = dir.Id,
                    Type = (int)MsgType.LoadDisk
                });

                Console.WriteLine(dispatcherResponse?.Code == 1
                    ? $"Папка {dir.Path} успешно синхронизована"
                    : $"Папка {dir.Path} не синхронизована. Ваши текущие изменения не будут сохранены!");
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

                var dispatcherResponse = await SendMessage(new Msg { Type = (int)MsgType.CreateDisk });
                if (dispatcherResponse?.Code == 1) // синхронизируемая папка создана на сервере
                {
                    _syncedDirs.Add(new SyncDirInfo(dispatcherResponse.DiskId, userDiskPath, true));
                    Console.WriteLine("Папка для синхронизации создана");
                }
                else
                {
                    Console.WriteLine("Не удалось создать папку");
                }
            }
            SyncSharedDirs();
            
            do
            {
                Console.Write("\nУправление диском\n" +
                              "0 - Завершение работы\n" +
                              "1 - Посмотреть доступные папки\n" +
                              "2 - Добавить отслеживаемую папку\n" +
                              "3 - Удалить отслеживаемую папку\n" +
                              "Выбор: ");
                string userChoice = Console.ReadLine();
                switch (userChoice)
                {
                    case "0":
                        Console.WriteLine("Завершение работы..");
                        return;
                    case "1":
                        Console.WriteLine("Название синхронизируемой папки - ID папки");
                        SyncSharedDirs();
                        _syncedDirs.ForEach(dir => Console.WriteLine($"{dir.Path} - {dir.Id}"));
                        break;
                    case "2":
                        Console.Write("Введите ID папки другого клиента: ");
                        string id = Console.ReadLine();
                        Console.Write("Укажите путь, где будут хранится синхронизируемая папка: ");
                        string path = Path.Join(Console.ReadLine(), id);
                        if (IsValidFilePath(path) && _syncedDirs.Find(dir => path.Contains(dir.Path)) == null)
                        {
                            Directory.CreateDirectory(path);
                            _syncedDirs.Add(new SyncDirInfo(id, path, false));
                            var dispatcherResponse = await SendMessage(new Msg
                            {
                                Id = id,
                                Type = (int)MsgType.LoadDisk
                            });

                            if (dispatcherResponse is { Code: 1 })
                            {
                                File.WriteAllText("index.json", JsonConvert.SerializeObject(_syncedDirs));
                                Console.WriteLine("Синхронизируемая папка успешно добавлена!");
                            }
                            else
                            {
                                _syncedDirs.Last().Dispose();
                                _syncedDirs.Remove(_syncedDirs.Last());
                                Directory.Delete(_syncedDirs.Last().Path, true);
                                Console.WriteLine("Не удалось создать синхронизируемую папку");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Ошибка. Некорректный путь");
                        }

                        break;

                    case "3":
                        SyncSharedDirs();
                        for (int i = 0; i < _syncedDirs.Count; i++)
                        {
                            Console.WriteLine($"{i + 1} - {_syncedDirs[i].Path}");
                        }
                        Console.Write("Выберите синхронизируемую папку для удаления: ");
                        if (!int.TryParse(Console.ReadLine(), out int numDirForDelete) ||
                            numDirForDelete < 1 || numDirForDelete > _syncedDirs.Count)
                        {
                            Console.WriteLine("Ошибка. Папки с таким номером не существует!");
                        }
                        else if (_syncedDirs[numDirForDelete - 1].CreatedByClient)
                        {
                            Console.WriteLine("Ошибка. Нельзя удалить эту синхронизируемую папку!");
                        }
                        else
                        {
                            var syncDirForDelete = _syncedDirs[numDirForDelete - 1];
                            syncDirForDelete.Dispose();
                            if (Directory.Exists(syncDirForDelete.Path))
                            {
                                Directory.Delete(syncDirForDelete.Path, true);
                            }
                            _syncedDirs.Remove(syncDirForDelete);
                            SyncSharedDirs();
                        }
                        break;

                    default:
                        Console.WriteLine("Ошибка. Пункт отсутствует в меню");
                        break;
                }
            } while (true);
        }
    }
}