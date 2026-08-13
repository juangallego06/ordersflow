# OrderFlow

Sistema de gestión de pedidos con reserva de inventario: tres servicios
independientes (**Orders API**, **Inventory Worker**, **frontend Angular**)
que se comunican de forma asíncrona vía RabbitMQ, sin compartir base de
datos.

## Arquitectura

**Dos bases de datos separadas** (`OrdersDb`, `InventoryDb`), una por
servicio, en el mismo contenedor de SQL Server por simplicidad de
despliegue pero lógicamente independientes. Ningún servicio consulta las
tablas del otro directamente — la única comunicación es vía eventos en
RabbitMQ (`order-created`: Orders→Inventory; `stock-events`:
Inventory→Orders, con `StockReserved`/`StockRejected` distinguidos por la
propiedad AMQP `Type`). Esto evita que un cambio de esquema en un
servicio rompa al otro sin contrato explícito.

**Patrón Outbox, simétrico en ambos servicios.** Ningún evento se publica
directo a RabbitMQ: se escribe en una tabla `OutboxMessages` dentro de la
misma transacción que el cambio de estado que lo originó, y un
`BackgroundService` lo publica cada 5s, reintentando solo si falla. Esto
resuelve el _dual-write problem_ (guardar en BD y publicar a un broker son
dos operaciones separadas; sin Outbox, una puede fallar mientras la otra
tiene éxito).

**Idempotencia con dos mecanismos, cada uno donde corresponde.** Inventory
Worker (el lado sensible, porque descuenta stock) usa una tabla
`ProcessedEvents` con `EventId` único, insertada en la misma transacción
que el descuento — evento duplicado, no se reprocesa. Orders API no
necesitó tabla adicional: `Order.Confirm()`/`Reject()` ya lanzan excepción
si el pedido no está en `Pending`, así que un evento duplicado se
descarta solo (idempotencia "gratis" de una regla de negocio que ya
existía).

**Reserva de stock, atómica sin locks.** `UPDATE Stock SET Available =
Available - @cantidad WHERE Sku = @sku AND Available >= @cantidad`,
condición y escritura en una sola sentencia — evita la condición de
carrera de un `SELECT` de verificación seguido de un `UPDATE` separado.

**Orders API** sigue Clean Architecture completa (`Domain`, `Application`,
`Infrastructure`, `Api`) porque orquesta REST + eventos + CQRS-lite (vía
MediatR, no CQRS puro — mismas tablas para lectura y escritura). `Order` y
`Product` son entidades con comportamiento (constructores privados,
`Create()` como único punto de entrada), no DTOs con setters públicos.
**Inventory Worker** es un solo flujo de trabajo (consumir evento → decidir
→ responder), así que es un único proyecto con carpetas en vez de 4
`.csproj` — la misma rigurosidad de Orders API hubiera sido
desproporcionada para su tamaño. Su `IUnitOfWork` expone
`Begin/Commit/RollbackAsync` (no solo `SaveChangesAsync` como en Orders
API) porque su consumidor necesita leer resultados intermedios
(¿duplicado? ¿alcanzó el stock?) antes de decidir qué publicar, todo
dentro de una única transacción.

**Frontend Angular 19**, standalone (sin NgModules), dos rutas
(`/crear`, `/pedidos`). Estado con signals (`OrdersService`) en vez de
NgRx — una lista y una bandera de carga no justifican esa ceremonia.
Reactive Forms para validación visible en el formulario. Lista
refrescada con polling cada 10s (permitido explícitamente por el
enunciado en vez de WebSockets/SignalR).

## Base de datos: SQL Server real, no en memoria

Se eligió SQL Server real corriendo en Docker (no SQLite ni una BD en
memoria) porque el enunciado pide explícitamente que "el seed funcione y
los estados persistan mientras el sistema corre" — una BD en memoria se
reinicia con cada restart del proceso, lo cual habría hecho trivial
simularlo pero no habría probado nada real sobre concurrencia (el `UPDATE`
atómico de `Stock` solo tiene sentido con un motor real gestionando
bloqueos de fila) ni sobre reinicio de servicios sin perder pedidos ya
creados. El esquema es **database-first**: `BD/init.sql` (idempotente,
`IF OBJECT_ID(...) IS NULL`) es la única fuente de verdad; EF Core solo
mapea contra él vía Fluent API, sin migraciones.

