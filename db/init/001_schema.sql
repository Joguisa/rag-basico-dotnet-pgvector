CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS chunks (
    id           SERIAL PRIMARY KEY,
    source       TEXT NOT NULL,
    chunk_index  INT NOT NULL,
    content      TEXT NOT NULL,
    embedding    vector(768) NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ALTER TABLE chunks ADD COLUMN test_col TEXT; --// prueba para validar que el script de init no se re-ejecuta aunque se cambie el .sql, mientras el volumen de datos ya exista
