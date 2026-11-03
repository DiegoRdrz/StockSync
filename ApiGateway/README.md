# API Gateway de StockSync

Proyecto TypeScript + NestJS con Fastify, dentro del mismo repositorio que `InventoryService`. Separa las políticas del gateway de los adaptadores HTTP y de la composición de dependencias.

```text
Cliente / Swagger → gateway :9090 → InventoryService → PostgreSQL
```

## Arranque completo con Docker

Desde la raíz **StockSync**, con Docker Desktop iniciado:

```sh
docker compose up -d --build
```

Abre http://localhost:9090/ (redirige a Swagger). Compose ejecuta la API internamente en `http://inventory-api:8080`, aplica sus migraciones y carga los datos demo mediante la configuración que ya traía el repo. La API también conserva su acceso directo de desarrollo en http://localhost:5001.

El gateway puede iniciar antes de que termine la inicialización del inventario; espera a que `docker compose logs inventory-api` indique que está escuchando. `/health` comprueba el proceso del gateway, no la base de datos.

## Gateway local con Node.js

Requiere Node.js 24 o superior (seleccionable con `nvm install && nvm use`). Arranca primero Inventario en Docker o con `dotnet run` siguiendo el README raíz.

```sh
cd ApiGateway
cp -n .env.example .env
npm ci
npm run dev
```

`INVENTORY_API_URL=http://localhost:5001` corresponde al inventario en Docker. Si ejecutas .NET directamente, cambia el valor a `http://localhost:5041`. Reinicia `npm run dev` cuando cambies `.env`. Para ejecutar compilado: `npm run build` y `npm start`.

No ejecutes al mismo tiempo el gateway local y el de Docker en el mismo puerto. Puedes detener solo el contenedor con `docker compose stop api-gateway` desde la raíz.

## Rutas públicas

| Ruta | Función |
| --- | --- |
| `/` | Redirigir a `/swagger` |
| `/health` | Estado del proceso |
| `/api/inventario/productos` y subrutas | Productos |
| `/api/inventario/categorias` y subrutas | Categorías |
| `/api/inventario/stock` y subrutas | Stocks, entradas, salidas, mínimos e historial |
| `/api/inventario/movimientos` y subrutas | Listado paginado de movimientos |
| `/swagger` y subrutas | Swagger UI y `/swagger/v1/swagger.json` |

Las rutas públicas `/api/inventario/...` se reescriben a `/api/...` antes de llamar al servicio. Por ejemplo, `http://localhost:9090/api/inventario/productos` llega a `http://inventory-api:8080/api/productos`. Las rutas antiguas sin `/inventario` ya no están publicadas por el gateway.

Las rutas de inventario reenvían métodos, cuerpo, query y estado HTTP; el backend decide cuáles admite. No se duplican reglas de stock ni se agregan conexiones a la base desde el gateway.

Ejemplos:

```sh
curl http://localhost:9090/api/inventario/productos
curl http://localhost:9090/api/inventario/categorias
curl http://localhost:9090/api/inventario/movimientos
curl http://localhost:9090/api/inventario/stock/sucursal/11111111-1111-1111-1111-111111111111
```

## Swagger y mismo origen

El gateway adapta **solo el documento OpenAPI JSON** para publicar las rutas `/api/inventario/...` y `servers: [{"url":"/"}]` y retirar servidores internos definidos en operaciones. Así **Try it out** ejecuta contra el puerto del gateway. Se elimina la cabecera de tenant de la documentación pública porque la establece el gateway. No es necesario `AllowAnyOrigin` para este flujo.

Se conserva el resto de la documentación, con límite de 2 MiB; un documento inválido devuelve 502. Las respuestas de inventario se transmiten por streaming sin transformarlas.

## Configuración

| Variable local | Predeterminado |
| --- | --- |
| `HOST` | `127.0.0.1` |
| `PORT` | `9090` |
| `INVENTORY_API_URL` | `http://localhost:5001` |
| `UPSTREAM_TIMEOUT_MS` | `10000` |
| `TENANT_ID` | `00000000-0000-0000-0000-000000000001` |
| `INVENTORY_CA_FILE` | Sin configurar; archivo PEM opcional para un backend HTTPS |

