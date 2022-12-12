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

        list_servers=[]
        for i in range(1, 4):
            if config['Serv'][f'visible{i}'] == 'True':
                list_servers.append(i)

        maxi = 99999999
        id = -1

        for i in list_servers:
            cur.execute(f"SELECT COUNT(*) FROM table_name WHERE id_server = {i}")
            rows = cur.fetchall()
            if rows[0][0] < maxi:
                maxi = rows[0][0]
                id = i

        if id == -1:
            return -3

        cur.execute(f"INSERT INTO table_name (id_folder, id_server) VALUES ({idFolder}, {id})")
        con.commit()
        con.close()

        return id
    except psycopg2.Error as error:
        con.close()
        return -1
