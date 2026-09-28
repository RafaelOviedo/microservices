FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Microservices.sln ./
COPY src/Services/Customer/Customer.Domain/Customer.Domain.csproj src/Services/Customer/Customer.Domain/
COPY src/Services/Customer/Customer.Application/Customer.Application.csproj src/Services/Customer/Customer.Application/
COPY src/Services/Customer/Customer.Infrastructure/Customer.Infrastructure.csproj src/Services/Customer/Customer.Infrastructure/
COPY src/Services/Customer/Customer.API/Customer.API.csproj src/Services/Customer/Customer.API/
RUN dotnet restore src/Services/Customer/Customer.API/Customer.API.csproj

COPY src/ ./src/
RUN dotnet publish src/Services/Customer/Customer.API/Customer.API.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
RUN mkdir -p /app/logs && chown "$APP_UID:$APP_UID" /app/logs
USER $APP_UID
ENTRYPOINT ["dotnet", "Customer.API.dll"]
