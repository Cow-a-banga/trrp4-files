using System.Text.RegularExpressions;
using FileSystemWatcher;

namespace Client;

public static class Program
{
    private static string _path = @"C:\MySyncDir";
    
    private static bool IsValidFilePath(string path)
    {
        var invalidChars = string.Join("", Path.GetInvalidPathChars());
        var regex = new Regex("[" + Regex.Escape(string.Join("", invalidChars)) + "]");

        return !regex.IsMatch(path);
    }
    
    public static void Main(string[] args)
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
        Console.WriteLine("Нажмите любую клавишу, чтобы завершить работу клиента");
        Console.ReadKey();
    }
}