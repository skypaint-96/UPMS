# Stage 1: Build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore — copy project files first for layer caching
COPY ["src/UPMS.Web/UPMS.Web.csproj", "src/UPMS.Web/"]
COPY ["src/UPMS.Data/UPMS.Data.csproj", "src/UPMS.Data/"]
RUN dotnet restore "src/UPMS.Web/UPMS.Web.csproj"

# Copy full source and publish
COPY . .
WORKDIR "/src/src/UPMS.Web"
RUN dotnet publish "UPMS.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Kestrel listens on HTTP 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UPMS.Web.dll"]
