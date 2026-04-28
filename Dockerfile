FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SPRMS.API/SPRMS.API.csproj", "SPRMS.API/"]
RUN dotnet restore "SPRMS.API/SPRMS.API.csproj"
COPY . .
WORKDIR "/src/SPRMS.API"
RUN dotnet build "SPRMS.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SPRMS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN mkdir -p /app/uploads /app/logs
COPY --from=publish /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SPRMS.API.dll"]
