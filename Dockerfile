# Build (repo root = build context)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CAKA.Api/CAKA.Api.csproj", "CAKA.Api/"]
RUN dotnet restore "CAKA.Api/CAKA.Api.csproj"
COPY . .
RUN dotnet publish "CAKA.Api/CAKA.Api.csproj" -c Release -o /app/publish

# Run (minimal bellek için ayarlar - Render free tier 512MB)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
EXPOSE 8080
# Render PORT ile çalıştırmak için shell form (PORT env okunabilsin)
CMD dotnet CAKA.Api.dll
