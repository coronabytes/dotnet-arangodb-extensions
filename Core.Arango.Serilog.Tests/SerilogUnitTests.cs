using Serilog;
using Serilog.Sinks.PeriodicBatching;
using Testcontainers.ArangoDb;

namespace Core.Arango.Serilog.Tests;

using Xunit;
using Xunit.Abstractions;

public class SerilogUnitTests : IAsyncLifetime
{
    private const string DatabaseName = "test";
    private const string CollectionName = "logs";

    private ArangoDbContainer? _arangoDbContainer;
    private Func<IArangoConfiguration>? _createArangoConfiguration;

    public async Task InitializeAsync()
    {
        _arangoDbContainer = new ArangoDbBuilder()
            .WithPassword("password")
            .Build();
        await _arangoDbContainer.StartAsync();

        _createArangoConfiguration = () => new ArangoConfiguration
        {
            ConnectionString =
                $"Server={_arangoDbContainer.GetTransportAddress()};User=root;Realm=CI-{Guid.NewGuid():D};Password=password;",
        };
    }

    public async Task DisposeAsync()
    {
        await _arangoDbContainer!.DisposeAsync();
    }

    [Fact]
    public async Task DefaultDatabaseIsCreated()
    {
        var config = _createArangoConfiguration!();
        var arango = new ArangoContext(config);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Sink(new PeriodicBatchingSink(
                new ArangoSerilogSink(arango,
                    "logs",
                    "logs"),
                new()
                {
                    BatchSizeLimit = 1000,
                    QueueLimit = 100000,
                    Period = TimeSpan.FromSeconds(2),
                    EagerlyEmitFirstEvent = true,
                }
            ))
            .MinimumLevel.Debug()
            .CreateLogger();
        
        Assert.True(await arango.Database.ExistAsync("logs"));
        Assert.True(await arango.Collection.ExistAsync("logs","logs"));
    }
    
    [Fact]
    public async Task DatabaseAndIndexesAreCreated()
    {
        var config = _createArangoConfiguration!();
        var arango = new ArangoContext(config);
        
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Sink(new PeriodicBatchingSink(
                new ArangoSerilogSink(arango,
                    DatabaseName,
                    CollectionName,
                    ArangoSerilogSink.LoggingRenderStrategy.StoreTemplate,
                    true,
                    true,
                    true),
                new()
                {
                    BatchSizeLimit = 1000,
                    QueueLimit = 100000,
                    Period = TimeSpan.FromSeconds(2),
                    EagerlyEmitFirstEvent = true,
                }
            ))
            .MinimumLevel.Debug()
            .CreateLogger();

        var indexes = await arango.Index.ListAsync(DatabaseName, CollectionName);
        
        Assert.True(await arango.Database.ExistAsync(DatabaseName));
        Assert.True(await arango.Collection.ExistAsync(DatabaseName, CollectionName));

        Assert.Contains(indexes!, x => x.Name == nameof(ArangoSerilogSink.LogEventEntity.Level));
        Assert.Contains(indexes!, x => x.Name == nameof(ArangoSerilogSink.LogEventEntity.Timestamp));
        Assert.Contains(indexes!, x => x.Name == nameof(ArangoSerilogSink.LogEventEntity.MessageTemplate));
    }
}