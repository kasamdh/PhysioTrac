FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Standalone Tailwind CSS CLI (Linux build) to compile the stylesheet before
# publish. The MSBuild target in PhysioTrac.Web.csproj looks for a
# Windows binary at .tools/tailwindcss.exe (a local Windows-dev convenience,
# gitignored) and skips gracefully with a warning when it's absent -- this
# replaces that step for the Linux-based build image.
RUN curl -fsSL -o /usr/local/bin/tailwindcss \
        https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-linux-x64 \
    && chmod +x /usr/local/bin/tailwindcss

COPY ["src/PhysioTrac.Web/PhysioTrac.Web.csproj", "src/PhysioTrac.Web/"]
COPY ["src/PhysioTrac.Domain/PhysioTrac.Domain.csproj", "src/PhysioTrac.Domain/"]
COPY ["src/PhysioTrac.Application/PhysioTrac.Application.csproj", "src/PhysioTrac.Application/"]
COPY ["src/PhysioTrac.Infrastructure/PhysioTrac.Infrastructure.csproj", "src/PhysioTrac.Infrastructure/"]
RUN dotnet restore "src/PhysioTrac.Web/PhysioTrac.Web.csproj"

COPY src/PhysioTrac.Web/ src/PhysioTrac.Web/
COPY src/PhysioTrac.Domain/ src/PhysioTrac.Domain/
COPY src/PhysioTrac.Application/ src/PhysioTrac.Application/
COPY src/PhysioTrac.Infrastructure/ src/PhysioTrac.Infrastructure/

RUN tailwindcss -i src/PhysioTrac.Web/Styles/app.tailwind.css \
    -o src/PhysioTrac.Web/wwwroot/css/app.built.css --minify

WORKDIR /src/src/PhysioTrac.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# See PhysioTrac.Api/Dockerfile's identical line for why.
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
    CMD ["curl", "-f", "http://localhost:8080/login"]

ENTRYPOINT ["dotnet", "PhysioTrac.Web.dll"]
