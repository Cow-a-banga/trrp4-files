# для grpc (сервисы)
import grpc
import RFM_pb2_grpc
import RFM_pb2
from concurrent import futures

import socket


from threading import Thread
from time import sleep

# работа с конфигами
import configparser

# модуль для работы с бд
import db

# модуль для работы с очередями
import queue



# получение ip из конфигов
def GetIp(n):
    config = configparser.ConfigParser()
    config.read("config.ini")
    if n == 1 and config['Serv']['visible1'] == 'True':
        return config['Serv']['ip1']
    if n == 2 and config['Serv']['visible2'] == 'True':
        return config['Serv']['ip2']
    if n == 3 and config['Serv']['visible3'] == 'True':
        return config['Serv']['ip3']
    if n == -1:
        return "-1"

    return "-2"


# описание процедуры
class RemoteFolderManager(RFM_pb2_grpc.RemoteFolderManagerServicer):
    def actionF(self, request_iterator, context):

        config = configparser.ConfigParser()
        config.read("config.ini")

        # обьявляем объект
        obj = {"id": 0,
               "path": "",
               "newPath": "",
               "type": 0,
               "file": b""}

        # собираем в него все содежимое
        for i in request_iterator:
            obj["id"] = i.id
            obj["path"] = i.path
            obj["newPath"] = i.newPath
            obj["type"] = i.type
            obj["file"] += i.file


        # полчаем id сервера на котром лежит папка
        id = db.GetIPServer(config, obj["id"])

        # делаем запрос в бд что бы получить номер сервера где лежит папка
        ip = GetIp(id)

        # сервер не доступен
        if ip == "-2":
            return RFM_pb2.Resp(code = -2)

        # если на серверах нет такой папки и мы её не создаем то кидаем код ошибки
        if ip == "-1" and obj["type"] != 1:
            return RFM_pb2.Resp(code = -1)

        # если папки нет, а пришло сообщение о её созданиии
        # то ищем самый не нагруженный сервер в бд и привязываем папку к нему
        if ip == "-1" and obj["type"] == 1:
            id = db.getServ(config, obj["id"])
            ip = GetIp(id)

        if ip == "-2":
            return RFM_pb2.Resp(code=-3)

        print(ip + " " + str(obj["id"]))


        # для теста это можно закоментить
        # отправка сообщения в очередь найдееного сервака
        queue.send(config, obj, ip)

        # тут получаем сообщение что все гуд
        return RFM_pb2.Resp(code = 1)


def server():
    config = configparser.ConfigParser()
    config.read("config.ini")

    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    RFM_pb2_grpc.add_RemoteFolderManagerServicer_to_server(RemoteFolderManager(), server)
    server.add_insecure_port(config['Conn']['ip'])
    server.start()
    server.wait_for_termination()

def ping(ip, port, n):
    sock = socket.socket()
    while True:
        try:
            while True:
                sock.connect(ip, port)
                sock.send(b"ok?")
                sock.recv(10)
                sock.close()
                sleep(300)
        except:
            config = configparser.ConfigParser()
            with open("config.ini", 'w') as conf:
                config.write(conf)
                config['Serv'][f'visible{n}'] = "False"


if __name__ == '__main__':
    config = configparser.ConfigParser()
    config.read("config.ini")
    th = Thread(target=server())
    th1 = Thread(target=ping(config['Serv']['ip1'], 5000, 1))
    th2 = Thread(target=ping(config['Serv']['ip2'], 5000, 2))
    th3 = Thread(target=ping(config['Serv']['ip3'], 5000, 3))
    th1.start()
    th2.start()
    th3.start()
    th.start()