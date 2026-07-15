# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0-dev
WORKDIR /src
# Restore first so the NuGet layer caches independently of source changes
COPY src/global.json src/Directory.Build.props src/Directory.Packages.props ./
COPY protos/ /protos/
COPY src/Njord/Njord.csproj Njord/
COPY src/Njord.ServiceDefaults/Njord.ServiceDefaults.csproj Njord.ServiceDefaults/
RUN dotnet restore Njord/Njord.csproj
COPY src/Njord/ Njord/
COPY src/Njord.ServiceDefaults/ Njord.ServiceDefaults/
RUN dotnet publish Njord/Njord.csproj -c Release -o /app --no-restore -p:Version=${VERSION} \
    && mkdir -p /app/data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
LABEL org.opencontainers.image.title="njord" \
      org.opencontainers.image.description="Open-Meteo weather API to MQTT bridge for Home Assistant" \
      org.opencontainers.image.source="https://github.com/st0o0/njord" \
      org.opencontainers.image.documentation="https://github.com/st0o0/njord#readme"
WORKDIR /app
COPY --from=build --chown=$APP_UID /app .
VOLUME /app/data
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Njord.dll"]
