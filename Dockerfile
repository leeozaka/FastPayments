FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/PagueVeloz.Domain/PagueVeloz.Domain.csproj src/PagueVeloz.Domain/
COPY src/PagueVeloz.Application/PagueVeloz.Application.csproj src/PagueVeloz.Application/
COPY src/PagueVeloz.Infrastructure/PagueVeloz.Infrastructure.csproj src/PagueVeloz.Infrastructure/
COPY src/PagueVeloz.API/PagueVeloz.API.csproj src/PagueVeloz.API/

RUN dotnet restore src/PagueVeloz.API/PagueVeloz.API.csproj

COPY src/ src/

RUN dotnet publish src/PagueVeloz.API/PagueVeloz.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PagueVeloz.API.dll"]
