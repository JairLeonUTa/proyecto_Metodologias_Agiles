FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Services/Siha.MsA.Api/Siha.MsA.Api.csproj", "src/Services/Siha.MsA.Api/"]
COPY ["src/BuildingBlocks/Siha.Contracts/Siha.Contracts.csproj", "src/BuildingBlocks/Siha.Contracts/"]
RUN dotnet restore "src/Services/Siha.MsA.Api/Siha.MsA.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/Siha.MsA.Api/Siha.MsA.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# 8080: API REST (HTTP/1.1).  8081: gRPC (HTTP/2, h2c) en un listener dedicado.
ENV ASPNETCORE_URLS=http://+:8080
ENV Grpc__Puerto=8081
EXPOSE 8080 8081
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Siha.MsA.Api.dll"]
