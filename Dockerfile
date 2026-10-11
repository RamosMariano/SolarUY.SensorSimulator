# syntax=docker/dockerfile:1

# ---- Etapa 1: compilar y publicar (imagen con el SDK, pesada) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Se copia primero solo el .csproj y se restaura: mientras no cambien los paquetes,
# Docker reutiliza esta capa y las compilaciones siguientes son mucho más rápidas.
COPY src/SolarUY.SensorSimulator/SolarUY.SensorSimulator.csproj src/SolarUY.SensorSimulator/
RUN dotnet restore src/SolarUY.SensorSimulator/SolarUY.SensorSimulator.csproj

# Recién ahora se copia el código y se publica en modo Release.
COPY src/ src/
RUN dotnet publish src/SolarUY.SensorSimulator/SolarUY.SensorSimulator.csproj \
    -c Release -o /app/publish --no-restore

# ---- Etapa 2: imagen final (solo el runtime, liviana) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# La imagen aspnet ya trae un usuario sin privilegios ("app"): no se ejecuta como root.
USER $APP_UID

# Dentro del contenedor escucha en el puerto 8080 (docker-compose lo publica en el 5100 de tu máquina).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SolarUY.SensorSimulator.dll"]
