FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SharedModels/SharedModels.csproj", "SharedModels/"]
COPY ["apps/channel-microservice/src/WebAPI.csproj", "apps/channel-microservice/src/"]
RUN dotnet restore "apps/channel-microservice/src/WebAPI.csproj"
COPY SharedModels/. SharedModels/
COPY apps/channel-microservice/src/. apps/channel-microservice/src/
WORKDIR /src/apps/channel-microservice/src
RUN dotnet build "WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebAPI.dll"]
