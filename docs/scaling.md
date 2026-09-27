# Cómo escalar esto

Este proyecto es deliberadamente un MVP local, sin framework, para mostrar la mecánica de RAG
"desde los fierros" (ver `PLAN.md`). Esta nota junta, en un solo lugar, qué cambiaría para
llevarlo a un entorno real con más de un usuario y más de un puñado de documentos - sin
implementar nada de esto ahora, porque sería sobre-ingeniería para el objetivo pedagógico del
proyecto.

## Búsqueda vectorial

**Índice ANN (`hnsw` vs `ivfflat`).** Hoy `chunks` no tiene índice sobre `embedding` - cada
`SearchAsync` hace un *exact search* (recorre toda la tabla, calcula la distancia coseno contra
cada fila). Es correcto y suficiente mientras el dataset sea pequeño (ver `docs/decisions.md`,
Día 1), pero no escala linealmente: con cientos de miles de chunks, cada consulta se vuelve una
sentencia O(n).

- **`ivfflat`**: parte el espacio vectorial en `lists` clusters (k-means) y busca solo dentro de
  los clusters más cercanos al vector de consulta. Más barato de construir y de mantener, pero
  necesita que la tabla ya tenga datos representativos al momento de crear el índice (el número
  de `lists` se elige en función del tamaño esperado de la tabla), y su recall cae si la
  distribución de los datos cambia mucho después.
- **`hnsw`**: un grafo de mundo pequeño navegable. Mejor recall/latencia en general y no depende
  de conocer de antemano el tamaño de la tabla, a costa de más tiempo y memoria para construir el
  índice y de un `INSERT` más lento (mantener el grafo actualizado tiene costo).

Para este caso (reingesta ocasional, lecturas frecuentes) `hnsw` es la elección por defecto más
razonable si el dataset creciera; `ivfflat` tendría sentido si la carga de escritura fuera alta y
un recall ligeramente menor fuera aceptable.

**Batching de embeddings - ya implementado, no es solo una nota teórica.**
`DocumentIngestionService.IngestAsync` (Día 2) parte los chunks en lotes de `BatchSize` (20 en
`appsettings.json`) antes de llamar a `POST /api/embed` de Ollama, en vez de un request HTTP por
chunk. El trade-off del tamaño de lote:

- Un lote muy chico (ej. 1) multiplica la cantidad de round-trips HTTP - cada uno con su propia
  latencia de red, aunque sea local.
- Un lote muy grande arriesga un payload gigante en un solo request (más memoria, más tiempo de
  respuesta, más impacto si falla a mitad de camino: se pierde el lote completo, no un chunk).

20 es un valor razonable para documentos de portafolio (decenas de chunks); con documentos reales
de cientos de páginas convendría medir el tamaño de payload real contra el límite práctico de
Ollama antes de subir el número a ciegas.

## Cache

Hoy cada reingesta de un documento recalcula el embedding de **todos** sus chunks, incluso los
que no cambiaron de contenido (el `ON CONFLICT` de `ChunkRepository.InsertManyAsync` los
sobrescribe igual). Un cache de embeddings - clave: hash del `content` del chunk, valor: el
vector ya calculado - evitaría llamadas repetidas a Ollama para contenido idéntico entre
reingestas. Un segundo nivel posible es cachear la respuesta completa de `/ask` para preguntas
idénticas, aunque ahí hay que decidir un TTL corto: el contexto recuperado puede cambiar con cada
reingesta.

## Separar el vector store detrás de una interfaz - ya parcialmente resuelto

La regla de dependencias del proyecto (`RagBasico.Core` sin paquetes externos, ver el `CLAUDE.md`
de este proyecto) ya deja las dos costuras que se necesitarían para este cambio:

- `IEmbeddingClient` (`Core.Embeddings`) es hoy implementado por `OllamaEmbeddingClient`
  (`Data.Embeddings`). Swapear Ollama por un proveedor hosted (OpenAI, Voyage, Cohere) es escribir
  una clase nueva que implemente esa misma interfaz - nada en `Core` ni en los endpoints de `Api`
  necesita cambiar.
- `IChunkRepository` (`Core.Persistence`) es hoy implementado por `ChunkRepository` (Npgsql +
  pgvector). Swapear Postgres por un vector store dedicado (Pinecone, Weaviate, Qdrant) es lo
  mismo: una implementación nueva de la interfaz, registrada en `Program.cs`.

Lo que falta para que este punto esté "resuelto" de verdad no es la interfaz (ya existe) sino
tener una segunda implementación real de al menos una de las dos, para probar que el contrato
efectivamente alcanza sin fugas de detalles de Ollama/Postgres hacia `Core`.

## Hybrid search (full-text + vector)

La búsqueda actual es 100% semántica (similitud coseno sobre `embedding`). Eso falla en consultas
que dependen de coincidencia léxica exacta (nombres propios, códigos, siglas) que un embedding
puede no capturar bien. Postgres permite indexar `content` con `tsvector`/GIN para full-text
search clásico, y combinar ambos rankings (ej. Reciprocal Rank Fusion) antes de devolver el
top-k. No requiere una base de datos adicional - coexiste en la misma tabla `chunks`.

## Reranking

`SearchAsync` ordena directamente por distancia coseno cruda. Un paso de reranking agregaría un
segundo modelo (típicamente un cross-encoder, más caro por candidato pero más preciso que la
distancia vectorial) que reordena solo el top-k ya recuperado (ej. top 20 -> reordenar -> devolver
top 5). Es barato porque corre sobre pocos candidatos, no sobre todo el dataset.

## Lo que ya quedó anotado como deuda deliberada en días anteriores

Estos puntos están documentados con su razón en `docs/decisions.md` (Día 2 y Día 3) y se agrupan
acá porque son, en conjunto, el resto de "cómo escalar esto":

- **Ingesta de archivos vía `data/` local (`POST /ingest/file`)** asume filesystem compartido.
  En producción: object storage (S3/Azure Blob/GCS) o un `multipart/form-data` real, con
  validación de que la ruta resuelta quede dentro de la carpeta base (no solo `Path.GetFileName`).
- **Ingesta síncrona.** Un documento grande bloquea el `POST` mientras se calculan todos sus
  embeddings. En producción: encolar el trabajo (queue + worker) y responder `202 Accepted` con
  un id de job.
- **Sin versionado de documentos.** Cada reingesta recalcula todo; no hay hash/versión por
  documento para saber qué cambió realmente.
- **Sin middleware de manejo de errores centralizado.** Las excepciones no capturadas hoy
  llegan al manejo por defecto de ASP.NET Core. Un middleware propio permitiría respuestas de
  error consistentes (mismo formato JSON) en toda la API.
- **Credenciales de Postgres en claro en `docker-compose.yml`.** Aceptable solo porque el uso es
  estrictamente local (ver Día 1).

## Fuera de alcance incluso en esta nota

Autenticación/autorización de la API, observabilidad (logging estructurado, tracing), multi-tenant,
y rate limiting no se tratan acá porque no son específicos de RAG - son preocupaciones de
cualquier API en producción, y agregarlas ahora sería sobre-ingeniería para lo que este proyecto
busca demostrar.
