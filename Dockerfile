# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG TARGETARCH
WORKDIR /src

COPY protos/ /protos/
COPY src/global.json src/Directory.Build.props src/Directory.Packages.props src/Njord.slnx ./
COPY src/Njord/Njord.csproj Njord/
RUN dotnet restore Njord/Njord.csproj -a ${TARGETARCH}

COPY src/Njord/ Njord/
RUN dotnet publish Njord/Njord.csproj -c Release -a ${TARGETARCH} -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble AS prep
RUN mkdir -p /data && chown 1654:1654 /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
LABEL org.opencontainers.image.title="njord" \
      org.opencontainers.image.description="Multi-model weather intelligence for Home Assistant" \
      org.opencontainers.image.source="https://github.com/st0o0/njord" \
      org.opencontainers.image.documentation="https://github.com/st0o0/njord#readme"
WORKDIR /app
COPY --from=build --chown=$APP_UID /app .
COPY --from=prep --chown=$APP_UID /data /app/data
VOLUME /app/data
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Njord.dll"]
