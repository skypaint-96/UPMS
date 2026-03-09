FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY UPMS.slnx ./
COPY src/UPMS.Data/UPMS.Data.csproj src/UPMS.Data/
COPY src/UPMS.Ingestion/UPMS.Ingestion.csproj src/UPMS.Ingestion/
COPY src/UPMS.Reporting/UPMS.Reporting.csproj src/UPMS.Reporting/
COPY src/UPMS.Worker/UPMS.Worker.csproj src/UPMS.Worker/
RUN dotnet restore src/UPMS.Worker/UPMS.Worker.csproj

COPY . .
RUN dotnet publish src/UPMS.Worker/UPMS.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
RUN chmod +x /app/UPMS.Worker
ENTRYPOINT ["./UPMS.Worker"]
