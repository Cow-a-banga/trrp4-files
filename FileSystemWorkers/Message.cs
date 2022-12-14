using System;

namespace FileSystemWork
{
    public class Message
    {
        public string Id { get; set; }
        public string Path { get; }
        public string NewPath { get; }
        public int Type { get; }
        public byte[] File { get; }
        public string ClientAddress { get; set; }

        public Message() {}

        public Message(string path, int type)
        {
            Id = "";
            Path = path;
            NewPath = "";
            Type = type;
            File = Array.Empty<byte>();
        }
    
        public Message(string path, string newPath, int type): this(path, type)
        {
            NewPath = newPath;
        }
    
        public Message(string path, int type, byte[] file): this(path, type)
        {
            File = file;
        }

    }
}