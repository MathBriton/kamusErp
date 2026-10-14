# Contexto de build: raiz do repositório (precisa de global.json e Directory.*.props)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/backend/Kamus.Api/Kamus.Api.csproj src/backend/Kamus.Api/
COPY src/backend/Shared/Kamus.SharedKernel/Kamus.SharedKernel.csproj src/backend/Shared/Kamus.SharedKernel/
COPY src/backend/Shared/Kamus.Infrastructure/Kamus.Infrastructure.csproj src/backend/Shared/Kamus.Infrastructure/
COPY src/backend/Modules/Identidade/Kamus.Modules.Identidade/Kamus.Modules.Identidade.csproj src/backend/Modules/Identidade/Kamus.Modules.Identidade/
COPY src/backend/Modules/Notificacoes/Kamus.Modules.Notificacoes/Kamus.Modules.Notificacoes.csproj src/backend/Modules/Notificacoes/Kamus.Modules.Notificacoes/
COPY src/backend/Modules/Licenciamento/Kamus.Modules.Licenciamento/Kamus.Modules.Licenciamento.csproj src/backend/Modules/Licenciamento/Kamus.Modules.Licenciamento/
RUN dotnet restore src/backend/Kamus.Api/Kamus.Api.csproj
COPY src/backend/ src/backend/
# Versão e commit informados pelo workflow de release (expostos em /api/versao)
ARG APP_VERSION=0.0.0
ARG SOURCE_REVISION=
RUN dotnet publish src/backend/Kamus.Api/Kamus.Api.csproj -c Release -o /app --no-restore -p:Version=$APP_VERSION -p:SourceRevisionId=$SOURCE_REVISION

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Kamus.Api.dll"]
