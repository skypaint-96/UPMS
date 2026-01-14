# =============================================================================
# UPMS Worker - .NET Background Service
# =============================================================================
# Multi-stage build for .NET 10 worker service
# =============================================================================

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY src/Worker/*.csproj ./Worker/
RUN dotnet restore Worker/Worker.csproj

# Copy source and build
COPY src/Worker/ ./Worker/
WORKDIR /src/Worker
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Run the application
ENTRYPOINT ["dotnet", "Worker.dll"]
