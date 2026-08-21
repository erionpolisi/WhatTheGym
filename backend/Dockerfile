# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Optional extra CA certificates (e.g. corporate TLS interception like Zscaler).
# The certs/ folder is gitignored except for its README; empty on CI.
COPY certs/ /usr/local/share/ca-certificates/extra/
RUN update-ca-certificates

# Restore first for layer caching.
COPY WhatTheGym.sln dotnet-tools.json Directory.Build.props ./
COPY src/Gym.Domain/Gym.Domain.csproj src/Gym.Domain/
COPY src/Gym.Application/Gym.Application.csproj src/Gym.Application/
COPY src/Gym.Infrastructure/Gym.Infrastructure.csproj src/Gym.Infrastructure/
COPY src/Gym.Api/Gym.Api.csproj src/Gym.Api/
RUN dotnet restore src/Gym.Api/Gym.Api.csproj

COPY src/ src/
RUN dotnet publish src/Gym.Api/Gym.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Gym.Api.dll"]
