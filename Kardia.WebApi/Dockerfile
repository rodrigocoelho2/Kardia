# Kardia.WebApi — the scores and the data. Not published on any port: only the website reaches it.
# Build context: the solution folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY Kardia.Library/Kardia.Library.csproj Kardia.Library/
COPY Kardia.WebApi/Kardia.WebApi.csproj Kardia.WebApi/
RUN dotnet restore Kardia.WebApi/Kardia.WebApi.csproj
COPY Kardia.Library/ Kardia.Library/
COPY Kardia.WebApi/ Kardia.WebApi/
# Files copied from a NAS share can keep owner-only permissions; the app runs as the non-root "app" user.
RUN dotnet publish Kardia.WebApi/Kardia.WebApi.csproj -c Release -o /app --no-restore \
    && chmod -R a+rX /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    TZ=Europe/Lisbon

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Kardia.WebApi.dll"]
