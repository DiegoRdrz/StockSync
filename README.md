# StockSync

API de inventario con .NET 8 y PostgreSQL 15.

![Diagrama del proyecto](Assets/diagrama.png)

## Ejecutar con Docker (sin instalar .NET ni PostgreSQL)

Con Docker Desktop iniciado, desde la raíz `StockSync` (si estás en
`InventoryService`, ejecuta primero `cd ..`):

```bash
docker compose up -d --build
```

La API espera a que PostgreSQL esté listo, aplica las migraciones pendientes y
carga los datos de prueba antes de empezar a atender. Repetir el arranque no
duplica datos. Swagger: <http://localhost:5001/swagger>.

```bash
docker compose logs -f inventory-api  # Ver logs (Ctrl+C sale del visor).
docker compose stop                 # Detener los servicios conservando los datos.
docker compose down -v              # Borrar también la base para empezar de cero.
```

Después de cambiar código, reconstruye con `docker compose up -d --build inventory-api`;
las migraciones nuevas se aplican solas al arrancar. Para no cargar los datos de
prueba o cambiar la contraseña de PostgreSQL, copia `.env.example` como `.env` y
ajusta `CARGAR_DATOS_DEMO` o `POSTGRES_PASSWORD`. La imagen aplica las migraciones
al arrancar (`Inicializacion__AplicarMigraciones=true`); con varias réplicas,
desactívalo y ejecuta `docker compose run --rm inventory-api --migrate` como paso
previo. Para usar los ejemplos `curl` de este documento con Docker, usa el puerto
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
y 5 movimientos mediante Entity Framework y los métodos de creación del dominio.
Un único `SaveChangesAsync` guarda todo en una transacción. Solo se ejecuta en
`Development`, con `--seed` o al arrancar en Docker, después de las migraciones,
y siempre en el tenant por defecto.

Busca categorías activas por nombre normalizado, productos por SKU (priorizando
el activo si el SKU se reutilizó tras una baja) y stocks por producto/sucursal.
Repetirla no duplica registros ni reinicia saldos o reactiva productos. Los movimientos iniciales solo se agregan al crear su stock.
Los IDs se generan mediante las entidades; consulta los stocks por sucursal
para obtenerlos antes de probar entradas y salidas.

| SKU | Producto | Saldo inicial | Caso |
| --- | --- | --- | --- |
| `DEMO-HER-001` | Martillo demo | 7 | Entradas, salidas e historial |
| `DEMO-HER-002` | Taladro demo | 0 | Salida rechazada con HTTP 409 |
| `DEMO-PAP-001` | Cuaderno demo | 12 | Saldo inicial registrado como ajuste |

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

`GET /api/movimientos` devuelve los 10 movimientos más recientes de todos los
stocks, ordenados por fecha descendente y por ID para desempatar.
`GET /api/movimientos/pagina/{pagina}` devuelve las páginas siguientes, siempre
de 10 en 10. La respuesta incluye `items`, `pagina`, `tamanoPagina`, `total` y
`totalPaginas`. Cada registro muestra `productoNombre`, `productoSku`,
`productoId`, `stockId`, `sucursalId`, tipo, cantidad, saldos anterior/posterior y fecha.
Los nombres y SKU son los actuales; también se incluye el historial de productos inactivos.

Ejemplos con Docker:

```bash
curl 'http://localhost:5001/api/movimientos'
curl 'http://localhost:5001/api/movimientos/pagina/2'
```

Una página sin registros devuelve HTTP 200 con `items: []`; una página menor que 1
devuelve HTTP 400. Este listado no admite filtros; para consultar solo un stock usa
`GET /api/stock/{stockId}/movimientos?pagina=1&tamanoPagina=20`.

## Reglas de inventario

- **Bajas lógicas.** Categorías y productos se dan de baja (`Activo = false`). El
  nombre de categoría (sin distinguir mayúsculas ni espacios) y el SKU solo deben
  ser únicos entre los registros activos del tenant, así que pueden reutilizarse
  tras una baja. El historial de movimientos se vincula por `productoId`.
- **Historial completo.** Hay tres tipos de movimiento: `Entrada`, `Salida` y
  `Ajuste`. El saldo inicial al asignar stock y el ajuste manual
  (`PUT /api/stock/{id}`) se registran como `Ajuste`, así que el saldo siempre
  coincide con la suma de los movimientos.
- **Sin sobreventa.** Una salida mayor que el saldo se rechaza con 409. Las
  operaciones simultáneas sobre un mismo stock se aplican en orden (bloqueo de
  fila), sin conflictos espurios.
- **Integridad.** No se puede eliminar una categoría con productos activos, un
  producto con existencias, ni una asignación de stock con existencias o con
  historial. Un producto dado de baja no admite movimientos ni ajustes.
- **Stock bajo mínimo.** `GET /api/stock/bajo-minimo?sucursalId=` lista los stocks
  de productos activos con `cantidad < stockMinimo`, de mayor a menor faltante.
  Un mínimo de 0 nunca alerta.
- **Listados paginados.** Todos los listados aceptan `pagina` (desde 1) y
  `tamanoPagina` (1 a 100, por defecto 20), salvo `/api/movimientos`, que va de 10 en 10.

| Código | Cuándo |
| --- | --- |
| 400 | Datos inválidos, incluida una referencia del cuerpo que no existe o está dada de baja (`categoriaId`, `productoId`) |
| 404 | El recurso de la URL no existe o está dado de baja |
| 409 | Duplicados, stock insuficiente o una operación que el estado actual no permite |

## Multitenencia

Cada categoría, producto, stock y movimiento pertenece a un tenant, que se indica
en el encabezado `X-Tenant-Id` (GUID). Un tenant no ve ni puede referenciar los
datos de otro. El encabezado es obligatorio salvo en `Development`, donde, si se
omite, se usa el tenant por defecto `00000000-0000-0000-0000-000000000001`, que
también contiene los datos anteriores a la multitenencia y los datos de prueba.
El encabezado debe fijarlo el API Gateway tras autenticar al usuario; no hay que
aceptarlo tal cual desde clientes públicos.

```bash
curl http://localhost:5001/api/productos -H 'X-Tenant-Id: 3f2b8c1e-5d4a-4e6f-9a7b-1c2d3e4f5a6b'
```

## Pruebas

Desde `InventoryService`:

```bash
dotnet test StockSync.Inventory.sln

# Incluye integración real con PostgreSQL (usuario con permiso CREATE DATABASE).
STOCKSYNC_TEST_POSTGRES='Host=localhost;Database=postgres;Username=postgres;Password=stock1234' \
  dotnet test StockSync.Inventory.sln
```

Sin esa variable se omiten las pruebas de integración. Cada prueba de
integración crea y elimina su propia base, sin modificar la de la aplicación.
