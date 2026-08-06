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
