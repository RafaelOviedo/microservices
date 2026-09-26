FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Microservices.sln ./
COPY src/Services/Product/Product.Domain/Product.Domain.csproj src/Services/Product/Product.Domain/
COPY src/Services/Product/Product.Application/Product.Application.csproj src/Services/Product/Product.Application/
COPY src/Services/Product/Product.Infrastructure/Product.Infrastructure.csproj src/Services/Product/Product.Infrastructure/
COPY src/Services/Product/Product.API/Product.API.csproj src/Services/Product/Product.API/
RUN dotnet restore Microservices.sln

COPY src/ ./src/
RUN dotnet publish src/Services/Product/Product.API/Product.API.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Product.API.dll"]
