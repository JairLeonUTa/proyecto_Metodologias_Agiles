FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Services/Siha.MsC.Api/Siha.MsC.Api.csproj", "src/Services/Siha.MsC.Api/"]
COPY ["src/BuildingBlocks/Siha.Contracts/Siha.Contracts.csproj", "src/BuildingBlocks/Siha.Contracts/"]
RUN dotnet restore "src/Services/Siha.MsC.Api/Siha.MsC.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/Siha.MsC.Api/Siha.MsC.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Siha.MsC.Api.dll"]
