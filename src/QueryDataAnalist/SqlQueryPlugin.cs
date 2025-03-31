using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Text;

namespace QueryDataAnalist;

public class SqlQueryPlugin
{
    private readonly DatabaseSchema _databaseSchema;
    public SqlQueryPlugin(DatabaseSchema databaseSchema)
    {
        _databaseSchema = databaseSchema;
    }

    [KernelFunction]
    [Description("Generates a SQL query based on the user's natural language request and database schema")]
public async Task<string> GenerateSqlQuery(
    [Description("User's natural language request for a SQL query")] string input,
    Kernel kernel,
    [Description("Optional chat history for context")] ChatHistory? existingHistory = null)
{
    string schemaDescription = GetDatabaseSchemaDescription();
         
    var chatHistory = existingHistory ?? new ChatHistory();
         
    if (existingHistory == null || chatHistory.Count()==1)
    {
        chatHistory.AddSystemMessage(@$"
# SQL Query Assistant

You are a Data Analyst and SQL expert who helps translate natural language questions about databases into SQL queries.

## Database Schema
{schemaDescription}

## Guidelines
- Use proper T-SQL syntax for SQL Server
- Include appropriate JOINs when data from multiple tables is needed
- Format your output as a clear explanation followed by the SQL query in ```sql``` code blocks
- Always use schema name in the query (example: dbo.TableName)
- For queries that involve aggregations, add appropriate GROUP BY clauses
- Handle NULLs appropriately");
    }
         
    chatHistory.AddUserMessage(input);
         
    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
         
    var completionResult = await chatCompletionService.GetChatMessageContentAsync(
        chatHistory,
        new OpenAIPromptExecutionSettings
        {
            MaxTokens = 2000,
            Temperature = 0.0,
            TopP = 0.95
        });
         
    chatHistory.AddAssistantMessage(completionResult.ToString());

    return completionResult.ToString().Trim();
}

    private string GetDatabaseSchemaDescription()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Database: {_databaseSchema.DatabaseName}");
        sb.AppendLine();
         
        var tablesBySchema = _databaseSchema.Tables
            .GroupBy(t => t.Schema)
            .OrderBy(g => g.Key);

        foreach (var schemaGroup in tablesBySchema)
        {
            sb.AppendLine($"Schema: {schemaGroup.Key}");

            foreach (var table in schemaGroup.OrderBy(t => t.Name))
            {
                sb.AppendLine($"Table: {table.FullName}");
                sb.AppendLine("Columns:");

                foreach (var column in table.Columns)
                {
                    string description = column.Description != null ? $" - {column.Description}" : "";
                    string constraints = string.Join(", ", new[] {
                        column.IsPrimaryKey ? "PRIMARY KEY" : null,
                        !column.IsNullable ? "NOT NULL" : null,
                        column.IsForeignKey ? $"FOREIGN KEY references {column.ForeignKeyTable}.{column.ForeignKeyColumn}" : null
                    }.Where(c => c != null));

                    string constraintStr = constraints.Length > 0 ? $" ({string.Join(", ", constraints)})" : "";
                     
                    string typeDetail = "";
                    if (column.Size.HasValue && column.Size.Value != -1)
                    {
                        typeDetail = $"({column.Size.Value})";
                    }
                    else if (column.Precision.HasValue)
                    {
                        typeDetail = column.Scale.HasValue && column.Scale.Value > 0
                            ? $"({column.Precision.Value},{column.Scale.Value})"
                            : $"({column.Precision.Value})";
                    }

                    sb.AppendLine($"  - {column.Name}: {column.DataType}{typeDetail}{constraintStr}{description}");
                }

                sb.AppendLine();
            }
        }
         
        sb.AppendLine("## Foreign Key Relationships");
        foreach (var table in _databaseSchema.Tables.OrderBy(t => t.FullName))
        {
            var foreignKeys = table.Columns.Where(c => c.IsForeignKey).ToList();
            if (foreignKeys.Any())
            {
                sb.AppendLine($"{table.FullName} relationships:");
                foreach (var fk in foreignKeys)
                {
                    sb.AppendLine($"  - {fk.Name} -> {fk.ForeignKeyTable}.{fk.ForeignKeyColumn}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}