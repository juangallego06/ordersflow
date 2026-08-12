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

**`Products` (OrdersDb):** cataloga los SKUs válidos del sistema —
únicamente el campo `Sku`. Existe separada de `Stock` (que vive en
`InventoryDb` y sí conoce cantidades) por una razón práctica: Orders API
necesita poder validar si un SKU existe en el momento de crear un pedido,
con un `400` inmediato si no existe, sin depender de una llamada síncrona a
Inventory Worker (lo que reintroduciría el acoplamiento que la separación
de bases de datos busca evitar). La solución fue duplicar intencionalmente
el catálogo — un dato de referencia casi estático — entre las dos bases,
mientras que el dato realmente mutable (la cantidad disponible) sigue
viviendo únicamente en `InventoryDb`. La limitación conocida: si se agrega
un producto nuevo, hay que sembrarlo en ambas bases; en un sistema real
esto se resolvería con un servicio de catálogo compartido o sincronización
por eventos.

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

## Arquitectura de Orders API

Orders API es el servicio más complejo del sistema — expone la API REST,
orquesta la creación de pedidos, y coordina tanto la publicación como el
consumo de eventos. Por eso se construyó siguiendo Clean Architecture
completa, separada en cuatro proyectos (`OrdersApi.Domain`,
`OrdersApi.Application`, `OrdersApi.Infrastructure`, `OrdersApi.Api`), cada
uno dependiendo solo de los que están más adentro. Inventory Worker, en
cambio, es un único flujo de trabajo (consumir un evento, decidir,
responder), así que se decidió construirlo como un solo proyecto con las
mismas responsabilidades organizadas en carpetas en vez de proyectos
separados — aplicar la misma rigurosidad a un servicio tan delgado hubiera
sido desproporcionado.

### Entidades con comportamiento, no bolsas de datos

`Order` y `Product` (en `Domain`) no son simples DTOs con getters y setters
públicos. Ambas tienen constructores privados, setters privados, y un
método estático `Create(...)` como único punto de entrada — así es
imposible construir un pedido con una cantidad fuera de rango o un nombre
de cliente vacío en cualquier parte del código, porque la validación vive
dentro de la propia entidad, no repartida en cada lugar que la use. `Order`
además expone `Confirm()` y `Reject()` en vez de dejar que cualquier capa
le cambie el estado directamente — esos métodos verifican que el pedido
esté en `Pending` antes de permitir la transición, protegiendo la máquina
de estados del negocio.

### Casos de uso como Commands y Queries (CQRS-lite)

La capa `Application` organiza cada caso de uso como una clase de Command o
Query independiente, despachada a través de un mediator (MediatR), en vez
de un único servicio con varios métodos. Es importante ser precisos con el
nombre: esto **no es CQRS puro** — no hay modelos ni almacenamiento
separados para lectura y escritura, ambos caminos usan las mismas tablas.
Es, más bien, el patrón mediator aplicado para que cada caso de uso sea una
clase pequeña con una sola responsabilidad, en vez de una interfaz
`IOrderService` con varios métodos no relacionados entre sí.
`CreateOrderCommand`/`CreateOrderCommandHandler` maneja la creación;
`GetOrdersQuery` y `GetOrderByIdQuery` manejan las dos formas de consulta.

### Repositorio + Unit of Work: un solo guardado atómico

Los repositorios (`IOrderRepository`, `IOutboxRepository`,
`IProductRepository`) solo agregan o consultan entidades — ninguno llama a
`SaveChanges` internamente. El guardado real ocurre una única vez, al final
de cada caso de uso, a través de `IUnitOfWork.SaveChangesAsync()`. La razón
es que `CreateOrderCommandHandler` necesita que el pedido nuevo y el
mensaje del Outbox se guarden **juntos o no se guarde ninguno** — como
ambos quedan rastreados por la misma instancia de `DbContext`, una sola
llamada a `SaveChangesAsync()` los envuelve automáticamente en una
transacción atómica, sin necesitar manejar transacciones explícitas a mano.

