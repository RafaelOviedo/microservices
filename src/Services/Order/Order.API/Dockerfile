FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Microservices.sln ./
COPY src/Services/Order/Order.Domain/Order.Domain.csproj src/Services/Order/Order.Domain/
COPY src/Services/Order/Order.Application/Order.Application.csproj src/Services/Order/Order.Application/
COPY src/Services/Order/Order.Infrastructure/Order.Infrastructure.csproj src/Services/Order/Order.Infrastructure/
COPY src/Services/Order/Order.API/Order.API.csproj src/Services/Order/Order.API/
RUN dotnet restore src/Services/Order/Order.API/Order.API.csproj

COPY src/ ./src/
RUN dotnet publish src/Services/Order/Order.API/Order.API.csproj \
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
ENTRYPOINT ["dotnet", "Order.API.dll"]
