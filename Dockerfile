# Build (repo root = build context)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CAKA.Api/CAKA.Api.csproj", "CAKA.Api/"]
RUN dotnet restore "CAKA.Api/CAKA.Api.csproj"
COPY . .
RUN dotnet publish "CAKA.Api/CAKA.Api.csproj" -c Release -o /app/publish

# Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# Render PORT ortam değişkenini verir; Program.cs bunu kullanır
ENTRYPOINT ["dotnet", "CAKA.Api.dll"]
