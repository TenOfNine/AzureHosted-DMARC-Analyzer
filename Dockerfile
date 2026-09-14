FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/DmarcAnalyzer.Core/DmarcAnalyzer.Core.csproj src/DmarcAnalyzer.Core/packages.lock.json src/DmarcAnalyzer.Core/
COPY src/DmarcAnalyzer.Infrastructure/DmarcAnalyzer.Infrastructure.csproj src/DmarcAnalyzer.Infrastructure/packages.lock.json src/DmarcAnalyzer.Infrastructure/
COPY src/DmarcAnalyzer.Web/DmarcAnalyzer.Web.csproj src/DmarcAnalyzer.Web/packages.lock.json src/DmarcAnalyzer.Web/
COPY Directory.Build.props ./

# --locked-mode matches the CI gate (ci.yml) — a lock file out of sync with a csproj fails the
# build here too, rather than silently resolving different package versions than CI verified.
RUN dotnet restore src/DmarcAnalyzer.Web/DmarcAnalyzer.Web.csproj --locked-mode

COPY src/DmarcAnalyzer.Core/ src/DmarcAnalyzer.Core/
COPY src/DmarcAnalyzer.Infrastructure/ src/DmarcAnalyzer.Infrastructure/
COPY src/DmarcAnalyzer.Web/ src/DmarcAnalyzer.Web/

RUN dotnet publish src/DmarcAnalyzer.Web/DmarcAnalyzer.Web.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN useradd --uid 1000 --create-home dmarcanalyzer
USER dmarcanalyzer
COPY --from=build --chown=dmarcanalyzer /app/publish .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "DmarcAnalyzer.Web.dll"]
