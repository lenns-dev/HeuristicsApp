# HeuristicsApp

Sistema de optimización de procesos de **batching** y **routing** en almacenes.

El proyecto aborda la preparación de pedidos (*order picking*): agrupar pedidos en lotes
(*Order Batching Problem*, OBP) y calcular la ruta de recogida de cada lote (*Picker Routing Problem*, PRP).
Permite cargar instancias de benchmark, combinar distintas heurísticas de batching y routing, repartir
los lotes entre varios operarios (*pickers*) y ver el resultado animado sobre un mapa del almacén.

## Requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Workload/plantillas de **.NET Aspire 13** (el AppHost usa `Aspire.AppHost.Sdk` 13.x)
- Docker (opcional, solo para el despliegue con docker-compose)

## Puesta en marcha

### Local

```bash
dotnet run --project HeuristicsApp.AppHost
```

Se abre el dashboard de Aspire, desde el que se accede a la web (`web`) y a la API (`api`).
En desarrollo la API lee las instancias directamente de `HeuristicsApp.Core/WarehouseInstances`.

### Docker

```bash
dotnet run --project HeuristicsApp.AppHost -- --operation publish --step prepare-compose --output-path ./artifacts
docker compose -f ./artifacts/docker-compose.yaml --env-file ./artifacts/.env.Development up -d
```

| Servicio | URL |
|---|---|
| Web | http://localhost:8081 |
| Dashboard de Aspire | http://localhost:8080 |

> El token del repositorio de instancias se pasa en `DATASET_TOKEN` dentro del fichero `.env`.
> Sin un volumen para la carpeta de instancias, lo sincronizado se pierde al recrear el contenedor.

La guía completa está en la pestaña **Docker** de la propia aplicación.

## Arquitectura

```
Web (Blazor Server)  ──HTTP/JSON──▶  ApiService (Minimal API)  ──▶  Core (heurísticas)
```

| Proyecto | Responsabilidad | Referencia a |
|---|---|---|
| `HeuristicsApp.AppHost` | Orquestación Aspire (local y docker-compose) | ApiService, Web |
| `HeuristicsApp.ApiService` | Endpoints, carga de instancias, simulación temporal, sincronización del dataset | Core, Contracts, ServiceDefaults |
| `HeuristicsApp.Web` | Interfaz, mapa SVG y reproducción de la simulación | Contracts, ServiceDefaults |
| `HeuristicsApp.Core` | Modelo de dominio, heurísticas y planificación (librería pura) | — |
| `HeuristicsApp.Contracts` | DTOs compartidos entre API y Web | — |
| `HeuristicsApp.ServiceDefaults` | Telemetría, health checks, service discovery y resiliencia | — |

**Regla de diseño:** Core no conoce Contracts ni ASP.NET. La traducción dominio → DTO se hace en la API
(`Mapping/PlanMapping.cs`) y la Web nunca ve tipos de Core.

### Flujo de un plan

1. La web envía `POST /plan` con el almacén, las claves de batching y routing y el nº de pickers.
2. La API valida las claves y carga la instancia desde disco.
3. `OperationPlanner` crea los lotes (el batching recibe el routing elegido) y enruta cada uno.
4. `PickerScheduler` asigna cada lote al picker que antes queda libre.
5. `RouteSimulator` genera la línea temporal de cada recorrido, incluidas las paradas de recogida.
6. `PlanMapping` construye el `SimulationResult`; la web lo dibuja y `TrackPlayback` interpola las posiciones al reproducir.

## Heurísticas disponibles

| Tipo | Clave | Clase |
|---|---|---|
| Batching | `random` | `RandomBatching` |
| Batching | `basicConstructive` | `BasicConstructiveBatching` |
| Batching | `cwSavingConstructive` | `CWSavingConstructiveBatching` (Clarke & Wright savings) |
| Routing | `s-shape` | `SShapeRouting` |
| Routing | `largest-gap` | `LargestGapRouting` |
| Routing | `combined` | `CombinedRouting` |
| Routing | `ratliff-rosenthal` | `RatliffRosenthalRouting` — *en desarrollo, no registrada* |

## Métricas

Cada plan devuelve (`PlanMetrics`): distancia total recorrida, duración estimada (*makespan*, fin del
último picker), nº de lotes, nº de pickers, tiempo de cómputo de la heurística, y los parámetros de
tiempo usados (velocidad de desplazamiento y tiempo de recogida por artículo).

## Añadir un algoritmo

