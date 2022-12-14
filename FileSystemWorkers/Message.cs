using System;

namespace FileSystemWork
{
    public class Message
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string NewPath { get; set;}
        public MsgType Type { get; set;}
        public byte[] File { get; set;}
        public string ClientAddress { get; set; }

        public Message() {}

        public Message(string path, MsgType type)
        {
            Id = "";
            Path = path;
            NewPath = "";
            Type = type;
            File = Array.Empty<byte>();
        }
    
        public Message(string path, string newPath, MsgType type): this(path, type)
        {
            NewPath = newPath;
        }
    
        public Message(string path, MsgType type, byte[] file): this(path, type)
        {
            File = file;
        }

    }
}