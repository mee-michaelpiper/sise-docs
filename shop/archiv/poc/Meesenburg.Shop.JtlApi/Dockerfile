# Build: docker build -f src/Meesenburg.Shop.JtlApi/Dockerfile -t meesenburg-shop-jtlapi .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/Meesenburg.Shop.JtlApi/Meesenburg.Shop.JtlApi.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
# Ablage der Schnittstellen-Stände (im AKS als Volume mounten)
RUN mkdir -p /app/data/snapshots && chown -R $APP_UID /app/data
# Nicht als root laufen (AKS Pod Security "restricted")
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Meesenburg.Shop.JtlApi.dll"]
