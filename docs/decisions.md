# Decisiones sobre RAG básico con .NET + pgvector

## Día 1

**Ollama nativo en Windows, no containerizado.** Correr Ollama dentro de Docker Desktop en
Windows pierde acceso a GPU salvo que se configure explícitamente WSL2 + nvidia container
toolkit. Para un demo local esa complejidad no aporta nada: Ollama corre nativo
(`http://localhost:11434`) y solo Postgres va en `docker-compose.yml`. Si en el futuro se
containeriza también la API .NET, el endpoint pasa a `http://host.docker.internal:11434`.

**Imagen Postgres pinneada a `pgvector/pgvector:0.8.6-pg16`.** Se verificó el tag vigente en
Docker Hub (no `latest`) al momento de escribir esto. Se eligió `pg16` sobre `pg17` por ser la
versión con más tiempo de adopción/estabilidad para un proyecto de portafolio, sin necesidad
real de features específicas de pg17.

**Sin índice ANN (hnsw/ivfflat) en el Día 1.** El dataset de demo es pequeño (unos pocos
documentos de `data/`), así que exact search (sin índice) es suficiente y más simple de
razonar mientras se valida el pipeline completo. El trade-off entre `hnsw` e `ivfflat` se
documenta como parte de "cómo escalar esto" en el README final (Día 5), no como requisito
del MVP: agregar el índice ahora sería sobre-ingeniería para el objetivo pedagógico de este
proyecto (ver regla operativa de "sin sobre-ingeniería").

**Esquema `chunks` sin tabla `documents` separada.** Una sola tabla con columna `source`
(nombre del archivo de origen) alcanza para trazabilidad y citas en la respuesta generada.
Separar en `documents`/`chunks` normalizado sería la abstracción "enterprise" correcta en
otro contexto, pero acá no aporta al objetivo de ver el RAG "desde los fierros" con el mínimo
de piezas.

**Credenciales de Postgres en claro en `docker-compose.yml` (`postgres`/`postgres`).**
Uso exclusivamente local/dev, sin exposición a internet. Es aceptable para este demo. No usar
este patrón si el proyecto se despliega en un entorno compartido.

**Paquete NuGet `Pgvector` en 0.3.2 (sin actualizar desde mayo/2025).** Se verificó que
sigue siendo el paquete oficial de `ankane` para integrar el tipo `vector` con Npgsql, y su
dependencia mínima (`Npgsql >= 8.0.5`) es compatible con `Npgsql 10.0.3` (el que se usa acá,
con target `net10.0` explícito). No está deprecado, pero es una dependencia con poco
mantenimiento activo a vigilar. Está anotado como riesgo, no como bloqueante.

## Día 2

**Carga de archivos de `data/` por nombre, un archivo por request (`POST /ingest/file`).**
Se eligió esta opción sobre "toda la carpeta de una" o un script/comando separado por ser la
más simple de razonar y probar, consistente con el resto del MVP. Queda anotado explícitamente
que este mecanismo **no es el que se usaría en producción**, por cuatro motivos:

1. *Origen de los archivos*: resolver `fileName` contra una carpeta local del servidor asume
   filesystem compartido/persistente, algo que no existe en despliegues reales (contenedores
   efímeros, múltiples instancias). En producción los documentos vendrían de object storage
   (S3/Azure Blob/GCS), un CMS, o un upload real (`multipart/form-data`).
2. *Path traversal*: aceptar un `fileName` de request y concatenarlo a una ruta base sin
   sanitizar es una superficie de ataque clásica (`fileName` tipo `"../../..."`). Para este
   proyecto se acepta el riesgo porque el uso es exclusivamente local; en producción requeriría
   validar que la ruta resuelta quede dentro de la carpeta base antes de leer el archivo.
3. *Ingesta síncrona y lenta*: la ingesta de un documento real (varios MB, muchos chunks)
   puede tardar minutos por las llamadas a embeddings. Bloquear un `POST` con eso no escala; en
   producción se encolaría (queue + worker) y el endpoint respondería `202 Accepted` con un id
   de job en vez de esperar el resultado.
4. *Sin versionado de documentos*: cada reingesta recalcula embeddings de todos los chunks del
   documento (con el fix de `DeleteOrphanedChunksAsync` para no dejar huérfanos). Un sistema real
   trackearía un hash/versión por documento para reprocesar solo lo que cambió.

Ninguno de los cuatro se resuelve en este proyecto — quedan documentados como parte de
"cómo escalar esto" (Día 5), siguiendo la misma regla de no sobre-ingeniería del MVP.

## Día 3

