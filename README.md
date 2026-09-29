# Microservicios con ASP.NET Core

Backend de una aplicación de ventas orientada a gestionar productos, clientes y órdenes de compra mediante APIs REST que pueda consumir una aplicación web.

La solución contempla tres microservicios: **Product**, para el catálogo y el stock; **Customer**, para los datos de los clientes; y **Order**, para las compras y su historial. Product y Customer están implementados y tienen bases de datos independientes. Order permite crear órdenes, confirmarlas descontando stock, cancelar las que todavía no están confirmadas y consultarlas por ID. Obtiene el cliente y los productos mediante HTTP, calcula los importes y conserva sus datos históricos. Si una comunicación falla durante la confirmación o cancelación, retoma el procesamiento automáticamente sin repetir el movimiento de stock.

Product permite crear, consultar, actualizar y dar de baja productos con nombre, descripción, precio y stock. Las bajas son lógicas: los datos se conservan en PostgreSQL y se excluyen de las consultas habituales. Customer permite gestionar clientes con nombre, email único, dirección y fecha de registro. También utiliza bajas lógicas.

## Tecnologías utilizadas

| Tecnología | Uso |
|---|---|
| C# y ASP.NET Core 8 | Desarrollo y ejecución de la API. |
| Clean Architecture y DDD | Separación en cuatro capas, agregados Product, Customer y Order; entidades OrderItem y value objects Money, Email, Address y CustomerSnapshot. |
| Entity Framework Core 8 y Npgsql | Acceso a PostgreSQL y herramientas para migraciones. |
| PostgreSQL 17 | Base de datos relacional. |
| FluentValidation | Validación de las solicitudes. |
| AutoMapper | Conversión de entidades a DTOs de respuesta. |
| Serilog | Registro de solicitudes y errores en consola y archivos. |
| Swagger / OpenAPI | Documentación y exploración de la API. |
| Docker | Compilación y ejecución en contenedores con .NET 8. |
| Docker Compose | Configuración de servicios, red, volúmenes y orden de inicio. |

## Docker y comunicación entre servicios

El archivo `compose.yaml`, ubicado en la raíz del proyecto, define los siguientes servicios:

| Servicio | Función | Dirección dentro de Docker | Acceso desde tu equipo |
|---|---|---|---|
| `product-api` | Ejecuta la API de Product. | `http://product-api:8080` | `http://localhost:5001` |
| `customer-api` | Ejecuta la API de Customer. | `http://customer-api:8080` | `http://localhost:5002` |
| `order-api` | Ejecuta la API de Order: creación, confirmación, cancelación y consulta por ID. | `http://order-api:8080` | `http://localhost:5003` |
| `postgres` | Aloja `product_db`, `customer_db` y `order_db`, con usuarios propios. | `postgres:5432` | `localhost:55432` |
| `customer-db-init` | Crea la base y el usuario de Customer si todavía no existen y finaliza. | Se conecta a `postgres:5432`. | No publica puertos. |
| `order-db-init` | Crea la base y el usuario de Order si todavía no existen y finaliza. | Se conecta a `postgres:5432`. | No publica puertos. |
| `product-tools` / `customer-tools` / `order-tools` | Ejecutan el SDK de .NET 8 para compilaciones y herramientas de EF Core. | No publican una API. | No publican puertos. |

Los servicios comparten una red de tipo `bridge`, declarada como `microservices-net`. Compose la crea con el nombre `microservicios_microservices-net`, combinando el nombre del proyecto con el de la red.

Dentro de esa red, los contenedores se encuentran por el nombre del servicio. Por ejemplo, las tres APIs se conectan a PostgreSQL usando `Host=postgres;Port=5432`. No necesitan conocer la dirección IP del contenedor.

Los puertos publicados permiten acceder desde tu equipo: `5001`, `5002` y `5003` se redirigen al puerto `8080` de Product, Customer y Order, respectivamente; `55432` se redirige al puerto `5432` de PostgreSQL. Están vinculados a `127.0.0.1`, por lo que solo se publican para acceso local. Dentro de un contenedor, `localhost` se refiere a ese mismo contenedor; para comunicarse con otro servicio se utiliza su nombre en la red.

Product comienza a ejecutarse cuando PostgreSQL supera su comprobación de disponibilidad. Customer y Order esperan, además, a que sus inicializadores (`customer-db-init` y `order-db-init`) terminen correctamente. Las tres APIs aplican las migraciones pendientes antes de atender solicitudes y tienen comprobaciones de conexión con sus bases. Los servicios de herramientas pertenecen al perfil opcional `tools` y solo se inician cuando se solicitan explícitamente.

Las APIs comparten la red y pueden direccionarse por sus nombres de servicio. Order consulta `http://customer-api:8080/api/customers/{id}` y `http://product-api:8080/api/products/{id}` para crear una orden. Las URLs se configuran mediante `Services__Customer__BaseUrl` y `Services__Product__BaseUrl` en Compose. Al confirmar o cancelar, Order también invoca las operaciones de stock de Product mediante HTTP, usando el ID de la orden para evitar movimientos duplicados. Cada solicitud tiene un tiempo máximo de espera de 10 segundos. Order accede únicamente a su propia base; los datos de otros servicios se obtienen por HTTP.

