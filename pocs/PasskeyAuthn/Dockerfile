FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The build context is this POC directory, so project paths must remain inside its src boundary.
COPY src/PasskeyAuthn/PasskeyAuthn.csproj src/PasskeyAuthn/
RUN dotnet restore src/PasskeyAuthn/PasskeyAuthn.csproj

COPY src/PasskeyAuthn/ src/PasskeyAuthn/
RUN dotnet publish src/PasskeyAuthn/PasskeyAuthn.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PasskeyAuthn.dll"]
