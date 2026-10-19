FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Services/Siha.MsB.Api/Siha.MsB.Api.csproj", "src/Services/Siha.MsB.Api/"]
COPY ["src/BuildingBlocks/Siha.Contracts/Siha.Contracts.csproj", "src/BuildingBlocks/Siha.Contracts/"]
RUN dotnet restore "src/Services/Siha.MsB.Api/Siha.MsB.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/Siha.MsB.Api/Siha.MsB.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Siha.MsB.Api.dll"]
