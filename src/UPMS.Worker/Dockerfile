FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY UPMS.slnx ./
COPY src/UPMS.Data/UPMS.Data.csproj src/UPMS.Data/
COPY src/UPMS.Ingestion/UPMS.Ingestion.csproj src/UPMS.Ingestion/
COPY src/UPMS.Reporting/UPMS.Reporting.csproj src/UPMS.Reporting/
COPY src/UPMS.Worker/UPMS.Worker.csproj src/UPMS.Worker/
RUN dotnet restore src/UPMS.Worker/UPMS.Worker.csproj

COPY . .
RUN dotnet publish src/UPMS.Worker/UPMS.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UPMS.Worker.dll"]
