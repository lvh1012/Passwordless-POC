FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY src/MagicLinkAuthn/MagicLinkAuthn.csproj src/MagicLinkAuthn/
RUN dotnet restore src/MagicLinkAuthn/MagicLinkAuthn.csproj --runtime linux-x64

COPY src/MagicLinkAuthn/ src/MagicLinkAuthn/
RUN dotnet publish src/MagicLinkAuthn/MagicLinkAuthn.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["./MagicLinkAuthn"]
