# syntax=docker/dockerfile:1
# GameNetty 游戏服务器镜像（Unity 客户端不进镜像）
# 腾讯云个人版仓库：ccr.ccs.tencentyun.com/<命名空间>/<仓库>:<标签>

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Share/ Share/
COPY Server/ Server/
COPY Config/ Config/

RUN dotnet restore Server/Server.sln \
    && dotnet build Server/Server.sln -c Release --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=Asia/Shanghai \
    ET_BIND_ANY=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends socat tzdata \
    && ln -snf /usr/share/zoneinfo/$TZ /etc/localtime \
    && echo $TZ > /etc/timezone \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/Bin /app/Config /app/Logs

WORKDIR /app/Bin

COPY --from=build /src/Bin/ /app/Bin/
COPY --from=build /src/Config/ /app/Config/
COPY docker/entrypoint.sh /app/entrypoint.sh
RUN sed -i 's/\r$//' /app/entrypoint.sh && chmod +x /app/entrypoint.sh

# NetInner TCP / Realm·Gate WebSocket / RouterManager HTTP / Router WebSocket
EXPOSE 20001
EXPOSE 30002
EXPOSE 30003
EXPOSE 30004
EXPOSE 30300
EXPOSE 30301
EXPOSE 30302
EXPOSE 30303
EXPOSE 30304

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["--AppType=Server", "--Process=1", "--StartConfig=StartConfig/Localhost", "--Console=0", "--LogLevel=1"]
