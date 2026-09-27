# Conceptos de búsqueda vectorial con pgvector

Este documento resume los conceptos centrales de búsqueda vectorial usando la extensión
`pgvector` de PostgreSQL, pensado como material de referencia para quien recién empieza a
trabajar con embeddings.

## Qué es un embedding

Un embedding es una representación numérica de un texto (o una imagen, o un audio) como un
vector de números de punto flotante, generalmente de varios cientos de dimensiones. Textos con
significado similar producen vectores cercanos entre sí en ese espacio de alta dimensión, aunque
las palabras usadas sean distintas. Por ejemplo, "el perro corre en el parque" y "un can juega en
el jardín" deberían generar embeddings cercanos, a pesar de no compartir casi ninguna palabra
literal.

## Distancia coseno vs distancia euclidiana

`pgvector` soporta tres operadores de distancia: `<->` (distancia euclidiana, L2), `<#>`
(producto interno negativo) y `<=>` (distancia coseno). La distancia coseno mide el ángulo entre
dos vectores, ignorando su magnitud: dos vectores que apuntan en la misma dirección tienen
distancia coseno cercana a 0, sin importar si uno es mucho más "largo" que el otro. Esto la hace
la opción más común para embeddings de texto, donde la dirección del vector captura el
significado y la magnitud suele ser un artefacto del modelo, no información útil.

La distancia euclidiana, en cambio, sí depende de la magnitud: dos vectores en la misma dirección
pero de distinta longitud tendrán una distancia euclidiana mayor a 0. Se usa cuando la magnitud del
vector aporta información relevante al problema, algo poco común en embeddings de texto genéricos.

## Búsqueda exacta vs índices aproximados (ANN)

Sin ningún índice sobre la columna `vector`, una consulta `ORDER BY embedding <=> @query LIMIT k`
hace un *exact nearest neighbor search*: calcula la distancia contra cada fila de la tabla y
ordena el resultado completo antes de recortar al top-k. Esto garantiza el resultado
matemáticamente correcto, pero el costo crece linealmente con la cantidad de filas.

Para tablas grandes (a partir de cientos de miles de filas, dependiendo del hardware), se usan
índices de *Approximate Nearest Neighbor* (ANN), que sacrifican una pequeña probabilidad de no
encontrar el vecino exacto a cambio de consultas mucho más rápidas. `pgvector` ofrece dos tipos de
índice ANN: `ivfflat`, basado en particionar el espacio en clusters mediante k-means, y `hnsw`,
basado en un grafo de navegación en capas ("Hierarchical Navigable Small World"). En términos
generales, `hnsw` ofrece mejor relación entre velocidad de consulta y precisión (recall), pero
tarda más en construirse y en actualizarse con cada `INSERT`.

## Dimensión del vector: por qué importa fijarla

La columna `vector(n)` de `pgvector` requiere declarar la dimensión `n` de antemano, y todo
vector insertado debe tener exactamente esa dimensión. Cada modelo de embeddings produce vectores
de una dimensión fija y específica de ese modelo: por ejemplo, un modelo puede producir vectores
de 768 dimensiones y otro de 1536. Mezclar vectores de dos modelos distintos en la misma columna,
o cambiar de modelo sin migrar el esquema, produce un error de inserción inmediato, ya que la
dimensión no coincide con la declarada en la tabla.
