# Microservicios con ASP.NET Core

Backend de una aplicación de ventas orientada a gestionar productos, clientes y órdenes de compra mediante APIs REST que pueda consumir una aplicación web.

La solución contempla tres microservicios: **Product**, para el catálogo y el stock; **Customer**, para los datos de los clientes; y **Order**, para las compras y su historial. Cada servicio tendrá su propia base de datos y se comunicará con los demás mediante HTTP.

Actualmente se incluye la estructura inicial de Product, su conexión con PostgreSQL y Swagger. Las operaciones de negocio todavía no están implementadas.

## Tecnologías utilizadas

| Tecnología | Uso |
|---|---|
| C# y ASP.NET Core 8 | Desarrollo y ejecución de la API. |
| Clean Architecture | Separación en Domain, Application, Infrastructure y API. |
| Entity Framework Core 8 y Npgsql | Acceso a PostgreSQL y herramientas para migraciones. |
| PostgreSQL 17 | Base de datos relacional. |
| Swagger / OpenAPI | Documentación y exploración de la API. |
| Docker | Compilación y ejecución en contenedores con .NET 8. |
| Docker Compose | Configuración de servicios, red, volúmenes y orden de inicio. |

## Docker y comunicación entre servicios

El archivo `compose.yaml`, ubicado en la raíz del proyecto, define los siguientes servicios:

| Servicio | Función | Dirección dentro de Docker | Acceso desde tu equipo |
|---|---|---|---|
| `product-api` | Ejecuta la API de Product. | `http://product-api:8080` | `http://localhost:5001` |
| `postgres` | Aloja la base `product_db`. | `postgres:5432` | `localhost:55432` |
| `product-tools` | Ejecuta el SDK de .NET 8 para compilaciones y herramientas de EF Core. | No publica una API. | No publica puertos. |

Los servicios comparten una red de tipo `bridge`, declarada como `microservices-net`. Compose la crea con el nombre `microservicios_microservices-net`, combinando el nombre del proyecto con el de la red.

Dentro de esa red, los contenedores se encuentran por el nombre del servicio. Por ejemplo, Product se conecta a PostgreSQL usando `Host=postgres;Port=5432`. No necesita conocer la dirección IP del contenedor.

Los puertos publicados permiten acceder desde tu equipo: `5001` se redirige al puerto `8080` de la API y `55432` al puerto `5432` de PostgreSQL. Están vinculados a `127.0.0.1`, por lo que solo se publican para acceso local. Dentro de un contenedor, `localhost` se refiere a ese mismo contenedor; para comunicarse con otro servicio se utiliza su nombre en la red.

Product comienza a ejecutarse cuando PostgreSQL supera su comprobación de disponibilidad. La API también tiene una comprobación que verifica su conexión con la base. `product-tools` pertenece al perfil opcional `tools` y solo se inicia cuando se solicita explícitamente.

PostgreSQL conserva sus datos en el volumen `postgres-data`. Durante la primera inicialización se crean la base `product_db` y el usuario `product_user`, que utiliza la API. El volumen `nuget-packages` conserva la caché de dependencias del contenedor de herramientas.

## Instalación y ejecución

### Requisitos

- Docker Desktop iniciado, o Docker Engine con Docker Compose instalado.
- Acceso a Internet para descargar las imágenes y dependencias en la primera compilación.

El SDK y el runtime de .NET 8 se ejecutan dentro de Docker; no hace falta instalarlos en tu equipo.

### 1. Preparar la configuración

Descargá o cloná el proyecto y abrí una terminal en la carpeta que contiene `compose.yaml`.

Creá el archivo `.env` a partir del ejemplo si todavía no existe:

```sh
test -f .env || cp .env.example .env
```

Si acabás de copiar el ejemplo, reemplazá las contraseñas por valores distintos, aleatorios y alfanuméricos:

| Variable | Configuración |
|---|---|
| `POSTGRES_PASSWORD` | Contraseña del administrador de PostgreSQL. |
| `PRODUCT_DB_PASSWORD` | Contraseña del usuario `product_user`, utilizada por la API. |
| `POSTGRES_PORT` | Puerto local de PostgreSQL; por defecto, `55432`. |
| `PRODUCT_API_PORT` | Puerto local de la API; por defecto, `5001`. |

El archivo `.env` contiene credenciales locales y está excluido del control de versiones. Si alguno de los puertos está ocupado, cambiá su valor antes de iniciar los servicios.

### 2. Levantar la aplicación

```sh
docker compose up --build -d --wait
```

Este comando descarga las imágenes necesarias, compila Product y crea la red, los volúmenes y los contenedores. Espera a que la API y PostgreSQL estén disponibles.

### 3. Acceder a la aplicación

Con los puertos predeterminados:

- Swagger: [http://localhost:5001/](http://localhost:5001/).
- Estado del proceso de la API: [http://localhost:5001/health/live](http://localhost:5001/health/live).
- Disponibilidad de la conexión con PostgreSQL: [http://localhost:5001/health/ready](http://localhost:5001/health/ready).

Swagger todavía no muestra operaciones de productos porque el CRUD está pendiente de implementación.

Para conectarte desde un cliente de base de datos, usá el servidor `localhost`, puerto `55432`, base `product_db`, usuario `product_user` y la contraseña configurada en `PRODUCT_DB_PASSWORD`.

Podés consultar el estado de los contenedores y sus registros con:

```sh
docker compose ps
docker compose logs --tail=100 product-api postgres
```

### 4. Detener la aplicación

```sh
docker compose down
```

Este comando elimina los contenedores y la red, pero conserva los volúmenes. Para volver a iniciar la aplicación, ejecutá nuevamente el comando del paso 2.

La opción `docker compose down -v` también elimina los volúmenes y sus datos. La inicialización de PostgreSQL solo se ejecuta cuando el volumen está vacío: modificar una contraseña en `.env` no cambia la contraseña de un usuario ya creado en la base.