### El patrón Outbox en la práctica

Cuando `CreateOrderCommandHandler` crea un pedido, no publica el evento
`OrderCreated` directamente a RabbitMQ — lo escribe como una fila en
`OutboxMessages`, en la misma transacción que el pedido. Un servicio
independiente, `OutboxPublisherBackgroundService`, revisa esa tabla cada 5
segundos, publica lo que encuentra pendiente, y solo lo marca como
procesado si la publicación tuvo éxito. Si RabbitMQ está caído en ese
momento, la publicación simplemente se reintenta en la siguiente vuelta del
loop — el pedido ya quedó guardado de forma segura, y el evento se
recupera solo en cuanto el broker vuelve a estar disponible. Este mismo
servicio corre en Orders API para publicar `OrderCreated`, y se construirá
una versión equivalente en Inventory Worker para publicar
`StockReserved`/`StockRejected`.

### Topología de RabbitMQ

Se usan dos colas, una por cada dirección del flujo, no una por cada tipo
de evento específico: `order-created` (Orders API publica, Inventory
Worker consume) y `stock-events` (Inventory Worker publica ahí tanto
`StockReserved` como `StockRejected`). Dentro de `stock-events`, los dos
tipos de evento se distinguen usando la propiedad estándar de AMQP `Type`
del mensaje — no hace falta abrir el JSON para saber cuál es cuál, ni
mantener dos colas para algo que conceptualmente es "la respuesta a mi
pedido". Se usa el exchange por defecto de RabbitMQ, sin necesidad de
declarar exchanges personalizados, dado el tamaño del problema.

### Idempotencia: dos mecanismos, cada uno donde corresponde

El sistema resuelve la idempotencia de dos formas distintas, según el lado
del flujo:

- **Del lado de Inventory Worker** (procesar `OrderCreated`, la operación
  más sensible porque descuenta stock): una tabla dedicada
  `ProcessedEvents`, con el `EventId` como clave única, insertado en la
  misma transacción que el descuento. Necesaria porque ahí sí importa
  evitar cualquier posibilidad de doble descuento.
- **Del lado de Orders API** (procesar `StockReserved`/`StockRejected`): no
  se construyó una tabla adicional. La propia máquina de estados de
  `Order` ya lo resuelve — `Confirm()`/`Reject()` lanzan una excepción si
  el pedido ya no está en `Pending`, así que un evento duplicado
  simplemente se descarta de forma segura (se hace `ack` sin reprocesar).
  Es una idempotencia "gratis", derivada de una regla de negocio que ya
  existía por otra razón.

### Sobre de respuesta uniforme y manejo centralizado de errores

Todas las respuestas de la API se envuelven en `ApiResponse<T>` (`success`,
`code`, `message`, `error`, `data`), aplicado únicamente en la capa `Api`
— los DTOs de `Application` no saben nada de este formato. Esto se aparta
levemente del ejemplo literal del enunciado (que muestra el pedido
directamente en el cuerpo de la respuesta, no envuelto), aunque los
códigos HTTP reales (`201`, `400`, `404`, etc.) siguen siendo los que
importan para cualquier validación automática. Un
`ExceptionHandlingMiddleware` centraliza la traducción de excepciones a
respuestas HTTP: `ArgumentException` se convierte en `400`,
`InvalidOperationException` en `409`, cualquier otra excepción no
controlada en `500` — así ningún controller ni handler necesita bloques
`try/catch` propios.

## Requisitos y despliegue

> Esta sección se irá ampliando a medida que se agreguen los demás
> servicios (Inventory Worker, frontend).

**Requisitos previos:**

- Docker Desktop instalado y corriendo.
- .NET 9 SDK (para correr Orders API en desarrollo local).

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

**Corriendo Orders API en desarrollo local:**

