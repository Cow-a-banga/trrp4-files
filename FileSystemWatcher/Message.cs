namespace FileSystemWork;

public enum MsgType
{
    CreateDirectory,
    DeleteDirectory,
    RenameDirectory,
    CreateFile,
    DeleteFile,
    ChangeFile,
    RenameFile
}
public class Message
{
    public long Id { get; set; }
    public string Path { get; set; }
    public string? NewPath { get; set; }
    public long Type { get; set; }
    public byte[]? File { get; set; }

    public Message(string path, long type)
    {
        Path = path;
        Type = type;
    }
    
    public Message(string path, string newPath, long type)
    {
        Path = path;
        NewPath = newPath;
        Type = type;
    }
    
    public Message(string path, long type, byte[] file)
    {
        Path = path;
        Type = type;
        File = file;
    }

}