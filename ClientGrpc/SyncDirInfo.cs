using FileSystemWork;
using Newtonsoft.Json;

namespace Client;

public class SyncDirInfo: IDisposable
{
    private FileSystemWorker _fsWorker;
    public string Id { get; set; }
    public string Path { get; set; }
    public bool CreatedByClient { get; set; }
    
    [JsonIgnore]
    public FileSystemWorker FsWorker
    {
        get => _fsWorker;
        set
        {
            _fsWorker = value;
            FsWorker.Notify += Program.SendFileSystemChangeMessage;
        }
    }
    
    [JsonIgnore]
    public MessageHandler MsgHandler { get; set; }
    
    public SyncDirInfo() {}

    public SyncDirInfo(string id, string path, bool createdByClient)
    {
        Id = id;
        Path = path;
        CreatedByClient = createdByClient;
        FsWorker = new FileSystemWorker(Path);
        FsWorker.Notify += Program.SendFileSystemChangeMessage;
        MsgHandler = new MessageHandler(new FilesSystemCreator(Path));
    }

    public void Dispose()
    {
        FsWorker.Notify -= Program.SendFileSystemChangeMessage;
        FsWorker.Dispose();
    }
}