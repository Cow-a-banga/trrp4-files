using System;
using System.Text;
using FileSystemWork;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Client
{
    public class MessageQueue
    {
        private string _hostName;
        private string _queueName;
        private string _username;
        private string _password;
        private EventHandler<BasicDeliverEventArgs> _handler;

        public MessageQueue(string hostName, string queueName, string username, string password,
            EventHandler<BasicDeliverEventArgs> handler)
        {
            _hostName = hostName;
            _queueName = queueName;
            _handler = handler;
            _password = password;
            _username = username;
        }

        public void Start()
        {
            var factory = new ConnectionFactory { HostName = _hostName, UserName = _username, Password = _password };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(
                    queue: _queueName,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += _handler;

                channel.BasicConsume(
                    queue: _queueName,
                    autoAck: true,
                    consumer: consumer);

                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
            }
        }
    }
}