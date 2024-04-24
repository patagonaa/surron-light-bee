# SurronCommunication_Importer
To deploy this, Docker can be used. There is a Dockerfile under `../../Dockerfile-importer`.
Be aware this does not handle authentication, so a reverse proxy doing Basic Auth is required to use this over the internet.

Example Dockerfile (using docker-compose and Traefik 2)

`docker-compose.yml`:
```yaml
version: "3.7"
services:
  web:
    image: surron-importer
    build:
      context: ./surron-light-bee/bms_comm/src
      dockerfile: ./Dockerfile-importer
    expose:
      - 8080
    environment:
      - "InfluxDb__Url=https://username:password@influxdb.example.com"
      - "InfluxDb__Database=surronlogger"
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.surronlogger-http.rule=Host(`surronlogger.example.com`)"
      - "traefik.http.routers.surronlogger-http.middlewares=surronlogger-auth"
      - "traefik.http.routers.surronlogger-https.rule=Host(`surronlogger.example.com`)"
      - "traefik.http.routers.surronlogger-https.middlewares=surronlogger-auth"
      - "traefik.http.routers.surronlogger-https.tls"
      - "traefik.http.routers.surronlogger-https.tls.certresolver=default"
      - "traefik.http.services.surronlogger-service.loadbalancer.server.port=8080"
      - "traefik.http.middlewares.surronlogger-auth.basicauth.users=surronlogger:<htpasswd_hash>"
    restart: unless-stopped
    networks:
      - default
      - web

networks:
  web:
    external: true
```