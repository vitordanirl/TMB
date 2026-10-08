# syntax=docker/dockerfile:1

# Dockerfile único, multi-stage, com um target por aplicação:
#   docker build --target api      -t orders-api .
#   docker build --target worker   -t orders-worker .
#   docker build --target migrator -t orders-migrator .
# Imagens Alpine: menores e com wget (busybox) para os healthchecks.

ARG DOTNET_VERSION=10.0

# ---------- restore (camada cacheada enquanto só o código muda) ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS restore
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json .editorconfig ./
COPY src/Orders.Domain/Orders.Domain.csproj                 src/Orders.Domain/
COPY src/Orders.Contracts/Orders.Contracts.csproj           src/Orders.Contracts/
COPY src/Orders.Infrastructure/Orders.Infrastructure.csproj src/Orders.Infrastructure/
COPY src/Orders.Api/Orders.Api.csproj                       src/Orders.Api/
COPY src/Orders.Worker/Orders.Worker.csproj                 src/Orders.Worker/

RUN dotnet tool restore \
 && dotnet restore src/Orders.Api/Orders.Api.csproj \
 && dotnet restore src/Orders.Worker/Orders.Worker.csproj

COPY src/ src/

# ---------- build/publish ----------
FROM restore AS publish-api
RUN dotnet publish src/Orders.Api/Orders.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM restore AS publish-worker
RUN dotnet publish src/Orders.Worker/Orders.Worker.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM restore AS publish-migrator
# Bundle de migrations do EF Core: executável único que aplica as migrations pendentes.
RUN dotnet ef migrations bundle \
      --project src/Orders.Infrastructure \
      --startup-project src/Orders.Infrastructure \
      --configuration Release \
      --output /out/efbundle \
      --force

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER $APP_UID

FROM runtime AS api
COPY --from=publish-api --chown=$APP_UID /out .
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
  CMD wget -qO- http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Orders.Api.dll"]

FROM runtime AS worker
COPY --from=publish-worker --chown=$APP_UID /out .
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
  CMD wget -qO- http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Orders.Worker.dll"]

FROM runtime AS migrator
COPY --from=publish-migrator --chown=$APP_UID /out/efbundle .
# A connection string vem de ConnectionStrings__Orders (lida pela DesignTimeDbContextFactory).
ENTRYPOINT ["./efbundle"]
