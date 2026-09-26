FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/Abstrict.Api/Abstrict.Api.csproj src/Abstrict.Api/
RUN dotnet restore src/Abstrict.Api/Abstrict.Api.csproj

COPY src/Abstrict.Api/ src/Abstrict.Api/
RUN dotnet publish src/Abstrict.Api/Abstrict.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Abstrict.Api.dll"]
