FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/ChannelService/ChannelService.csproj", "ChannelService/"]
RUN dotnet restore "ChannelService/ChannelService.csproj"
COPY src/ChannelService/. ChannelService/
WORKDIR /src/ChannelService
RUN dotnet build "ChannelService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ChannelService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChannelService.dll"]
