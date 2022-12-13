import json
import socket

import psycopg2


def GetIPServer(config, idFolder):
    try:
        con = psycopg2.connect(
            database=config['DB']['name'],
            user=config['DB']['user'],
            password=config['DB']['password'],
            host=config['DB']['host'],
            port=int(config['DB']['port'])
        )

        cur = con.cursor()
        cur.execute(f"SELECT id_server FROM table_name WHERE id_folder = {idFolder}")
        rows = cur.fetchall()
        con.close()

        if len(rows) == 0:
            return -1

        return rows[0][0]
    except psycopg2.Error as error:
        con.close()
        return -1


def getServ(config, idFolder):
    try:
        con = psycopg2.connect(
            database=config['DB']['name'],
            user=config['DB']['user'],
            password=config['DB']['password'],
            host=config['DB']['host'],
            port=int(config['DB']['port'])
        )

        cur = con.cursor()

        with open('servers.json') as f:
            servers = json.load(f)

        list_serv = []


        for server in servers:
            try:
                sock = socket.socket()
                sock.connect(server["ip"], server["port"])
                sock.send(b"ok?")
                sock.recv(10)
                sock.close()
                list_serv.append(server["id"])
            except:
                i = 0


        maxi = 99999999
        id = -1

        for i in list_serv:
            cur.execute(f"SELECT COUNT(*) FROM table_name WHERE id_server = {i}")
            rows = cur.fetchall()
            if rows[0][0] < maxi:
                maxi = rows[0][0]
                id = i

        if id == -1:
            return (-3, 0)

        cur.execute(f"INSERT INTO table_name (id_server) VALUES ({id}) RETURNING id_folder")
        con.commit()
        rows = cur.fetchall()
        con.close()

        return (id, rows[0][0])
    except psycopg2.Error as error:
        con.close()
        return (-1, 0)
