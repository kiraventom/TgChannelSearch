FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY ["TgChannelSearch/TgChannelSearch.csproj", "TgChannelSearch/"]
COPY ["TgChannelLib/TgChannelLib.csproj", "TgChannelLib/"]

RUN dotnet restore "TgChannelSearch/TgChannelSearch.csproj"

COPY TgChannelSearch/ TgChannelSearch/
COPY TgChannelLib/ TgChannelLib/

WORKDIR /source/TgChannelSearch
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "TgChannelSearch.dll"]
CMD []
