FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY UPMS.slnx ./
COPY src/UPMS.Data/UPMS.Data.csproj src/UPMS.Data/
COPY src/UPMS.Ingestion/UPMS.Ingestion.csproj src/UPMS.Ingestion/
COPY src/UPMS.Reporting/UPMS.Reporting.csproj src/UPMS.Reporting/
COPY src/UPMS.Api/UPMS.Api.csproj src/UPMS.Api/
RUN dotnet restore src/UPMS.Api/UPMS.Api.csproj

COPY . .
RUN dotnet publish src/UPMS.Api/UPMS.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "UPMS.Api.dll"]
