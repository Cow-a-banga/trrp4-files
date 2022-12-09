namespace FileSystemWatcher;

using System;
using System.IO;


public class FileSystemWorker
{
    private readonly FileSystemWatcher _watcher;

    public FileSystemWorker(string path)
    {
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

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed || Directory.Exists(e.FullPath))
        {
            return;
        }

        Console.WriteLine($"Изменено содержимое: {e.FullPath}\n");
    }

    private static void OnCreated(object sender, FileSystemEventArgs e)
    {
        string value = $"Создано: {e.FullPath}\n";
        Console.WriteLine(value);
    }

    private static void OnDeleted(object sender, FileSystemEventArgs e) =>
        Console.WriteLine($"Удалено: {e.FullPath}\n");

    private static void OnRenamed(object sender, RenamedEventArgs e)
    {
        Console.WriteLine("Переименование:");
        Console.WriteLine($"    Было: {e.OldFullPath}");
        Console.WriteLine($"    Стало: {e.FullPath}\n");
    }

    private static void OnError(object sender, ErrorEventArgs e) =>
        PrintException(e.GetException());

    private static void PrintException(Exception? ex)
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
