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
        try
        {
            serverSocket.Bind(ipPoint);
            serverSocket.Listen(100);
            Console.WriteLine("Сервер запущен. Ждём входящих соединений");
            while (true)
            {
                var handler = serverSocket.Accept();
                Console.WriteLine("Обрабатываем входящее подключение");

                while (true)
                {
                    var lengthBytes = new byte[3];
                    int readBytes = handler.Receive(lengthBytes);
                    if (readBytes == 0) // конец приема
                        break;
                    int packageLength = int.Parse(Encoding.UTF8.GetString(lengthBytes));
                    byte[] packageBytes = new byte[packageLength];

                    handler.Receive(packageBytes);

                    try
                    {
                        JsonConvert.DeserializeObject<Message>(
                            Encoding.UTF8.GetString(packageBytes));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ошибка при получении данных\n" + ex.Message);
                        Console.WriteLine($"Передаваемая строка {Encoding.UTF8.GetString(packageBytes)}");
                    }

                }

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            Console.WriteLine("Нажмите Enter, чтобы завершить работу программы");
            Console.ReadLine();
        }
    }
}