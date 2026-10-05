# Builds two images from one source tree:
#   --target web       the API and sign-in pages (Dixels.Portal.Web)
#   --target migrator  creates/updates the database and seeds it (Dixels.Portal.DbMigrator)
# docker-compose.yml at the repo root builds both; see docs/docker.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Node and the ABP CLI fill Dixels.Portal.Web/wwwroot/libs (the sign-in pages' scripts and styles),
# which git does not keep. The CLI version matches the ABP packages.
RUN apt-get update \
 && apt-get install -y --no-install-recommends nodejs npm \
 && rm -rf /var/lib/apt/lists/* \
 && npm install -g yarn
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install -g Volo.Abp.Cli --version 10.6.1

WORKDIR /src

# Restore first, from the project files only, so code changes don't re-download every package.
COPY common.props NuGet.Config ./
COPY src/Dixels.Portal.Domain.Shared/Dixels.Portal.Domain.Shared.csproj src/Dixels.Portal.Domain.Shared/
COPY src/Dixels.Portal.Domain/Dixels.Portal.Domain.csproj src/Dixels.Portal.Domain/
COPY src/Dixels.Portal.Application.Contracts/Dixels.Portal.Application.Contracts.csproj src/Dixels.Portal.Application.Contracts/
COPY src/Dixels.Portal.Application/Dixels.Portal.Application.csproj src/Dixels.Portal.Application/
COPY src/Dixels.Portal.EntityFrameworkCore/Dixels.Portal.EntityFrameworkCore.csproj src/Dixels.Portal.EntityFrameworkCore/
COPY src/Dixels.Portal.HttpApi/Dixels.Portal.HttpApi.csproj src/Dixels.Portal.HttpApi/
COPY src/Dixels.Portal.Web/Dixels.Portal.Web.csproj src/Dixels.Portal.Web/
COPY src/Dixels.Portal.DbMigrator/Dixels.Portal.DbMigrator.csproj src/Dixels.Portal.DbMigrator/
RUN dotnet restore src/Dixels.Portal.Web/Dixels.Portal.Web.csproj \
 && dotnet restore src/Dixels.Portal.DbMigrator/Dixels.Portal.DbMigrator.csproj

COPY src/ src/

RUN cd src/Dixels.Portal.Web && abp install-libs

# Outside Development the sign-in server signs its tokens with openiddict.pfx (git-ignored). Generate one for the
# image; the password is the one PortalWebModule opens it with.
RUN dotnet dev-certs https -v -ep src/Dixels.Portal.Web/openiddict.pfx -p a266993e-a57b-4d12-b2c7-bc95b0e4eeed

# The migrator project always publishes appsettings.secrets.json; the real one is git-ignored and kept out of the
# image, so publish an empty one. Settings come from environment variables instead (docker-compose.yml).
RUN echo '{}' > src/Dixels.Portal.DbMigrator/appsettings.secrets.json

RUN dotnet publish src/Dixels.Portal.Web/Dixels.Portal.Web.csproj -c Release -o /out/web --no-restore \
 && dotnet publish src/Dixels.Portal.DbMigrator/Dixels.Portal.DbMigrator.csproj -c Release -o /out/migrator --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=build --chown=$APP_UID /out/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "Dixels.Portal.DbMigrator.dll"]


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
WORKDIR /app
COPY --from=build --chown=$APP_UID /out/web .
# Logs/ is where the app writes its log file; App_Data/DataProtection-Keys keeps the sign-in cookie keys
# (docker-compose.yml puts a volume there so sign-ins survive a restart).
RUN mkdir -p Logs App_Data/DataProtection-Keys && chown -R $APP_UID Logs App_Data
USER $APP_UID
ENTRYPOINT ["dotnet", "Dixels.Portal.Web.dll"]
