using System;

namespace FileSystemWork
{
    public abstract class AbstractMessage
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string AbsPath { get; set; }
        public string NewPath { get; set;}
        public MsgType Type { get; set;}
        public string Ip { get; set; }
    }
    
    public class Message:AbstractMessage
    {
        public byte[] File { get; set; }

        public Message()
        {
            
        }

        public Message(string path, string absPath, MsgType type)
        {
            Id = "";
            Path = path;
            AbsPath = absPath;
            NewPath = "";
            Type = type;
            File = Array.Empty<byte>();
        }
    
        public Message(string path, string absPath, string newPath, MsgType type): this(path, absPath, type)
        {
            NewPath = newPath;
        }
    
        public Message(string path, string absPath, MsgType type, byte[] file): this(path, absPath, type)
        {
            File = file;
        }

    }

    public class ServerMessage : AbstractMessage
    {
        public string File { get; set; }
    }
}