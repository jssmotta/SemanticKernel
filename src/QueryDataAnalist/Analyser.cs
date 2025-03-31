using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using static QueryDataAnalist.Helpers;

namespace QueryDataAnalist;

public static class Analyser
{
    public static async Task RunSqlQueryAssistant(string connectionString)
    {
        try
        {
            var builder = Kernel.CreateBuilder();

            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("OpenAI API Key not found");

            string modelId = "gpt-4o-mini";

            builder.AddOpenAIChatCompletion(
                modelId: modelId,
                apiKey: apiKey
            );

            var kernel = builder.Build();


            var schemaExtractor = new DatabaseSchemaExtractor(connectionString);

            WriteColorLine("\n📊 SQL QUERY ASSISTANT", ConsoleColors.Title);
            WriteColorLine("Connecting to SQL Server and extracting database schema...", ConsoleColors.Prompt);

            var databaseSchema = await GetDatabaseSchemaAsync(schemaExtractor, connectionString);

            WriteColorLine($"✅ Successfully extracted schema for database: {schemaExtractor.DatabaseName}", ConsoleColors.Success);
            WriteColorLine($"📋 Found {databaseSchema.Tables.Count} tables", ConsoleColors.Success);

            var sqlQueryPlugin = new SqlQueryPlugin(databaseSchema);

            kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(sqlQueryPlugin, "SqlQueryPlugin"));

var chatHistory = new ChatHistory();

chatHistory.AddSystemMessage($"You are a Data Analyst and SQL expert assistant for the database: {schemaExtractor.DatabaseName}");

            while (true)
            {
                WriteColorLine("\nEnter your query in natural language (or 'exit' or press ENTER to quit):", ConsoleColors.Prompt);

                while (Console.KeyAvailable)
                {
                    Console.ReadKey(intercept: true);
                }

                string userInput = Console.ReadLine() ?? "";

                if (userInput.ToLower() == "exit" || string.IsNullOrWhiteSpace(userInput))
                    break;

                try
                {

                    WriteColorLine("Generating SQL query...", ConsoleColors.Prompt);

                    var isBasic = true;

                    var inputParam = isBasic ? "input" : "inputArg2";

                    var arguments = new KernelArguments
                    {
                        [inputParam] = userInput,
                        ["existingHistory"] = chatHistory
                    };

                    var result = await kernel.InvokeAsync<string>("SqlQueryPlugin", "GenerateSqlQuery", arguments);

                    var resultSql = ClearSql(result);

                    WriteColorLine("\nYour answer:", ConsoleColors.Prompt);
                    WriteColorLine("-------------------", ConsoleColors.Prompt);
                    WriteColorLine(ClearResult(result), ConsoleColors.Result);

                    if (!string.IsNullOrEmpty(resultSql))
                    {
                        WriteColorLine("\nGenerated SQL Query:", ConsoleColors.Prompt);
                        WriteColorLine("-------------------", ConsoleColors.Prompt);
                        WriteColorLine(resultSql, ConsoleColors.Query);

                        WriteColorLine("\nDo you want to execute this query? (y(es)/n(o))", ConsoleColors.Prompt);

                        string executeResponse = Console.ReadLine() ?? "";

                        if (executeResponse.ToLower() == "yes" || executeResponse.ToLower() == "y")
                        {
                            WriteColorLine("Executing query...", ConsoleColors.Prompt);
                            var queryResults = await schemaExtractor.ExecuteQueryAsync(resultSql);
                            DisplayQueryResults(queryResults);

                            chatHistory.AddUserMessage(userInput);
                            chatHistory.AddAssistantMessage(result);
                            chatHistory.AddUserMessage($"I executed the query and it returned {queryResults.Rows.Count} rows.");
                        }
                        else
                        {
                            chatHistory.AddUserMessage(userInput);
                            chatHistory.AddAssistantMessage(result);
                            chatHistory.AddUserMessage("I chose not to execute this query.");
                        }
                    }
                    else
                    {
                        WriteColorLine("\nNo SQL query was generated from the response.", ConsoleColors.Error);

                        chatHistory.AddUserMessage(userInput);
                        chatHistory.AddAssistantMessage("I couldn't generate a valid SQL query for your request.");
                    }
                }
                catch (Exception ex)
                {
                    WriteColorLine($"❌ Error: {ex.Message}", ConsoleColors.Error);

                    chatHistory.AddUserMessage(userInput);
                    chatHistory.AddAssistantMessage($"I encountered an error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            WriteColorLine($"❌ Error: {ex.Message}", ConsoleColors.Error);
        }
    }
}
