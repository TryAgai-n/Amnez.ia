FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["src/Amnezia.Panel.Contracts/Amnezia.Panel.Contracts.csproj", "src/Amnezia.Panel.Contracts/"]
COPY ["src/Amnezia.Panel.Api/Amnezia.Panel.Api.csproj", "src/Amnezia.Panel.Api/"]
RUN dotnet restore "src/Amnezia.Panel.Api/Amnezia.Panel.Api.csproj"

COPY . .
RUN dotnet publish "src/Amnezia.Panel.Api/Amnezia.Panel.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8083
ENV ASPNETCORE_URLS=http://0.0.0.0:8083

ENTRYPOINT ["dotnet", "Amnezia.Panel.Api.dll"]
