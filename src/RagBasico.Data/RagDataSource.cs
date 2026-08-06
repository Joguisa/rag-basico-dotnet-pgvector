using Npgsql;

namespace RagBasico.Data;

public static class RagDataSource
{
    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();
        return builder.Build();
    }
}
