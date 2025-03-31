using Microsoft.Extensions.Configuration;
using QueryDataAnalist;
using static QueryDataAnalist.Helpers; 
 
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

IConfiguration configuration = builder.Build();

string baseConnectionString = configuration.GetConnectionString("SqlServer") ?? throw new Exception("Not found SqlServer connection");

try
{
    WriteColorLine("\n🔍 SQL SERVER DATABASE SELECTOR", ConsoleColors.Title);
    WriteColorLine("Connecting to SQL Server and searching for available databases...", ConsoleColors.Prompt);
 
    var databases = await GetAvailableDatabases(baseConnectionString);

    if (databases.Count == 0)
    {
        WriteColorLine("❌ No databases found on the local server.", ConsoleColors.Error);
        return;
    }

    WriteColorLine($"✅ Found {databases.Count} databases", ConsoleColors.Success);
    WriteColorLine("\nUse arrow keys ↑/↓ to navigate and ENTER to select a database:", ConsoleColors.Prompt);

 
    int selectedIndex = ShowDatabaseSelectionMenu(databases);
    string selectedDatabase = databases[selectedIndex];

    WriteColorLine($"\n✅ Selected database: {selectedDatabase}", ConsoleColors.Success);
 
    string connectionString = $"{baseConnectionString};Database={selectedDatabase};";


    await Analyser.RunSqlQueryAssistant(connectionString);
}
catch (Exception ex)
{
    WriteColorLine($"❌ Error: {ex.Message}", ConsoleColors.Error);
}


