# syntax=docker/dockerfile:1

# Pre-built publish output is passed via build context (no SDK needed).
# CI cross-compiles with `dotnet publish -r linux-x64` / `-r linux-arm64`
# and feeds the output directory as the Docker build context.

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
LABEL org.opencontainers.image.title="njord" \
      org.opencontainers.image.description="Multi-model weather intelligence for Home Assistant" \
      org.opencontainers.image.source="https://github.com/st0o0/njord" \
      org.opencontainers.image.documentation="https://github.com/st0o0/njord#readme"
WORKDIR /app
COPY --chown=$APP_UID . .
RUN mkdir -p /app/data
VOLUME /app/data
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Njord.dll"]
