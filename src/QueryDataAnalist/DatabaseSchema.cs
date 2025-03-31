using System.Text;

namespace QueryDataAnalist; 
public class DatabaseSchema
{
    public string DatabaseName { get; set; } = string.Empty;
    public List<TableSchema> Tables { get; set; } = new List<TableSchema>();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Database: {DatabaseName}");
        sb.AppendLine($"Tables: {Tables.Count}");

        foreach (var table in Tables)
        {
            sb.AppendLine(table.ToString());
        }

        return sb.ToString();
    }
}