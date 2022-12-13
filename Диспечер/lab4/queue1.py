import json
import pika


def send(data, ip):

    with open('servers.json') as f:
        servers = json.load(f)

    serv = {}
    for server in servers:
        if server["ip"] == ip:
            serv = server

    credentials = pika.PlainCredentials(
        serv['queueUser'],
        serv['queuePassword']
    )

    connection = pika.BlockingConnection(
        pika.ConnectionParameters(
            ip,
            int(serv['queuePort']),
            '/',
            credentials)
    )

    channel = connection.channel()

    channel.queue_declare(queue=serv['queueName'])

    channel.basic_publish(exchange='', routing_key=serv['queueName'], body=json.dumps(data).encode())
    connection.close()
