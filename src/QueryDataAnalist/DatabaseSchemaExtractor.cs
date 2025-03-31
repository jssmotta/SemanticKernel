using System.Data;
using Microsoft.Data.SqlClient;

namespace QueryDataAnalist; 
public class DatabaseSchemaExtractor
{
    private readonly string _connectionString;
    public string DatabaseName { get; private set; } = string.Empty;

    public DatabaseSchemaExtractor(string connectionString)
    {
        _connectionString = connectionString; 
        var builder = new SqlConnectionStringBuilder(connectionString);
        DatabaseName = builder.InitialCatalog;
    }

    public async Task<DatabaseSchema> ExtractSchemaAsync()
    {
        try
        {
            var schema = new DatabaseSchema
            {
                DatabaseName = this.DatabaseName,
                Tables = new List<TableSchema>()
            };

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
             
            DataTable tablesSchema = await Task.Run(() => connection.GetSchema("Tables"));

            foreach (DataRow row in tablesSchema.Rows)
            {
                string tableName = row["TABLE_NAME"].ToString() ?? "";
                string tableSchema = row["TABLE_SCHEMA"].ToString() ?? "dbo";

                var table = new TableSchema
                {
                    Name = tableName,
                    Schema = tableSchema,
                    Columns = new List<ColumnSchema>()
                };
                 
                using var command = new SqlCommand(
                    @"SELECT 
                    c.COLUMN_NAME, 
                    c.DATA_TYPE, 
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION, 
                    c.NUMERIC_SCALE,
                    c.IS_NULLABLE,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END AS IS_PRIMARY_KEY,
                    c.COLUMN_DEFAULT,
                    ep.value AS DESCRIPTION
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN (
                    SELECT ku.TABLE_CATALOG, ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS ku
                        ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                        AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                ) pk 
                    ON c.TABLE_CATALOG = pk.TABLE_CATALOG 
                    AND c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                    AND c.TABLE_NAME = pk.TABLE_NAME 
                    AND c.COLUMN_NAME = pk.COLUMN_NAME
                LEFT JOIN sys.tables t
                    ON t.name = c.TABLE_NAME
                    AND SCHEMA_NAME(t.schema_id) = c.TABLE_SCHEMA
                LEFT JOIN sys.columns sc
                    ON sc.object_id = t.object_id
                    AND sc.name = c.COLUMN_NAME
                LEFT JOIN sys.extended_properties ep
                    ON ep.major_id = t.object_id
                    AND ep.minor_id = sc.column_id
                    AND ep.name = 'MS_Description'
                WHERE c.TABLE_NAME = @TableName AND c.TABLE_SCHEMA = @TableSchema
                ORDER BY c.ORDINAL_POSITION",
                    connection);

                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@TableSchema", tableSchema);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var column = new ColumnSchema
                    {
                        Name = reader["COLUMN_NAME"].ToString() ?? "",
                        DataType = reader["DATA_TYPE"].ToString() ?? "",
                        IsNullable = reader["IS_NULLABLE"].ToString() == "YES",
                        IsPrimaryKey = reader["IS_PRIMARY_KEY"].ToString() == "YES",
                        DefaultValue = reader["COLUMN_DEFAULT"] == DBNull.Value ? null : reader["COLUMN_DEFAULT"].ToString(),
                        Description = reader["DESCRIPTION"] == DBNull.Value ? null : reader["DESCRIPTION"].ToString()
                    };

                    try
                    { 
                        if (!reader.IsDBNull(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH")))
                        {
                            column.Size = Convert.ToInt32(reader["CHARACTER_MAXIMUM_LENGTH"]);
                        }
                        else if (!reader.IsDBNull(reader.GetOrdinal("NUMERIC_PRECISION")))
                        {
                            column.Precision = Convert.ToInt32(reader["NUMERIC_PRECISION"]);
                            column.Scale = Convert.ToInt32(reader["NUMERIC_SCALE"]);
                        }
                    }
                    catch { }

                    table.Columns.Add(column);
                }

                schema.Tables.Add(table);
            }
             
            foreach (var table in schema.Tables)
            {
                using var command = new SqlCommand(
                    @"SELECT 
                    fk.name AS FK_NAME,
                    OBJECT_NAME(fk.parent_object_id) AS TABLE_NAME,
                    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS COLUMN_NAME,
                    OBJECT_NAME(fk.referenced_object_id) AS REFERENCED_TABLE_NAME,
                    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS REFERENCED_COLUMN_NAME
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fk.OBJECT_ID = fkc.constraint_object_id
                INNER JOIN sys.tables t ON t.OBJECT_ID = fk.parent_object_id
                WHERE SCHEMA_NAME(t.schema_id) = @TableSchema AND t.name = @TableName",
                    connection);

                command.Parameters.AddWithValue("@TableName", table.Name);
                command.Parameters.AddWithValue("@TableSchema", table.Schema);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string columnName = reader["COLUMN_NAME"] == DBNull.Value ? "" : reader["COLUMN_NAME"].ToString() ?? "";
                    string referencedTableName = reader["REFERENCED_TABLE_NAME"] == DBNull.Value ? "" : reader["REFERENCED_TABLE_NAME"].ToString() ?? "";
                    string referencedColumnName = reader["REFERENCED_COLUMN_NAME"] == DBNull.Value ? "" : reader["REFERENCED_COLUMN_NAME"].ToString() ?? "";
 
                    var column = table.Columns.FirstOrDefault(c => c.Name == columnName);
                    if (column != null)
                    {
                        column.IsForeignKey = true;
                        column.ForeignKeyTable = referencedTableName;
                        column.ForeignKeyColumn = referencedColumnName;
                    }
                }
            }

            return schema;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting schema: {ex.Message}");
            Console.ReadKey();
            throw new Exception($"Error extracting schema: {ex.Message}");
        }
    }


    public async Task<DataTable> ExecuteQueryAsync(string sqlQuery)
    {
        var results = new DataTable();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(sqlQuery, connection);
        using var adapter = new SqlDataAdapter(command);

        adapter.Fill(results);

        return results;
    }
}