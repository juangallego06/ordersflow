# OrderFlow — Prueba técnica Q10

Sistema de gestión de pedidos con reserva de inventario, construido como un
conjunto de servicios independientes que se comunican de forma asíncrona.
Este README se irá completando a medida que avanza la implementación.

## Modelo de datos y decisiones de arquitectura

### Separación de bases de datos

El sistema usa dos bases de datos separadas — `OrdersDb` e `InventoryDb` —
alojadas en el mismo contenedor de SQL Server por simplicidad de despliegue,
pero lógicamente independientes. Cada servicio es dueño exclusivo de sus
propios datos: Orders API nunca consulta directamente las tablas de
Inventory Worker, ni viceversa. La única forma en que un servicio se entera
de lo que pasó en el otro es a través de eventos publicados en RabbitMQ.

Esta separación es intencional y refleja un principio central de sistemas
distribuidos: acoplar dos servicios a través de una base de datos compartida
elimina buena parte del beneficio de tenerlos separados en primer lugar,
porque un cambio de esquema en un lado puede romper al otro sin que medie
ningún contrato explícito.

### Esquema y tablas

**`Orders` (OrdersDb):** cada fila representa un pedido con un único `Sku`
y una única `Quantity`, siguiendo el contrato mínimo definido en el
enunciado de la prueba. Se evaluó un modelo con múltiples líneas por pedido
(`Orders` + `OrdersDetail`), pero se descartó por ahora — el detalle de esa
decisión y cómo se resolvería está en la sección "Qué haría distinto con
más tiempo".

**`OutboxMessages` (en ambas bases):** implementa el patrón Outbox. Cada
vez que un servicio necesita publicar un evento como resultado de un cambio
de estado, el evento se escribe en esta tabla dentro de la misma transacción
que el cambio de estado, en vez de publicarse directamente a RabbitMQ en el
momento. Un proceso en segundo plano (`BackgroundService`) lee
periódicamente los mensajes pendientes y los publica, marcándolos como
procesados solo si el envío fue exitoso.

Esto resuelve un problema clásico de sistemas distribuidos conocido como
"dual-write problem": si guardas un cambio en la base de datos y publicas un
evento como dos operaciones separadas, existe una ventana en la que la
primera tiene éxito y la segunda falla (por ejemplo, si el broker está
caído en ese instante), dejando el sistema en un estado inconsistente del
que nadie se entera. Con Outbox, el cambio de estado y la intención de
publicar quedan atómicamente juntos; la publicación en sí se recupera sola
en cuanto el broker vuelve a estar disponible. Se implementó en ambos
servicios (Orders API e Inventory Worker) porque el mismo riesgo existe en
las dos direcciones del flujo, no solo en la que el enunciado menciona
explícitamente.

**`Stock` (InventoryDb):** guarda la cantidad disponible por SKU. El
descuento de inventario se hace con una sola sentencia `UPDATE` con la
condición de stock suficiente en el propio `WHERE`, en vez de un `SELECT`
de verificación seguido de un `UPDATE`. Esto es importante bajo
concurrencia: si dos pedidos del mismo SKU llegan casi al mismo tiempo, un
`SELECT` previo no garantiza que el dato siga vigente para cuando se
ejecuta el `UPDATE`, lo que podría dejar el stock en negativo. Al poner la
condición directamente en el `UPDATE`, la validación y la escritura ocurren
como una sola operación atómica a nivel de motor de base de datos.

**`ProcessedEvents` (InventoryDb):** sostiene la idempotencia del
consumidor. Antes de procesar un evento `OrderCreated`, se intenta insertar
su `EventId` en esta tabla dentro de la misma transacción que el descuento
de stock; si el insert falla porque el `EventId` ya existe (clave única),
se trata como un evento duplicado y no se vuelve a descontar. Esto es
necesario porque RabbitMQ (como la mayoría de brokers de mensajes) garantiza
entrega _at-least-once_: el mismo mensaje puede llegar más de una vez.

### Inicialización automática (`BD/entrypoint.sh`)

El esquema de ambas bases de datos se define en `BD/init.sql` y se aplica
automáticamente al levantar el sistema, sin pasos manuales. A diferencia de
la imagen oficial de Postgres, la imagen de SQL Server no ejecuta scripts
de inicialización por sí sola, así que se agregó un servicio adicional
(`db-init` en el `docker-compose.yml`) cuyo único trabajo es esperar a que
SQL Server esté listo para aceptar conexiones y luego ejecutar `init.sql`
con `sqlcmd`.

El script `init.sql` está escrito para ser idempotente (usa
`IF NOT EXISTS` / `IF OBJECT_ID IS NULL` en cada creación y en el seed), de
forma que `docker compose up` se pueda ejecutar cualquier cantidad de veces
sin fallar ni duplicar datos.

El esquema se maneja como **database-first**: `init.sql` es la única fuente
de verdad de la estructura de las tablas. Entity Framework Core se usa
únicamente para consultar y mapear esas tablas (vía Fluent API), pero nunca
para generar ni aplicar migraciones — así se evita tener dos fuentes de
verdad del esquema compitiendo entre sí.

## Requisitos y despliegue

> Esta sección se irá ampliando a medida que se agreguen los demás
> servicios (Orders API, Inventory Worker, frontend). Por ahora cubre solo
> la infraestructura base.

**Requisitos previos:**

- Docker Desktop instalado y corriendo.

**Configuración:**

1. Copia `.env.example` a `.env`.
2. Completa `MSSQL_SA_PASSWORD` con una contraseña que cumpla los
   requisitos de complejidad de SQL Server (mínimo 8 caracteres, al menos
   3 de estas 4 categorías: mayúsculas, minúsculas, números, símbolos).

**Levantar la infraestructura actual:**

```bash
docker compose up sqlserver db-init rabbitmq
```

Esto levanta SQL Server, aplica el esquema y el seed de forma automática, y
levanta RabbitMQ con su panel de administración disponible en
`http://localhost:15672` (usuario/clave por defecto: `guest`/`guest`).

**Verificación:** el servicio `db-init` debería terminar con código de
salida 0 (visible con `docker compose ps`). Conectándote a `localhost:1433`
con cualquier cliente de SQL Server (usuario `sa`, la contraseña de tu
`.env`), deberías ver `OrdersDb` con la tabla `Orders` y `OutboxMessages`, e
`InventoryDb` con `Stock` (con 3 productos sembrados), `ProcessedEvents` y
`OutboxMessages`.

## Manejo de fallos

_(pendiente — se documentará junto con la implementación de Orders API e
Inventory Worker)_

## Tests

_(pendiente)_

## Qué haría distinto con más tiempo

_(pendiente — incluirá el diseño de modelo multi-línea con `SAVEPOINT` que
se consideró y no se implementó)_