PostgreSQL conserva sus datos en el volumen `postgres-data`. Durante la primera inicialización se crean la base `product_db` y el usuario `product_user`, que utiliza la API. Customer utiliza `customer_db` y `customer_user`; Order utiliza `order_db` y `order_user`. Sus inicializadores funcionan también cuando el volumen ya contiene otras bases. Cada API utiliza las credenciales de su propia base. El volumen `nuget-packages` conserva la caché de dependencias del contenedor de herramientas. Los archivos de Serilog se guardan en `/app/logs`, dentro de los volúmenes `product-logs`, `customer-logs` y `order-logs`, y se conservan al recrear la API. Los logs tienen rotación diaria.

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
| `CUSTOMER_DB_PASSWORD` | Contraseña del usuario `customer_user`, utilizada por Customer. |
| `CUSTOMER_API_PORT` | Puerto local de Customer; por defecto, `5002`. |
| `ORDER_DB_PASSWORD` | Contraseña del usuario `order_user`, utilizada por Order. |
| `ORDER_API_PORT` | Puerto local de Order; por defecto, `5003`. |
| `POSTGRES_PORT` | Puerto local de PostgreSQL; por defecto, `55432`. |
| `PRODUCT_API_PORT` | Puerto local de la API; por defecto, `5001`. |
| `AUTOMAPPER_LICENSE_KEY` | Clave de AutoMapper, si disponés de una; se puede dejar vacía durante el desarrollo y las pruebas. |

Si ya tenías Product instalado, agregá `CUSTOMER_DB_PASSWORD` con una contraseña nueva y `CUSTOMER_API_PORT=5002` a tu `.env`, conservando las variables anteriores. Para agregar Order a una instalación existente, agregá también `ORDER_DB_PASSWORD` con una contraseña nueva y `ORDER_API_PORT=5003`. No hace falta eliminar el volumen de PostgreSQL.

El archivo `.env` contiene credenciales locales y está excluido del control de versiones. Si alguno de los puertos está ocupado, cambiá su valor antes de iniciar los servicios.

### 2. Levantar la aplicación

```sh
docker compose up --build -d --wait
```

Este comando descarga las imágenes necesarias, compila Product, Customer y Order y crea la red, los volúmenes y los contenedores. La configuración local aplica automáticamente las migraciones pendientes de las tres APIs. El comando espera a que las tres APIs y PostgreSQL estén disponibles.

### 3. Acceder a la aplicación

Con los puertos predeterminados:

- Swagger de Product: [http://localhost:5001/](http://localhost:5001/).
- Swagger de Customer: [http://localhost:5002/](http://localhost:5002/).
- Swagger de Order: [http://localhost:5003/](http://localhost:5003/).
- Estado del proceso de la API: [http://localhost:5001/health/live](http://localhost:5001/health/live).
- Disponibilidad de la conexión con PostgreSQL: [http://localhost:5001/health/ready](http://localhost:5001/health/ready).

Customer y Order también exponen `/health/live` y `/health/ready` en los puertos `5002` y `5003`. En los Swagger de Product y Customer podés ejecutar las operaciones de creación, consulta, actualización y baja lógica de productos o clientes. En Order podés crear una orden usando los IDs obtenidos de esas APIs y consultarla por ID. La creación deja la orden en estado `PendingStockConfirmation`, sin reservar ni descontar stock. Después, ejecutá `POST /api/orders/{id}/confirm` con su ID para confirmar la compra: Product descuenta la cantidad que sigue disponible y Order actualiza el total. Si el procesamiento devuelve `202`, consultá la orden por ID hasta conocer el resultado. `POST /api/orders/{id}/cancel` permite cancelar una orden todavía no confirmada y devolver el stock que pudiera haberse descontado durante un intento previo. Una orden `Confirmed` ya completó la compra.

Para conectarte desde un cliente de base de datos, usá el servidor `localhost`, puerto `55432`, base `product_db`, usuario `product_user` y la contraseña configurada en `PRODUCT_DB_PASSWORD`. Para Customer, usá el mismo servidor y puerto, base `customer_db`, usuario `customer_user` y contraseña `CUSTOMER_DB_PASSWORD`. Para Order, usá la base `order_db`, el usuario `order_user` y la contraseña `ORDER_DB_PASSWORD`.

Podés consultar el estado de los contenedores y sus registros con:

```sh
docker compose ps
docker compose logs --tail=100 product-api customer-api order-api postgres
```

### 4. Detener la aplicación

```sh
docker compose down
```

Este comando elimina los contenedores y la red, pero conserva los volúmenes. Para volver a iniciar la aplicación, ejecutá nuevamente el comando del paso 2.

La opción `docker compose down -v` también elimina los volúmenes y sus datos. La inicialización de Product se ejecuta cuando el volumen está vacío. Los inicializadores de Customer y Order comprueban si sus bases y usuarios ya existen y conservan los existentes. En todos los casos, modificar una contraseña en `.env` no cambia la contraseña de un usuario ya creado en la base.
