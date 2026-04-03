FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["src/Amnezia.Panel.Contracts/Amnezia.Panel.Contracts.csproj", "src/Amnezia.Panel.Contracts/"]
COPY ["src/Amnezia.Agent/Amnezia.Agent.csproj", "src/Amnezia.Agent/"]
RUN dotnet restore "src/Amnezia.Agent/Amnezia.Agent.csproj"

COPY . .
RUN dotnet publish "src/Amnezia.Agent/Amnezia.Agent.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends docker.io \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 9180
ENV ASPNETCORE_URLS=http://0.0.0.0:9180

ENTRYPOINT ["dotnet", "Amnezia.Agent.dll"]
