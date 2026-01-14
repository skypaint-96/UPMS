# =============================================================================
# UPMS Backend - ASP.NET Core Web API
# =============================================================================
# Multi-stage build for .NET 10 application
# =============================================================================

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY src/Backend/*.csproj ./Backend/
RUN dotnet restore Backend/Backend.csproj

# Copy source and build
COPY src/Backend/ ./Backend/
WORKDIR /src/Backend
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment
ENV ASPNETCORE_URLS=http://+:8080

# Run the application
ENTRYPOINT ["dotnet", "Backend.dll"]
