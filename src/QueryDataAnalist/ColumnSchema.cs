namespace QueryDataAnalist;
 
public class ColumnSchema
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? Size { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string? ForeignKeyTable { get; set; }
    public string? ForeignKeyColumn { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }

    public override string ToString()
    {
        var typeInfo = DataType;

        if (Size.HasValue && Size.Value != -1)
        {
            typeInfo += $"({Size.Value})";
        }
        else if (Precision.HasValue)
        {
            typeInfo += Scale.HasValue && Scale.Value > 0
                ? $"({Precision.Value},{Scale.Value})"
                : $"({Precision.Value})";
        }

        var nullableInfo = IsNullable ? "NULL" : "NOT NULL";
        var pkInfo = IsPrimaryKey ? "PK" : "";
        var fkInfo = IsForeignKey ? $"FK -> {ForeignKeyTable}.{ForeignKeyColumn}" : "";

        return $"{Name} [{typeInfo}] {nullableInfo} {pkInfo} {fkInfo}".Trim();
    }
}