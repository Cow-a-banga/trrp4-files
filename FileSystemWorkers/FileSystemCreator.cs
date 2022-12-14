using System;
using System.IO;

namespace FileSystemWork
{
    public class FilesSystemCreator
    {
        private string _dirPath;
        private string AbsolutePath(string path) => Path.Combine(_dirPath, path);

        public FilesSystemCreator(string dirPath)
        {
            _dirPath = dirPath;
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(AbsolutePath(path));
        }

        public void RenameDirectory(string oldPath, string newPath)
        {
            Directory.Move(AbsolutePath(oldPath), AbsolutePath(newPath));
        }
        
        public void CreateFile(string path, byte[] inner)
        {
            using (var writer = new BinaryWriter(File.Open(AbsolutePath(path), FileMode.OpenOrCreate)))
            {
                writer.Write(inner);
            }    
        }
        
        public void UpdateFile(string path, byte[] inner)
        {
            using (var writer = new BinaryWriter(File.Open(AbsolutePath(path), FileMode.Truncate)))
            {
                writer.Write(inner);
            }    
        }
        
        public void Delete(string path)
        {
            var attr = File.GetAttributes(AbsolutePath(path));
            if (attr.HasFlag(FileAttributes.Directory))
            {
                Directory.Delete(AbsolutePath(path), true);
            }
            else
            {
                File.Delete(AbsolutePath(path));
            }
        }
        
        public void RenameFile(string oldPath, string newPath)
        {
            File.Move(AbsolutePath(oldPath), AbsolutePath(newPath));
        }
    }
}