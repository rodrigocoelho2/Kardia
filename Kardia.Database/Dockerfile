# Kardia.Database — builds the dacpac and publishes it to SQL Server, then exits.
# Runs on every deploy (docker compose up): creates the database the first time, applies schema changes afterwards.
# Build context: the solution folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Kardia.Database/ Kardia.Database/
RUN dotnet build Kardia.Database/Kardia.Database.sqlproj -c Release \
    && mkdir -p /dacpac \
    && cp "$(find Kardia.Database/bin -name 'Kardia.Database.dacpac' | head -n 1)" /dacpac/

# SqlPackage is a .NET tool; DOTNET_ROLL_FORWARD lets it run on the newest runtime in the image.
FROM mcr.microsoft.com/dotnet/sdk:10.0
ENV PATH="${PATH}:/root/.dotnet/tools" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_ROLL_FORWARD=Major
RUN dotnet tool install --global microsoft.sqlpackage
WORKDIR /deploy
COPY --from=build /dacpac/Kardia.Database.dacpac ./
COPY Kardia.Database/Scripts/deploy.sh ./
ENTRYPOINT ["/bin/bash", "/deploy/deploy.sh"]
