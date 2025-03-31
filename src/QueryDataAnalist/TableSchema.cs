using System.Text;

namespace QueryDataAnalist;

public class TableSchema
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public List<ColumnSchema> Columns { get; set; } = new List<ColumnSchema>();
    public string FullName => $"{Schema}.{Name}";
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Table: {FullName}");
        sb.AppendLine("Columns:");

        foreach (var column in Columns)
        {
            sb.AppendLine($"  {column}");
        }

        return sb.ToString();
    }
}