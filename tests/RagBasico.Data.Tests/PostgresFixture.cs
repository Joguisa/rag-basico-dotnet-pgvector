using Npgsql;
using Testcontainers.PostgreSql;

namespace RagBasico.Data.Tests;

// Se crea UNA vez por clase de test (IClassFixture) - levantar un contenedor Docker
// es caro, no queremos hacerlo por cada [Fact]/[Theory] individual.
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // misma imagen y credenciales que docker-compose.yml, para no depender de
        // una versión de pgvector distinta a la que corre en desarrollo.
        //
        // WithBindMount monta db/init tal como docker-compose.yml monta esa misma carpeta
        // en /docker-entrypoint-initdb.d: Postgres corre 001_schema.sql el mismo durante
        // su inicializacion, ANTES de aceptar conexiones de la app. Ejecutar el schema
        // "a mano" despues de StartAsync (via una conexion de este mismo DataSource) falla:
        // Npgsql resuelve y cachea el catalogo de tipos (incluido 'vector') en la primera
        // conexion que abre, y si esa conexion ocurre antes del CREATE EXTENSION, el cache
        // queda sin el tipo 'vector' para el resto de la vida del contenedor.
        _container = new PostgreSqlBuilder("pgvector/pgvector:0.8.6-pg16")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithDatabase("ragbasico")
            .WithBindMount(FindSchemaDirPath(), "/docker-entrypoint-initdb.d")
            .Build();

        await _container.StartAsync();

        DataSource = RagDataSource.Create(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        // DataSource/_container pueden seguir en null! si InitializeAsync tiro antes de
        // asignarlos (ej. Docker caido, StartAsync fallo) - sin este chequeo, xUnit igual
        // llama a DisposeAsync y una NullReferenceException tapa el error real del arranque.
        if (DataSource is not null)
            await DataSource.DisposeAsync();

        if (_container is not null)
            await _container.DisposeAsync();
    }

    // busca la carpeta db/init subiendo desde la carpeta de salida del build
    // (bin/Debug/net10.0/...) hasta encontrar la raiz del repo. Evita depender de
    // cual sea el directorio de trabajo actual cuando se corre "dotnet test".
    private static string FindSchemaDirPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "db", "init");
            if (File.Exists(Path.Combine(candidate, "001_schema.sql")))
                return candidate;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"No se encontro db/init subiendo desde {AppContext.BaseDirectory}");
    }
}
