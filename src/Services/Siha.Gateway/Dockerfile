FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Services/Siha.Gateway/Siha.Gateway.csproj", "src/Services/Siha.Gateway/"]
RUN dotnet restore "src/Services/Siha.Gateway/Siha.Gateway.csproj"

COPY . .
RUN dotnet publish "src/Services/Siha.Gateway/Siha.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Siha.Gateway.dll"]
