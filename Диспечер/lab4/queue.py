import json
import pika


def send(config, data, ip):
    credentials = pika.PlainCredentials(
        config['Serv']['name'],
        config['Serv']['password']
    )

    connection = pika.BlockingConnection(
        pika.ConnectionParameters(
            ip,
            int(config['Serv']['port']),
            '/',
            credentials)
    )

    channel = connection.channel()

    channel.queue_declare(queue=config['Serv']['queue'])

    channel.basic_publish(exchange='', routing_key=config['Serv']['queue'], body=json.dumps(data).encode())
    connection.close()
