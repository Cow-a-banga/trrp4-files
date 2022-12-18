using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FileSystemWork;
using Newtonsoft.Json;

namespace Client;

public class ClientSyncSocket
{
    private readonly IPEndPoint ipPoint;
    private readonly Socket serverSocket;


    public ClientSyncSocket(int port)
    {
        ipPoint = new IPEndPoint(IPAddress.Any, port);
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public void Run(List<SyncDirInfo> dirInfo)
    {
        serverSocket.Bind(ipPoint);
        serverSocket.Listen(15);
        while (true)
        {
            try
            {
                var handler = serverSocket.Accept();
                string dirId;
                byte[] type = new byte[1];
                handler.Receive(type);
                if (type[0] == 0)
                {
                    using (var output = File.Create("result.zip"))
                    {
                        byte[] readBytes = new byte[handler.ReceiveBufferSize];
                        int bytesReadCount = handler.Receive(readBytes);
                        dirId = Encoding.UTF8.GetString(readBytes.Take(bytesReadCount).ToArray());
                        handler.Send(Encoding.UTF8.GetBytes($"Id ({dirId}) collected"));
                        
                        using (var stream = new NetworkStream(handler))
                        {
                            do
                            {
                                bytesReadCount = stream.Read(readBytes);
                                output.Write(readBytes, 0, bytesReadCount);
                            } while (bytesReadCount > 0);
                        }
                    }
                    
                    var changedDir = dirInfo.Find(dir => dir.Id == dirId);
                    changedDir.FsWorker.Dispose();
                    try
                    {
                        Directory.Delete(changedDir.Path, true);
                        Directory.CreateDirectory(changedDir.Path);
                        ZipFile.ExtractToDirectory("result.zip", changedDir.Path);
                        File.Delete("result.zip");
                    }
                    finally
                    {
                        changedDir.FsWorker = new FileSystemWorker(changedDir.Path);
                    }
                }
                else
                {
                    byte[] readBytes = new byte[handler.ReceiveBufferSize];
                    var allBytes = new List<byte>();

                    using (var stream = new NetworkStream(handler))
                    {
                        int bytesReadCount;
                        do
                        {
                            bytesReadCount = stream.Read(readBytes);
                            allBytes.AddRange(readBytes.Take(bytesReadCount));
                        } while (bytesReadCount > 0);
                    }

                    string messageStr = Encoding.UTF8.GetString(allBytes.ToArray());
                    Message msg = JsonConvert.DeserializeObject<Message>(messageStr);
                    var changedDir = dirInfo.Find(dir => dir.Id == msg.Id);
                    changedDir.FsWorker.Dispose();
                    changedDir.MsgHandler.Handle(msg);
                    changedDir.FsWorker = new FileSystemWorker(changedDir.Path);
                }
                
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при синхронизации");
            }
        }
    }
}