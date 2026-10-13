# Contexto de build: raiz do repositório (precisa de global.json e Directory.*.props)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/backend/Kamus.Api/Kamus.Api.csproj src/backend/Kamus.Api/
COPY src/backend/Shared/Kamus.SharedKernel/Kamus.SharedKernel.csproj src/backend/Shared/Kamus.SharedKernel/
COPY src/backend/Shared/Kamus.Infrastructure/Kamus.Infrastructure.csproj src/backend/Shared/Kamus.Infrastructure/
RUN dotnet restore src/backend/Kamus.Api/Kamus.Api.csproj
COPY src/backend/ src/backend/
# Commit embutido na versão informada por /api/versao (o .git não entra no contexto do build)
ARG SOURCE_REVISION=
RUN dotnet publish src/backend/Kamus.Api/Kamus.Api.csproj -c Release -o /app --no-restore -p:SourceRevisionId=$SOURCE_REVISION

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Kamus.Api.dll"]
