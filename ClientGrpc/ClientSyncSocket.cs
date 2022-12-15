﻿using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FileSystemWork;
using Newtonsoft.Json;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;

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
                Console.WriteLine("Server start synchronization. ");

                string dirId;

                Console.Write("Receiving a type. ");
                byte[] type = new byte[1];
                handler.Receive(type);
                Console.WriteLine("[Success]");
                if (type[0] == 0)
                {
                    using (var output = File.Create("result.zip"))
                    {
                        Console.Write("Receiving a file id. ");
                        byte[] readBytes = new byte[handler.ReceiveBufferSize];
                        int bytesReadCount = handler.Receive(readBytes);
                        dirId = Encoding.UTF8.GetString(readBytes.Take(bytesReadCount).ToArray());
                        handler.Send(Encoding.UTF8.GetBytes($"Id ({dirId}) collected"));
                        Console.WriteLine("[Success]");
                        Console.WriteLine($"Id ({dirId}) collected");

                        Console.Write("Receiving a file. ");
                        do
                        {
                            bytesReadCount = handler.Receive(readBytes);
                            output.Write(readBytes, 0, bytesReadCount);
                        } while (bytesReadCount == handler.ReceiveBufferSize);


                        handler.Send(Encoding.UTF8.GetBytes("File collected"));
                        Console.WriteLine("[Success]");
                    }

                    var changedDir = dirInfo.Find(dir => dir.Id == dirId);
                    changedDir.FsWorker.Dispose();
                    Directory.Delete(changedDir.Path, true);
                    Directory.CreateDirectory(changedDir.Path);
                    ZipFile.ExtractToDirectory("result.zip", changedDir.Path);
                    File.Delete("result.zip");
                    changedDir.FsWorker = new FileSystemWorker(changedDir.Path);
                }
                else
                {
                    byte[] readBytes = new byte[handler.ReceiveBufferSize];
                    int bytesReadCount = handler.Receive(readBytes);
                    string messageStr = Encoding.UTF8.GetString(readBytes.Take(bytesReadCount).ToArray());
                    Message msg = JsonConvert.DeserializeObject<Message>(messageStr);
                    var changedDir = dirInfo.Find(dir => dir.Id == msg.Id);
                    changedDir.MsgHandler.Handle(msg);
                }
                
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка " + ex);
            }
        }
    }
}