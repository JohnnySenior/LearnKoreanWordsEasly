# Single deployable: ASP.NET Core API that also serves the Blazor WebAssembly client.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Lugat.slnx ./
COPY src/ src/
COPY content/ content/
RUN dotnet publish src/Lugat.Api/Lugat.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# pg_dump/pg_restore for the nightly backup (D-22). The client must be at least the server's major version
# (Railway Postgres 18), so it comes from the PostgreSQL apt repository, not the distribution's older one.
# The repository's signing key (fingerprint B97B 0AFC AA1A 47F0 44F2 44A0 7FCC 7D46 ACCC 4CF8) is kept in the repo:
# downloading it from www.postgresql.org failed builds when that site dropped the connection.
COPY docker/apt.postgresql.org.asc /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc
RUN apt-get -o Acquire::Retries=5 update \
 && apt-get install -y --no-install-recommends ca-certificates \
 && . /etc/os-release \
 && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt ${VERSION_CODENAME}-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
 && apt-get -o Acquire::Retries=5 update \
 && apt-get -o Acquire::Retries=5 install -y --no-install-recommends postgresql-client-18 \
 && rm -rf /var/lib/apt/lists/* \
 && pg_dump --version
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Lugat.Api.dll"]