**`IChunkRepository.SearchAsync` recibe `float[]`, no `Pgvector.Vector`.** Mismo criterio que
`StoredChunk.Embedding` en el Día 2: `Core` no depende del paquete `Pgvector` (detalle de
infraestructura). La conversión a `Vector` ocurre únicamente dentro de `ChunkRepository`
(Data), manteniendo la interfaz de `Core` libre de dependencias externas.

**`TopK` vive en `RetrievalOptions` (Api), no en `Core`.** Mismo patrón que `IngestionOptions`:
`Core` no depende de `Microsoft.Extensions.Options`, así que la configuración se resuelve en
`Program.cs` (Api) y se pasa a `RetrievalService` como `int` plano por constructor.

**`/ask` devuelve la distancia coseno cruda (`RetrievedChunk.Distance`), sin convertirla a un
"score" de similitud.** Es más simple para el MVP y alcanza para validar manualmente la calidad
del retrieval; la conversión (`1 - distance`) queda como mejora cosmética, no como necesidad
funcional.

**Sin transacción en `ChunkRepository.SearchAsync`.** Las transacciones agrupan escrituras que
deben aplicarse todas o ninguna; un `SELECT` no modifica nada, así que no hay nada que revertir.
Envolver una lectura en una transacción es sobrecosto sin beneficio.

**Sin `try/catch` local en `SearchAsync`.** El método no hace ninguna acción de recuperación
propia (no hay rollback, no hay traducción de excepciones), así que las excepciones no
capturadas se propagan solas por la cadena de `await` hasta el manejo de errores por defecto de
ASP.NET Core. Un `catch { throw; }` sin lógica adicional es un anti-patrón (catch-and-rethrow)
que no cambia el comportamiento del programa. Un manejo de errores centralizado (middleware)
queda anotado como mejora futura (Día 5), no como requisito del MVP.

**`/ask` retorna solo los chunks recuperados, sin generación.** Cumple lo planeado: separar el
retrieval de la generación permite validar la calidad de la búsqueda semántica de forma aislada
antes de sumar el LLM en el Día 4.

## Día 4

**Modelo de generación fijado a `llama3.1:8b`.** Mismo criterio que `nomic-embed-text:latest`
en el Día 1: tag exacto, sin `latest` ni rangos abiertos, para que el comportamiento del pipeline
no cambie por una actualización silenciosa del modelo.

**Prompt de dos mensajes: instrucciones en `system`, contexto + pregunta en `user`.** Es la
convención estándar de las APIs de chat (Ollama incluida): el rol `system` lleva reglas de
comportamiento fijas para toda la llamada (responder solo con el contexto dado, avisar si no
alcanza, citar fuentes con el formato `[source:chunk_index]`), y el rol `user` lleva la entrada
concreta de ese turno (el contexto recuperado más la pregunta). Se descartó meter el contexto
también en `system` por ser redundante: duplicar el mismo texto en los dos mensajes no le agrega
información al modelo, solo tokens de más.

**`OllamaChatClient` recibe `HttpClient` + `string generationModel` por constructor, no
`IOptions<OllamaOptions>`.** Sigue el mismo patrón de configuración ya documentado para
`RetrievalService`/`DocumentIngestionService`: los servicios de `Core`/`Data` reciben valores
planos, y el binding a `IOptions<T>` queda solo en `Program.cs`. Como este cliente además
necesita un `HttpClient` con `BaseAddress` configurado, `Program.cs` usa `AddHttpClient("Ollama",
...)` para registrar y configurar el `HttpClient` con nombre, y `IHttpClientFactory.CreateClient
("Ollama")` dentro de una factory `AddScoped<IChatClient>(sp => ...)` para construir el cliente a
mano — en vez de `AddHttpClient<IChatClient, OllamaChatClient>(...)`, que le delega a DI la
construcción completa de la clase y por lo tanto no puede resolver un `string` suelto que no está
registrado como servicio. Se corrigió también `OllamaEmbeddingClient` (Día 2) al mismo patrón,
que hasta ahora inyectaba `IOptions<OllamaOptions>` directamente y rompía esta regla.

**`POST /ask` cambia de contrato: de `{ question, chunks }` a `{ question, answer, citations }`.**
`answer` es el texto generado por el LLM; `citations` es la misma lista estructurada de
`RetrievedChunk` (`source`, `chunkIndex`, `content`, `distance`) que ya se usaba como contexto —
no se parsean las citas del texto generado. Motivo: confiar en la lista de chunks que
efectivamente se le mandó al modelo es más confiable que confiar en que el modelo haya escrito
bien el formato de cita dentro de la respuesta.
