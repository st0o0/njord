# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:0294f285d062afacc8b5f6d7febb0cd4ee879320e5585fa15f2f4ebf1feb9ba7 AS build
# protoc from Grpc.Tools ships glibc-linked binaries
RUN apk add --no-cache libc6-compat
ARG TARGETARCH
ARG VERSION=0.0.0-dev
WORKDIR /src

COPY protos/ /protos/
COPY src/global.json src/Directory.Build.props src/Directory.Packages.props src/Njord.slnx ./
COPY src/Njord.Budget/Njord.Budget.csproj Njord.Budget/
COPY src/Njord.Persistence/Njord.Persistence.csproj Njord.Persistence/
COPY src/Njord.Messages/Njord.Messages.csproj Njord.Messages/
COPY src/Njord.Core/Njord.Core.csproj Njord.Core/
COPY src/Njord.OpenMeteo/Njord.OpenMeteo.csproj Njord.OpenMeteo/
COPY src/Njord.Pipeline/Njord.Pipeline.csproj Njord.Pipeline/
COPY src/Njord.Grpc/Njord.Grpc.csproj Njord.Grpc/
COPY src/Njord.Polling/Njord.Polling.csproj Njord.Polling/
COPY src/Njord.Features/Njord.Features.csproj Njord.Features/
COPY src/Njord/Njord.csproj Njord/
RUN dotnet restore Njord/Njord.csproj -a ${TARGETARCH}

COPY src/Njord.Budget/ Njord.Budget/
COPY src/Njord.Persistence/ Njord.Persistence/
COPY src/Njord.Messages/ Njord.Messages/
COPY src/Njord.Core/ Njord.Core/
COPY src/Njord.OpenMeteo/ Njord.OpenMeteo/
COPY src/Njord.Pipeline/ Njord.Pipeline/
COPY src/Njord.Grpc/ Njord.Grpc/
COPY src/Njord.Polling/ Njord.Polling/
COPY src/Njord.Features/ Njord.Features/
COPY src/Njord/ Njord/
RUN dotnet publish Njord/Njord.csproj -c Release -a ${TARGETARCH} -o /app /p:Version=${VERSION} /p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:21c3f9798416cba4c0576a947c286231cb71ea2884896cb968253f5c9668f930
ARG VERSION=0.0.0-dev
LABEL org.opencontainers.image.title="njord" \
      org.opencontainers.image.description="Multi-domain weather intelligence for Home Assistant" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.source="https://github.com/st0o0/njord" \
      org.opencontainers.image.documentation="https://github.com/st0o0/njord#readme"
RUN mkdir -p /app/data && chown 1654:1654 /app/data
WORKDIR /app
COPY --from=build --chown=1654 /app .
VOLUME /app/data
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Njord.dll"]
