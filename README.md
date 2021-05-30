![Build](https://github.com/coronabytes/dotnet-arangodb-extensions/workflows/Build/badge.svg)

# Extensions for .NET ArangoDB Driver
See [dotnet-arangodb](https://github.com/coronabytes/dotnet-arangodb)

| Extension   | Nuget        | Command |
| :---        | :---         | :---    |
| [Core.Arango.Migration](https://www.nuget.org/packages/Core.Arango.Migration) | ![Nuget](https://img.shields.io/nuget/v/Core.Arango.Migration) ![Nuget](https://img.shields.io/nuget/dt/Core.Arango.Migration) | dotnet add package Core.Arango.Migration  |
| [Core.Arango.DataProtection](https://www.nuget.org/packages/Core.Arango.DataProtection) | ![Nuget](https://img.shields.io/nuget/v/Core.Arango.DataProtection) ![Nuget](https://img.shields.io/nuget/dt/Core.Arango.DataProtection) | dotnet add package Core.Arango.DataProtection |
| [Core.Arango.DevExtreme](https://www.nuget.org/packages/Core.Arango.DevExtreme) | ![Nuget](https://img.shields.io/nuget/v/Core.Arango.DevExtreme) ![Nuget](https://img.shields.io/nuget/dt/Core.Arango.DevExtreme) | dotnet add package Core.Arango.DevExtreme |
| [Core.Arango.Serilog](https://www.nuget.org/packages/Core.Arango.Serilog) | ![Nuget](https://img.shields.io/nuget/v/Core.Arango.Serilog) ![Nuget](https://img.shields.io/nuget/dt/Core.Arango.Serilog) | dotnet add package Core.Arango.Serilog |

# Migration
- Ensures the Arango structure / model is up-to-date
- Synchronises collection, index, graph, analyzer, views and custom functions from code model to arango db
  - objects are compared and if they differ they will be dropped and recreated
  - objects cannot be renamed with this method as they are matched by name and not by id
  - collections cannot be updated (data-loss) with this method, only new ones can be created and old ones dropped
- Database export and import support with zip-archives
- Full and partial updates
- Optional history collection for advanced migration scenarios, like running transformation queries
- Still under heavy development, might toss some functions around

## Extract structure to code model
```csharp
var migrationService = new ArangoMigrator(Arango);

var structure = await migrationService.GetStructureAsync("source-database");
```

## Apply structure from code model (dry run)
```csharp
var migrationService = new ArangoMigrator(Arango);

await migrationService.ApplyStructureAsync("target-database", structure, new ArangoMigrationOptions
{
    DryRun = true,
    Notify = n =>
    {
        if (n.State != ArangoMigrationState.Identical)
	    _output.WriteLine($"{n.State} {n.Object} {n.Name}");
    }
});
```

## Apply structure from code model 
```csharp
var migrationService = new ArangoMigrator(Arango);

await migrationService.ApplyStructureAsync("target-database", new ArangoStructure
{
    Collections = new List<ArangoCollectionIndices>
    {
	    new ()
	    {
		Collection = new ArangoCollection
		{
		    Name = "Project",
		    Type = ArangoCollectionType.Document
		},
		Indices = new List<ArangoIndex>
		{
		    new ()
		    {
			Name = "IDX_ParentKey",
			Fields = new List<string> {"ParentKey"},
			Type = ArangoIndexType.Hash
		    }
		}
	    },
	    new ()
	    {
		Collection = new ArangoCollection
		{
		    Name = "Activity",
		    Type = ArangoCollectionType.Document
		},
		Indices = new List<ArangoIndex>
		{
		    new ()
		    {
			Name = "IDX_ProjectKey",
			Fields = new List<string> {"ProjectKey"},
			Type = ArangoIndexType.Hash
		    }
		}
	    }
    }
});
```

## Export database with structure and data to zip archive
```csharp
var migrationService = new ArangoMigrator(Arango);

await using var fs = File.Create("export.zip", 1024 * 1024);
await migrationService.ExportAsync("source-database", fs, ArangoMigrationScope.Data | ArangoMigrationScope.Structure);
```

## Import database with structure and data from zip archive
```csharp
var migrationService = new ArangoMigrator(Arango);

await using var fs = File.OpenRead("export.zip");
await migrationService.ImportAsync("target-database", fs, ArangoMigrationScope.Data | ArangoMigrationScope.Structure);
```

## Advanced migrations with history collection
```csharp
var migrator = new ArangoMigrator(Arango);
migrator.HistoryCollection = "MigrationHistory";

// load all migrations from assembly
migrator.AddMigrations(typeof(Program).Assembly);

// apply all migrations up to latest
await migrator.UpgradeAsync("target-database");

// sample migration / downgrades not yet supported
public class M20210401_001 : IArangoMigration
{
    public long Id => 20210401_001; // sortable unique id
    public string Name => "Initial";
    
    public async Task Up(IArangoMigrator migrator, ArangoHandle handle)
    {
        await migrator.ApplyStructureAsync(...);	
	await migrator.Context.Query.ExecuteAsync(...);
    }

    public Task Down(IArangoMigrator migrator, ArangoHandle handle)
    {
        throw new NotImplementedException();
    }
}
```

# DataProtection
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton(new ArangoContext(Configuration.GetConnectionString("Arango")));

    var dataProtection = services.AddDataProtection()
        .SetApplicationName("App")
        .SetDefaultKeyLifetime(TimeSpan.FromDays(30));
    dataProtection.PersistKeysToArangoDB(database: "dataprotection", collection: "keys");
}
```

# DevExtreme
- Translates DevExtreme queries to AQL with filtering, sorting, grouping and summaries on a 'best effort basis'
- Parameters are escaped with bindvars
- Property names 
  - need to match ^[A-Za-z_][A-Za-z0-9\\.]*$
  - need to be within 128 characters
  - can be customized with ValidPropertyName() and PropertyTransform()
- Developer retains full control over the projection - full document by default
- Check safety limits in settings if your query fails
- Support for ArangoSearch is coming soon
  - Not so soon...
- Groupings by foreign key can be enriched with displayValue using GroupLookups()
```csharp

private static readonly ArangoTransformSettings Transform = new ArangoTransformSettings
{
    IteratorVar = "x",
    Key = "key",
    Filter = "x.Active == true",
    RestrictGroups = new HashSet<string>
	{
		"ProjectKey", "UserKey"
	}, // null allows all groupings (not recommended) / empty hashset disables grouping
	GroupLookups = new Dictionary<string, string>
	{
		["ProjectKey"] = "DOCUMENT(AProject, ProjectKey).Name",
		["UserKey"] = "DOCUMENT(AUser, UserKey).Name"
	}
};

[HttpGet("dx-query")]
public async Task<ActionResult<DxLoadResult>> DxQuery([FromQuery] DataSourceLoadOptions loadOptions)
{
    var arangoTransform = new ArangoTransform(loadOptions, Transform);

    if (!arangoTransform.Transform(out var error))
        return BadRequest(error);

    return await arangoTransform.ExecuteAsync<SomeEntity>(arango, "database", "collection");
}
```

# Serilog
```csharp
webBuilder.UseSerilog((c, log) =>
{
    var arango = c.Configuration.GetConnectionString("Arango");

    log.MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Sink(new ArangoSerilogSink(new ArangoContext(arango), 
            database: "logs", 
            collection: "logs", 
            batchPostingLimit: 50, 
            TimeSpan.FromSeconds(2)), 
            restrictedToMinimumLevel: LogEventLevel.Information);

    // This is unreliable...
    if (Environment.UserInteractive)
        log.WriteTo.Console(theme: AnsiConsoleTheme.Code);
});
```
