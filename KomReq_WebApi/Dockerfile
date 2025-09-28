FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["KomReq_WebApi/KomReq_WebApi.csproj", "KomReq_WebApi/"]
RUN dotnet restore "KomReq_WebApi/KomReq_WebApi.csproj"
COPY . .
WORKDIR "/src/KomReq_WebApi"
RUN dotnet build "./KomReq_WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./KomReq_WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KomReq_WebApi.dll"]
