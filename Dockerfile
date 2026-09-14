# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем csproj всех проектов для кэширования restore
COPY ["src/EchoNET.API/EchoNET.API.csproj", "src/EchoNET.API/"]
COPY ["src/EchoNET.Application/EchoNET.Application.csproj", "src/EchoNET.Application/"]
COPY ["src/EchoNET.Domain/EchoNET.Domain.csproj", "src/EchoNET.Domain/"]
COPY ["src/EchoNET.Infrastructure/EchoNET.Infrastructure.csproj", "src/EchoNET.Infrastructure/"]

# Восстанавливаем зависимости через API-проект (подтянет все ProjectReference)
RUN dotnet restore "src/EchoNET.API/EchoNET.API.csproj"

# Копируем весь код
COPY . .

# Публикуем
WORKDIR "/src/src/EchoNET.API"
RUN dotnet publish "EchoNET.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EchoNET.API.dll"]