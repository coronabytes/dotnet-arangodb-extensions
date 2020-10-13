![Build](https://github.com/coronabytes/dotnet-arangodb-extensions/workflows/Build/badge.svg)

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

# DevExtreme Query
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
