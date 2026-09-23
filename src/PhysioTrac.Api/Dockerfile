FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/PhysioTrac.Api/PhysioTrac.Api.csproj", "src/PhysioTrac.Api/"]
COPY ["src/PhysioTrac.Domain/PhysioTrac.Domain.csproj", "src/PhysioTrac.Domain/"]
COPY ["src/PhysioTrac.Application/PhysioTrac.Application.csproj", "src/PhysioTrac.Application/"]
COPY ["src/PhysioTrac.Infrastructure/PhysioTrac.Infrastructure.csproj", "src/PhysioTrac.Infrastructure/"]
RUN dotnet restore "src/PhysioTrac.Api/PhysioTrac.Api.csproj"

COPY src/PhysioTrac.Api/ src/PhysioTrac.Api/
COPY src/PhysioTrac.Domain/ src/PhysioTrac.Domain/
COPY src/PhysioTrac.Application/ src/PhysioTrac.Application/
COPY src/PhysioTrac.Infrastructure/ src/PhysioTrac.Infrastructure/

WORKDIR /src/src/PhysioTrac.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# The base runtime image ships without curl; the HEALTHCHECK below needs it.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Program.cs applies migrations and seeds demo data on startup, but only
# when ASPNETCORE_ENVIRONMENT=Development -- set that explicitly in
# docker-compose.yml (or your own deployment) rather than relying on a
# default here, since this image is not meant to auto-seed a production run.
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
    CMD ["curl", "-f", "http://localhost:8080/health"]

ENTRYPOINT ["dotnet", "PhysioTrac.Api.dll"]