La API es la única fuente de verdad: **un algoritmo está disponible si y solo si está registrado en
`HeuristicsApp.ApiService/Program.cs`**. La web construye sus selectores a partir de `GET /strategies`,
así que no hay que tocarla.

1. **Implementa la interfaz** en `HeuristicsApp.Core/Heuristics/Routing/` o `Batching/`:
   - `IRoutingStrategy.Route(batch, layout, ct)` → `PickRoute`
   - `IBatchingStrategy.CreateBatches(problem, routing, ct)` → lista de `Batch`
   - Ambas exponen `Key` (único y estable) y `DisplayName` (texto de la UI).
2. **Regístralo**:
   ```csharp
   builder.Services.AddSingleton<IRoutingStrategy, MiRouting>();
   // con parámetros de constructor:
   builder.Services.AddSingleton<IBatchingStrategy>(new RandomBatching(seed: 7));
   ```
   Para retirarlo basta con comentar el registro.
3. **Pruébalo** con las peticiones de `HeuristicsApp.ApiService/HeuristicsApp.ApiService.http`
   y revisa la animación en la web.

Reglas que el resto de la aplicación da por supuestas:

- **Routing:** la ruta empieza y acaba en `layout.Depot`, usa `layout.Distance` y pasa por la posición
  de cada artículo (si no, ese artículo no tendrá hora de recogida en la animación).
- **Batching:** cada pedido aparece en exactamente un lote y cada lote respeta `problem.Capacity`.
  Si necesitas el coste de un lote, usa el `routing` que recibe `CreateBatches`, no uno fijo inyectado.
- Las `Key` deben ser únicas: una clave repetida hace fallar el arranque de la API.
- Llama a `ct.ThrowIfCancellationRequested()` en bucles demasiado largos (>30s).

## API

| Endpoint | Descripción |
|---|---|
| `GET /strategies` | Heurísticas de batching y routing habilitadas |
| `GET /warehouse-folders` | Carpetas de instancias y nº de ficheros |
| `GET /warehouses?folder=X` | Instancias de una carpeta |
| `POST /plan` | Ejecuta un plan → `SimulationResult` (400 clave desconocida, 404 almacén inexistente, 422 fichero ilegible) |
| `GET /dataset/status` | Estado de la sincronización de instancias |
| `POST /dataset/sync?dryRun=` | Comprueba (`dryRun=true`) o sincroniza las instancias (409 si ya hay una en curso, 503 sin configurar) |

Ejemplo:

```http
POST /plan
Content-Type: application/json

{
  "warehouseId": "<id de GET /warehouses?folder=W5A>",
  "batchingKey": "basicConstructive",
  "routingKey": "s-shape",
  "options": { "pickers": 2 }
}
```

> Los endpoints no tienen autenticación: están pensados para uso local.

## Instancias del almacén

Las instancias son ficheros JSON de benchmark en `HeuristicsApp.Core/WarehouseInstances/`, agrupados
por carpeta (`W1-W4`, `W5A`, …). Ocupan más de 1 GB, así que la API solo indexa las rutas y lee cada
fichero al usarlo.

### Sincronización con un repositorio de datos

La carpeta local puede mantenerse idéntica a una carpeta de un repositorio GitHub (normalmente privado)
desde la pestaña **Instancias** de la web o con `POST /dataset/sync`. La sincronización fija un commit,
compara por hash de blob git sin descargar nada, descarga solo lo nuevo o modificado (uno a uno o como
zip si son muchos), borra lo que sobra y reindexa sin reiniciar la API.

Configuración en `HeuristicsApp.ApiService/appsettings.json`, sección `WarehouseDataset`:

| Clave | Significado |
|---|---|
| `Owner`, `Repo` | Repositorio GitHub (obligatorios) |
| `Branch` | Rama; vacía = rama por defecto |
| `Path` | Carpeta remota a replicar (por defecto `dataset`) |
| `InstancesPath` | Carpeta local relativa a la API; si se omite, la copia de la salida de compilación |
| `IndividualDownloadLimit` | Umbral de ficheros para pasar de descarga individual a zip (200) |
| `Token` | Token con lectura de contenidos — **nunca en appsettings** |

```bash
# Local
dotnet user-secrets set "WarehouseDataset:Token" "<token>" --project HeuristicsApp.ApiService

# Docker / producción
WarehouseDataset__Token=<token>
```

Limitaciones: no soporta ficheros en Git LFS, y si la carpeta remota no existe o está vacía no se toca
nada en local.

## Documentación

La aplicación incluye una guía de usuario (página de inicio) y una documentación técnica detallada
en `/docs` (`HeuristicsApp.Web/Components/Pages/Docs.razor`).
