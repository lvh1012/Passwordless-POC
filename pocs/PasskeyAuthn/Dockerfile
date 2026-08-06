FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# The build context is this POC directory, so project paths must remain inside its src boundary.
COPY src/PasskeyAuthn/PasskeyAuthn.csproj src/PasskeyAuthn/
RUN dotnet restore src/PasskeyAuthn/PasskeyAuthn.csproj \
    --runtime linux-x64

COPY src/PasskeyAuthn/ src/PasskeyAuthn/
RUN dotnet publish src/PasskeyAuthn/PasskeyAuthn.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
# ASP.NET Core reads this standard setting at runtime, so deployments can override the container port without rebuilding the application.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["./PasskeyAuthn"]
