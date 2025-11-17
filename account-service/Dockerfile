# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Csproj'u kopyala ve restore yap
COPY Account.Api.csproj ./
RUN dotnet restore

# Tüm dosyaları kopyala
COPY . .

# Publish
RUN dotnet publish -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Account.Api.dll"]


