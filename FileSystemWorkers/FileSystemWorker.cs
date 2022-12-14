using System;
using System.IO;
using System.Threading;

namespace FileSystemWork
{
    public class FileSystemWorker
    {
        public delegate void FileSystemWorkerHandler(Message msg);
        public event FileSystemWorkerHandler? Notify;
        private readonly string _path;
        private readonly FileSystemWatcher _watcher;

        public FileSystemWorker(string path)
        {
            _path = path;
            _watcher = new FileSystemWatcher(path);
            _watcher.NotifyFilter = NotifyFilters.Attributes 
                                    | NotifyFilters.CreationTime
                                    | NotifyFilters.DirectoryName
                                    | NotifyFilters.FileName
                                    | NotifyFilters.LastAccess
                                    | NotifyFilters.LastWrite
                                    | NotifyFilters.Security
                                    | NotifyFilters.Size;
            _watcher.Changed += OnChanged;
            _watcher.Created += OnCreated;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;

            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;
        }

        private string GetRelativePath(string absPath) => absPath.Substring(_path.Length + 1);

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed || Directory.Exists(e.FullPath))
            {
                return;
            }
            
            byte[] buffer;
            Thread.Sleep(100);
            using (FileStream fs = File.OpenRead(e.FullPath))
            {
                buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);
            }
            Notify?.Invoke(new Message(GetRelativePath(e.FullPath), e.FullPath, MsgType.ChangeFile, buffer));
            Console.WriteLine($"Изменено содержимое: {e.FullPath}\n");
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (!Directory.Exists(e.FullPath))
            {
                byte[] buffer;
                Thread.Sleep(300);
                using (FileStream fs = File.OpenRead(e.FullPath))
                {
                    buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                }
                Notify?.Invoke(new Message(GetRelativePath(e.FullPath), e.FullPath, MsgType.CreateFile, buffer)); 
            }
            else 
                Notify?.Invoke(new Message(GetRelativePath(e.FullPath), e.FullPath, MsgType.CreateDirectory)); 
            Console.WriteLine($"Создано: {e.FullPath}\n");
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            Notify?.Invoke(new Message(GetRelativePath(e.FullPath), e.FullPath, MsgType.Delete));
            Console.WriteLine($"Удалено: {e.FullPath}\n");
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            Notify?.Invoke(Directory.Exists(e.FullPath)
                ? new Message(GetRelativePath(e.OldFullPath), e.FullPath, 
                    GetRelativePath(e.FullPath),  MsgType.RenameDirectory)
                : new Message(GetRelativePath(e.OldFullPath),e.FullPath, 
                    GetRelativePath(e.FullPath), MsgType.RenameFile));

            Console.WriteLine("Переименование:");
            Console.WriteLine($"    Было: {e.OldFullPath}");
            Console.WriteLine($"    Стало: {e.FullPath}\n");
        }

        private void OnError(object sender, ErrorEventArgs e) =>
            PrintException(e.GetException());

        private void PrintException(Exception? ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine("Stacktrace:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                PrintException(ex.InnerException);
            }
        }
    }
}