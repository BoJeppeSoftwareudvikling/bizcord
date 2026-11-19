using Microsoft.Data.Sqlite;

namespace UploadWorkflow;

public sealed class Database
{
    private readonly string connectionString;
    public Database(string path, string schema)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        using var connection = Open();
        using var init = connection.CreateCommand();
        init.CommandText = schema;
        init.ExecuteNonQuery();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
