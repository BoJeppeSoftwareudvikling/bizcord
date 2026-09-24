FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/channel-microservice/src/ChannelMicroservice.Presentation/ChannelMicroservice.Presentation.csproj", "apps/channel-microservice/src/ChannelMicroservice.Presentation/"]
COPY ["apps/channel-microservice/dtos/ChannelService.Dtos.csproj", "apps/channel-microservice/dtos/"]
COPY ["packages/MessageClient/src/MessageClient.csproj", "packages/MessageClient/src/"]
COPY ["packages/Messages/Messages.csproj", "packages/Messages/"]
RUN dotnet restore "apps/channel-microservice/src/ChannelMicroservice.Presentation/ChannelMicroservice.Presentation.csproj"

COPY . .
WORKDIR "apps/channel-microservice/src/ChannelMicroservice.Presentation/"
RUN dotnet build "ChannelMicroservice.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ChannelMicroservice.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChannelMicroservice.Presentation.dll"]
