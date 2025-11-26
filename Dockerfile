# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia il file csproj e ripristina le dipendenze
COPY ["KlodTattooWeb.csproj", "./"]
RUN dotnet restore "KlodTattooWeb.csproj"

# Copia tutto il resto e builda
COPY . .
RUN dotnet build "KlodTattooWeb.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "KlodTattooWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copia i file pubblicati
COPY --from=publish /app/publish .

# Railway espone automaticamente la porta
# L'applicazione leggerà la porta dalla variabile d'ambiente PORT
EXPOSE 8080

ENTRYPOINT ["dotnet", "KlodTattooWeb.dll"]