Con la infraestructura levantada, Orders API se puede correr directamente
con `dotnet run`, sin necesidad de contenedor propio todavía. Las
credenciales de desarrollo se configuran con
[User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(nunca en archivos versionados):

```bash
cd orders-api/OrdersApi.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OrdersDb" "Server=localhost,1433;Database=OrdersDb;User Id=sa;Password=<tu contraseña>;TrustServerCertificate=True;"
dotnet user-secrets set "RabbitMQ:Host" "localhost"
dotnet user-secrets set "RabbitMQ:Port" "5672"
dotnet user-secrets set "RabbitMQ:User" "guest"
dotnet user-secrets set "RabbitMQ:Password" "guest"
dotnet run
```

El navegador abre automáticamente en `/swagger`, desde donde se pueden
probar los tres endpoints (`POST /orders`, `GET /orders`,
`GET /orders/{id}`).

**Verificación:** el servicio `db-init` debería terminar con código de
salida 0 (visible con `docker compose ps`). Conectándote a `localhost:1433`
con cualquier cliente de SQL Server (usuario `sa`, la contraseña de tu
`.env`), deberías ver `OrdersDb` con las tablas `Orders`, `Products` y
`OutboxMessages`, e `InventoryDb` con `Stock` (con 3 productos sembrados),
`ProcessedEvents` y `OutboxMessages`.

## Manejo de fallos

**Si Inventory Worker no responde o está caído:** el pedido queda visible
en el panel con estado `Pending` de forma indefinida — Orders API no
depende de que Inventory Worker esté disponible para aceptar y persistir
un pedido nuevo, precisamente porque los dos servicios están desacoplados
vía eventos. Mejora futura, no implementada: un timeout que marque el
pedido para revisión manual o dispare una alerta si lleva demasiado tiempo
en `Pending`.

**Si RabbitMQ está caído cuando un servicio intenta publicar un evento:**
este es el problema conocido como "dual-write problem" (ver sección de
Outbox arriba). Se resuelve con el patrón Outbox, implementado
simétricamente en ambos servicios: el evento queda guardado en la base de
datos en la misma transacción que el cambio de estado que lo originó, y un
`BackgroundService` reintenta publicarlo automáticamente cada 5 segundos
hasta que RabbitMQ vuelva a estar disponible. No se pierde ningún evento ni
se requiere intervención manual.

**Si el mismo evento se entrega más de una vez** (RabbitMQ garantiza
_at-least-once delivery_, no _exactly-once_): cada consumidor lo maneja de
forma idempotente, con un mecanismo distinto según el riesgo real de cada
lado (ver sección de Idempotencia arriba).

## Tests

_(pendiente — se documentará cuando se escriban. Candidatos claros dado el
diseño: `Order.Create()` y `Order.Confirm()`/`Reject()` son unitarios
puros, sin dependencia de base de datos, ideales para cubrir validación y
transiciones de estado.)_

## Qué haría distinto con más tiempo

- **Modelo de pedido multi-línea:** se evaluó un diseño con varias líneas
  por pedido (`Orders` + `OrdersDetail`) en vez del `Sku`+`Quantity` único
  que exige el contrato mínimo. Se descartó por proporcionalidad, pero el
  diseño para resolverlo correctamente ya está pensado: descuento de cada
  línea con `UPDATE` atómico condicional, y si alguna línea falla,
  revertir solo las líneas ya descontadas con un `SAVEPOINT`
  (`SAVE TRANSACTION`) dentro de la misma transacción — conservando el
  registro en `ProcessedEvents` (insertado antes del savepoint) para que
  la idempotencia no se pierda aunque el pedido termine rechazado.
- **Timeout/alerta para pedidos atascados en `Pending`** si Inventory
  Worker está caído por un período prolongado.
- **Sincronización de catálogo** entre `OrdersDb.Products` e
  `InventoryDb.Stock` vía eventos, en vez de seeds duplicados manualmente.
- **`CreatedAtAction`** en el endpoint de creación en vez de
  `StatusCode(201, ...)`, ahora que `GetOrderById` ya existe, para incluir
  el header `Location` estándar de REST.