En Docker, configura `GATEWAY_PORT` y `GATEWAY_TENANT_ID` en el `.env` de la raíz, junto a los valores de PostgreSQL existentes. Los cambios en `.env` requieren recrear los contenedores con `docker compose up -d`.

### Alcance del tenant

Esta versión es **de desarrollo con un tenant fijo**. El gateway reemplaza siempre `X-Tenant-Id` por `TENANT_ID`; el consumidor no puede seleccionar otro tenant enviando esa cabecera. El valor predeterminado corresponde a los datos demo del repo.

Todavía no hay autenticación de usuarios. Para un despliegue multitenant real hay que autenticar al usuario, resolver su tenant autorizado y proteger el acceso directo al backend. Esta configuración fija no implementa ese flujo.

### HTTPS opcional

Docker y el perfil HTTP de Inventario no requieren certificado. Si eliges el perfil HTTPS (`https://localhost:7079`), Node valida el certificado. Puedes exportar el certificado público local y configurar `INVENTORY_CA_FILE`:

```sh
mkdir -p certs
dotnet dev-certs https --export-path certs/inventario.pem --format PEM
```

`certs/` y `.env` están excluidos de Git y del contexto Docker. No se desactiva TLS. Usa Node.js 24 o superior para ejecutar el gateway.

## Arquitectura y responsabilidades

```text
src/
  main.ts                       Punto de entrada del proceso
  composition/                  Arranque Nest/Fastify y registro de dependencias
  application/
    routing/                    Rutas públicas y su correspondencia con Inventario
    documentation/              Política de publicación de OpenAPI
    health/                     Estado del proceso
  infrastructure/
    configuration/              Variables de entorno, certificados y opciones inyectadas
    proxy/                      Transporte, streaming, cabeceras, timeouts y errores upstream
  presentation/http/
    controllers/                Endpoints propios: raíz y health
    filters/                    Traducción de excepciones Nest a Problem Details
```

La dirección de dependencias es `presentation → application` e `infrastructure → application`. `composition` conecta las implementaciones concretas. `application` no importa NestJS, Fastify, variables de entorno ni adaptadores. La adaptación del documento OpenAPI es una función independiente; el adaptador del proxy se encarga de leer el stream, limitar su tamaño y enviar la respuesta HTTP.

Esta es una aplicación proporcionada de Clean Architecture para un gateway: no se agregan entidades, repositorios ni una capa `domain` vacía. Las reglas de productos, stock y persistencia pertenecen a `InventoryService`. Si aparecen casos de uso que necesiten servicios externos, sus contratos deben definirse en `application` y sus implementaciones en `infrastructure`.

Los endpoints del proxy usan hooks y manejo de errores del plugin Fastify; los guards y filtros de controladores Nest no se aplican automáticamente a esas rutas.

### Compilación y producción

- `npm run build` compila únicamente `src/` a `dist/`, usando `tsconfig.json`.
- `npm start` ejecuta `dist/main.js`.
- `npm run typecheck` revisa los tipos del servidor, sin generar archivos.
- Docker copia únicamente el servidor compilado y sus dependencias de producción.

## Manejo de errores

Conexión fallida: 502. Timeout antes de cabeceras: 504; después de comenzar el cuerpo se interrumpe la conexión. El timeout limita conexión, cabeceras e inactividad del cuerpo, no la duración total de una transmisión continua. No hay reintentos automáticos.

Se genera `X-Request-Id` y se conservan los estados 400/404/409 del inventario. No se registran cuerpos ni Authorization. Las cabeceras de forwarding del cliente se reemplazan.

```sh
npm run typecheck     # Tipado estricto
```

Referencias: [NestJS providers](https://docs.nestjs.com/providers) y [@fastify/http-proxy](https://github.com/fastify/fastify-http-proxy).

## Diagnóstico del error 500 de PostgreSQL

Si Inventario registra `28P01: password authentication failed`, ajusta `POSTGRES_PASSWORD` en el `.env` raíz a la contraseña **actual** del volumen y ejecuta `docker compose up -d --no-build inventory-api`. Cambiar la variable no cambia la contraseña de una base ya inicializada. No es necesario borrar el volumen.

La credencial de este equipo se recuperó del historial y se guardó únicamente en `.env`, excluido de Git. El servicio C# y el gateway no incluyen esa credencial en las respuestas.
