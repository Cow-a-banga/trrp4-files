import grpc
import RFM_pb2_grpc
import RFM_pb2
from concurrent import futures
import socket

import configparser
import json

import db
import queue


# получение ip из конфигов
def GetIp(n):

    with open('servers.json') as f:
        servers = json.load(f)

    for server in servers:
        if server["id"] == n:
            try:
                sock = socket.socket()
                sock.connect(server["ip"], server["port"])
                sock.send(b"ok?")
                sock.recv(10)
                sock.close()
                return server["ip"]
            except:
                return "-1"


def getFile(iterator):
    obj = {"id": 0, "path": "", "newPath": "", "type": 0, "file": b""}

    # собираем в него все содежимое
    for i in iterator:
        obj["id"] = i.id
        obj["path"] = i.path
        obj["newPath"] = i.newPath
        obj["type"] = i.type
        obj["file"] += i.file
    return obj


# описание процедуры
class RemoteFolderManager(RFM_pb2_grpc.RemoteFolderManagerServicer):
    def actionF(self, request_iterator, context):

        config = configparser.ConfigParser()
        config.read("config.ini")

        obj = getFile(request_iterator)

        # создание дирректории
        if obj["type"] == 1:
            id, id_f = db.getServ(config, obj["id"])

            if id == -3:
                return RFM_pb2.Resp(code=-3, id=0)

            ip = GetIp(id)

            # queue.send(obj, ip)
            return RFM_pb2.Resp(code=1, id=id_f)



        id = db.GetIPServer(config, obj["id"])

        # если хотим создать операцию с папкой а её нет то кидаем код ошибки
        if id == -1:
            return RFM_pb2.Resp(code=-1, id=0)

        ip = GetIp(id)

        if ip == "-1":
            return RFM_pb2.Resp(code = -2, id=0)

        # queue.send(obj, ip)
        print(ip + " " + str(obj["id"]))
        return RFM_pb2.Resp(code=1, id=0)


def server():
    config = configparser.ConfigParser()
    config.read("config.ini")

    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    RFM_pb2_grpc.add_RemoteFolderManagerServicer_to_server(RemoteFolderManager(), server)
    server.add_insecure_port(config['Conn']['ip'])
    server.start()
    server.wait_for_termination()


if __name__ == '__main__':
    server()
