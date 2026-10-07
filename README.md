# StockSync

API de inventario con .NET 8 y PostgreSQL 15.

## Ejecutar con Docker (sin instalar .NET ni PostgreSQL)

Con Docker Desktop iniciado, desde la raíz `StockSync` (si estás en
`InventoryService`, ejecuta primero `cd ..`):

```bash
docker compose up -d inventory-db
docker compose build inventory-api

# Comprobar que PostgreSQL está listo; debe indicar "accepting connections".
docker compose exec inventory-db pg_isready -U postgres -d stocksync_inventory

# Aplicar migraciones y poblar datos usando EF dentro del contenedor.
docker compose run --rm inventory-api --migrate --seed

docker compose up -d inventory-api
```

Swagger: <http://localhost:5001/swagger>.
`--migrate` y `--seed` son comandos manuales de preparación en `Development`;
terminan al completar la operación. El arranque normal no modifica el esquema
ni agrega datos de prueba.

```bash
docker compose logs -f inventory-api  # Ver logs (Ctrl+C sale del visor).
docker compose stop                 # Detener los servicios conservando los datos.
```

Después de cambiar código, reconstruye y levanta la API con
`docker compose up -d --build inventory-api`. Si agregaste migraciones,
ejecuta también el comando de preparación anterior con la imagen nueva.
Para usar los ejemplos `curl` de este documento con Docker, usa el puerto
`5001` en lugar de `5041`.

## Ejecutar desde la terminal con .NET instalado

Necesitas un SDK con el runtime de .NET 8 y PostgreSQL en `localhost:5432`.
Desde la raíz del repositorio:

```bash
# Opcional: levantar solo PostgreSQL usando el compose existente.
docker compose up -d inventory-db

cd InventoryService
dotnet tool restore
dotnet restore
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project StockSync.Inventory.Infrastructure \
  --startup-project StockSync.Inventory.WebApi

# Poblar datos de prueba con Entity Framework y salir.
dotnet run --project StockSync.Inventory.WebApi --launch-profile http -- --seed

dotnet run --project StockSync.Inventory.WebApi --launch-profile http
```

Swagger: <http://localhost:5041/swagger>. Detener la API con `Ctrl+C`.
La API en el compose existente usa el puerto `5001`; `dotnet run` usa `5041`.
Las migraciones y la semilla se ejecutan manualmente, no al arrancar la API.

Si en macOS `dotnet --list-runtimes` solo muestra .NET 10 pero tienes .NET 8
en `/usr/local/share/dotnet`, selecciona esa instalación para esta terminal:

```bash
export DOTNET_ROOT=/usr/local/share/dotnet
export PATH="$DOTNET_ROOT:$PATH"
dotnet --list-runtimes
```

## Datos de prueba

`InventoryDbSeeder` agrega 2 categorías, 3 productos, 3 asignaciones de stock
y 4 movimientos mediante Entity Framework y los métodos de creación del dominio.
Un único `SaveChangesAsync` guarda todo en una transacción. Solo se ejecuta
con `--seed` en `Development`, después de aplicar las migraciones.

Busca categorías por nombre normalizado, productos por SKU y stocks por
producto/sucursal. Repetirla no duplica registros ni reinicia saldos o reactiva
productos. Los movimientos iniciales solo se agregan al crear su stock.
Los IDs se generan mediante las entidades; consulta los stocks por sucursal
para obtenerlos antes de probar entradas y salidas.

| SKU | Producto | Saldo inicial | Caso |
| --- | --- | --- | --- |
| `DEMO-HER-001` | Martillo demo | 7 | Entradas, salidas e historial |
| `DEMO-HER-002` | Taladro demo | 0 | Salida rechazada con HTTP 409 |
| `DEMO-PAP-001` | Cuaderno demo | 12 | Ajuste y eliminación sin historial |

Los dos primeros stocks pertenecen a la sucursal
`11111111-1111-1111-1111-111111111111`; el tercero, a
`22222222-2222-2222-2222-222222222222`. Las sucursales son referencias
externas, no tablas de este servicio.

```bash
curl http://localhost:5041/api/productos
curl http://localhost:5041/api/categorias
curl http://localhost:5041/api/stock/sucursal/11111111-1111-1111-1111-111111111111

# Reemplazar con el ID del stock que quieras probar.
stock_id='ID_DEL_STOCK'
curl "http://localhost:5041/api/stock/$stock_id/movimientos"
curl -i -X POST "http://localhost:5041/api/stock/$stock_id/salidas" \
  -H 'Content-Type: application/json' -d '{"cantidad":1}'
```

También puedes usar `StockSync.Inventory.WebApi/StockSync.Inventory.WebApi.http`
para crear datos propios y recorrer el flujo de movimientos.

## Consultar movimientos sin conocer IDs

`GET /api/movimientos` lista todos los movimientos, ordenados por fecha descendente
y por ID para desempatar. La respuesta incluye `items`, `pagina`, `tamanoPagina`,
`total` y `totalPaginas`. Cada registro muestra `productoNombre`, `productoSku`,
`productoId`, `stockId`, `sucursalId`, tipo, cantidad, saldos anterior/posterior y fecha.
Los nombres y SKU son los actuales; también se incluye el historial de productos inactivos.

Todos los filtros son opcionales y se pueden combinar:

| Parámetro | Uso |
| --- | --- |
| `busqueda` | Coincidencia parcial por nombre o SKU, sin distinguir mayúsculas |
| `tipo` | `Entrada` o `Salida` |
| `desde`, `hasta` | Límites inclusivos de fecha/hora ISO 8601 con zona, por ejemplo `2026-10-06T00:00:00Z` |
| `sucursalId` | Limitar a una sucursal si conoces su ID |
| `pagina`, `tamanoPagina` | Por defecto 1 y 20; máximo 100 registros por página |

Ejemplos con Docker:

```bash
curl 'http://localhost:5001/api/movimientos'
curl 'http://localhost:5001/api/movimientos?busqueda=martillo&tipo=Salida'
curl 'http://localhost:5001/api/movimientos?busqueda=DEMO-HER&pagina=1&tamanoPagina=10'
curl 'http://localhost:5001/api/movimientos?desde=2026-10-06T00:00:00Z&hasta=2026-10-06T23:59:59.999999Z'
```

Sin coincidencias devuelve HTTP 200 con `items: []`; filtros inválidos devuelven
HTTP 400. Para consultar solo un stock sigue disponible
`GET /api/stock/{stockId}/movimientos`.

## Pruebas

Desde `InventoryService`:

```bash
dotnet test StockSync.Inventory.sln

# Incluye integración real con PostgreSQL (usuario con permiso CREATE DATABASE).
STOCKSYNC_TEST_POSTGRES='Host=localhost;Database=postgres;Username=postgres;Password=mysecretpassword' \
  dotnet test StockSync.Inventory.sln
```

Sin esa variable se omiten las pruebas de integración. Cada prueba de
integración crea y elimina su propia base, sin modificar la de la aplicación.
