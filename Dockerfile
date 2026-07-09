FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/PitWall.Core/PitWall.Core.csproj src/PitWall.Core/
COPY src/PitWall.Api/PitWall.Api.csproj src/PitWall.Api/

RUN dotnet restore src/PitWall.Api/PitWall.Api.csproj

COPY . .

RUN dotnet restore src/PitWall.Api/PitWall.Api.csproj

RUN dotnet publish src/PitWall.Api/PitWall.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["sh", "-c", "dotnet PitWall.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]