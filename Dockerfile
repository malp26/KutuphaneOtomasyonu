# 1. Aşama: Çalışma Zamanı (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# 2. Aşama: Derleme (Build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Projelerin bağımlılıklarını (.csproj) Docker içine kopyalıyoruz
COPY ["LibraryAutomation.API/LibraryAutomation.API.csproj", "LibraryAutomation.API/"]
COPY ["LibraryAutomation.Application/LibraryAutomation.Application.csproj", "LibraryAutomation.Application/"]
COPY ["LibraryAutomation.Domain/LibraryAutomation.Domain.csproj", "LibraryAutomation.Domain/"]
COPY ["LibraryAutomation.Infrastructure/LibraryAutomation.Infrastructure.csproj", "LibraryAutomation.Infrastructure/"]

# Tüm katmanları indir ve bağla
RUN dotnet restore "LibraryAutomation.API/LibraryAutomation.API.csproj"

# Kalan tüm kodları kopyala
COPY . .

# Ana projeyi derle
WORKDIR "/src/LibraryAutomation.API"
RUN dotnet build "LibraryAutomation.API.csproj" -c Release -o /app/build

# 3. Aşama: Yayınlama (Publish)
FROM build AS publish
RUN dotnet publish "LibraryAutomation.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 4. Aşama: Son İmaj (Final)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LibraryAutomation.API.dll"]