## Trade-offs asumidos

- **Duplicar el catálogo de SKUs** (`Products` en `OrdersDb`) en vez de
  que Orders API valide el SKU llamando a Inventory Worker de forma
  síncrona — evita reintroducir el acoplamiento que la separación de
  bases de datos busca evitar, a costa de tener que sembrar productos
  nuevos en dos lugares.
- **Pedido de una sola línea** (`Sku` + `Quantity`), no multi-línea —
  cumple el contrato mínimo del enunciado; el diseño para multi-línea
  con `SAVEPOINT` ya está pensado (ver "Qué haría distinto").
- **CORS abierto solo a `http://localhost:4200`** en desarrollo, vía
  config (no secreto, no necesita `user-secrets`).
- **`ApiResponse<T>`** envuelve todas las respuestas (`success`, `code`,
  `message`, `error`, `data`) — se aparta un poco del ejemplo literal del
  enunciado, pero los códigos HTTP reales (`201`/`400`/`404`) siguen
  siendo los que importan para cualquier validación automática.

## Cómo correr todo

**Requisitos:** Docker Desktop, .NET 9 SDK, Node.js + Angular CLI 19 (los
dos últimos solo si quieres correr algo fuera de Docker).

```bash
cp .env.example .env
# completa MSSQL_SA_PASSWORD en .env (mín. 8 caracteres, 3 de 4: mayúsculas/minúsculas/números/símbolos)

docker compose up
```

Esto levanta SQL Server (con el esquema y seed de `BD/init.sql` aplicados
automáticamente), RabbitMQ, Orders API, Inventory Worker y el frontend.
Abre **`http://localhost:4200`** — no hace falta ningún paso manual más.
RabbitMQ queda disponible en `http://localhost:15672` (`guest`/`guest`)
para ver las colas `order-created`/`stock-events` en vivo.

## SKUs disponibles tras el seed

`BD/init.sql` siembra automáticamente 3 productos con stock inicial en
`InventoryDb`, listos para probar sin pasos manuales:

| SKU      | Stock inicial |
| -------- | ------------- |
| `ABC-01` | 100           |
| `ABC-02` | 50            |
| `ABC-03` | 25            |

Para ver el pedido pasar a `Confirmed`, pide una cantidad menor o igual al
stock disponible. Para ver el caso `Rejected`, pide más — por ejemplo
26 unidades de `ABC-03`.

**Tests**, un comando por stack:

```bash
cd orders-api && dotnet test          # 7 tests (xUnit + Moq): validación, transición de estado, idempotencia
cd orders-front && npm install && ng test   # 2 tests (Jasmine/Karma): servicio y validación de formulario
```

## Qué haría distinto con más tiempo

- **Pedido multi-línea:** descuento por línea con `UPDATE` atómico
  condicional; si una línea falla, revertir solo las ya descontadas con
  un `SAVEPOINT` dentro de la misma transacción, conservando el registro
  de idempotencia insertado antes del savepoint.
- **Timeout/alerta para pedidos atascados en `Pending`** si Inventory
  Worker está caído por un período prolongado.
- **Sincronización de catálogo** entre `Products` y `Stock` vía eventos,
  en vez de seeds duplicados manualmente.
- **Consistencia de `DateTime.Kind`** al serializar fechas en Orders
  API — `createdAt` sale a veces con `Z` (UTC explícito) y a veces sin
  él. Se mitigó en el frontend (normalizando el string), pero lo
  correcto es forzar `Kind = Utc` de forma consistente en el backend.
- **`CreatedAtAction`** en vez de `StatusCode(201, ...)` en el endpoint
  de creación, para incluir el header `Location` estándar de REST.
