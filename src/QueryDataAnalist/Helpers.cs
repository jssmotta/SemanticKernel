using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QueryDataAnalist;

public class Helpers
{
    public static async Task<List<string>> GetAvailableDatabases(string connectionString)
    {
        var databases = new List<string>();

        using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();
         
        string query = @"
                SELECT name 
                FROM sys.databases 
                WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb') 
                ORDER BY name";

        using (var command = new SqlCommand(query, connection))
        {
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }
            }
        }


        return databases;
    }


    public static int ShowDatabaseSelectionMenu(List<string> databases)
    {
        int selectedIndex = 0;
        ConsoleKey keyPressed;

        do
        { 
            Console.CursorVisible = false;
             
            int currentLine = Console.CursorTop;
             
            WriteColorLine($"> {databases[selectedIndex]}", ConsoleColors.Success);
             
            keyPressed = Console.ReadKey(true).Key;
             
            if (keyPressed == ConsoleKey.UpArrow && selectedIndex > 0)
            {
                selectedIndex--;
            }
            else if (keyPressed == ConsoleKey.DownArrow && selectedIndex < databases.Count - 1)
            {
                selectedIndex++;
            }
             
            Console.SetCursorPosition(0, currentLine);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLine);

        } while (keyPressed != ConsoleKey.Enter);

        Console.CursorVisible = true;
        return selectedIndex;
    }

    
    public static string ClearSql(string queryTSql)
    {
        var match = Regex.Match(queryTSql, @"```sql\s*(.*?)\s*```", RegexOptions.Singleline);
        if (!match.Success) return "";
        string tsql = match.Groups[1].Value;
        return tsql;
    }

    public static string ClearResult(string result)
    {
        return result.Replace("###", "").Replace("**", "");
    }

    public static class ConsoleColors
    {
        public static ConsoleColor Title = ConsoleColor.Cyan;
        public static ConsoleColor Success = ConsoleColor.Green;
        public static ConsoleColor Error = ConsoleColor.Red;
        public static ConsoleColor Prompt = ConsoleColor.Yellow;
        public static ConsoleColor Result = ConsoleColor.White;
        public static ConsoleColor Query = ConsoleColor.Magenta;
    }

    public static void WriteColorLine(string text, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = originalColor;
    }

    public static void DisplayQueryResults(DataTable results)
    {
        if (results.Rows.Count == 0)
        {
            WriteColorLine("Query executed successfully, but no rows were returned.", ConsoleColors.Success);
            return;
        }

        var columnWidths = new Dictionary<string, int>();
        foreach (DataColumn column in results.Columns)
        {
            columnWidths[column.ColumnName] = column.ColumnName.Length;
        }

        foreach (DataRow row in results.Rows)
        {
            foreach (DataColumn column in results.Columns)
            {
                string value = row[column] == DBNull.Value ? "NULL" : row[column].ToString() ?? "";
                int displayWidth = Math.Min(value.Length, 40);
                columnWidths[column.ColumnName] = Math.Max(columnWidths[column.ColumnName], displayWidth);
            }
        }

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        foreach (DataColumn column in results.Columns)
        {
            Console.Write($"{column.ColumnName.PadRight(columnWidths[column.ColumnName] + 2)}");
        }
        Console.WriteLine();
        Console.ForegroundColor = originalColor;

        Console.WriteLine(string.Join("", results.Columns.Cast<DataColumn>()
            .Select(column => new string('-', columnWidths[column.ColumnName] + 2))));

        int rowCount = 0;
        foreach (DataRow row in results.Rows)
        {
            rowCount++;
            Console.ForegroundColor = rowCount % 2 == 0 ? ConsoleColor.White : ConsoleColor.Gray;

            foreach (DataColumn column in results.Columns)
            {
                string value = row[column] == DBNull.Value ? "NULL" : row[column].ToString() ?? "";
                if (value.Length > 40)
                {
                    value = value.Substring(0, 37) + "...";
                }
                Console.Write($"{value.PadRight(columnWidths[column.ColumnName] + 2)}");
            }
            Console.WriteLine();
        }

        Console.ForegroundColor = originalColor;
        WriteColorLine($"\n✅ Total rows: {results.Rows.Count}", ConsoleColors.Success);
    }

    public static async Task<DatabaseSchema> GetDatabaseSchemaAsync(DatabaseSchemaExtractor schemaExtractor, string connectionString)
    {
        string cacheFilePath = "Cache.txt";
        string connectionStringHash = ComputeHash(connectionString);

        if (File.Exists(cacheFilePath))
        {
            try
            {
                var cacheContent = await File.ReadAllTextAsync(cacheFilePath);
                var cache = JsonSerializer.Deserialize<Dictionary<string, DatabaseSchema>>(cacheContent);

                if (cache != null && cache.TryGetValue(connectionStringHash, out var cachedSchema))
                {
                    WriteColorLine("Using cached database schema", ConsoleColors.Success);
                    return cachedSchema;
                }
            }
            catch (Exception ex)
            {
                WriteColorLine($"Cache read error: {ex.Message}. Will extract fresh schema.", ConsoleColors.Error);
            }
        }

        WriteColorLine("Extracting fresh database schema...", ConsoleColors.Prompt);
        var databaseSchema = await schemaExtractor.ExtractSchemaAsync();

        try
        {
            var newCache = new Dictionary<string, DatabaseSchema>
            {
                [connectionStringHash] = databaseSchema
            };

            await File.WriteAllTextAsync(cacheFilePath, JsonSerializer.Serialize(newCache));
            WriteColorLine("Schema cached for future use", ConsoleColors.Success);
        }
        catch (Exception ex)
        {
            WriteColorLine($"Warning: Failed to cache schema: {ex.Message}", ConsoleColors.Error);
        }

        return databaseSchema;
    }

    public static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();

        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }

}
