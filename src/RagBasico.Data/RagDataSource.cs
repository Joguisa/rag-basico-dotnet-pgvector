using Npgsql;

namespace RagBasico.Data;

public static class RagDataSource
{
    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);// patron builder
        builder.UseVector(); // Viene del paquete Pgvector (con soporte Npgsql) y le dice a Npgsql "cuando veas una columna de tipo vector en Postgres, mapéala al tipo Vector de C# (o ReadOnlyMemory<float>)"
        return builder.Build(); // devuelve el NpgsqlDataSource ya configurado y listo para usar.
    }
}
