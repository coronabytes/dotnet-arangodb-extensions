using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Core.Arango.Protocol;
using Newtonsoft.Json;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Core.Arango.Serilog
{
    public class ArangoSerilogSink : IBatchedLogEventSink
    {
        [Flags]
        public enum LoggingRenderStrategy
        {
            RenderMessage = 1,
            StoreTemplate = 2,
        }

        private readonly IArangoContext _arango;
        private readonly string _collection;
        private readonly string _database;
        private readonly LoggingRenderStrategy _renderMessage;
        private readonly bool _indexLevel;
        private readonly bool _indexTimestamp;
        private readonly bool _indexTemplate;

        public ArangoSerilogSink(
            IArangoContext arango,
            string database = "logs",
            string collection = "logs",
            LoggingRenderStrategy renderMessage = LoggingRenderStrategy.RenderMessage,
            bool indexLevel = false,
            bool indexTimestamp = false,
            bool indexTemplate = false)
        {
            _arango = arango;
            _database = database;
            _collection = collection;
            _renderMessage = renderMessage;
            _indexLevel = indexLevel;
            _indexTimestamp = indexTimestamp;
            _indexTemplate = indexTemplate;
            
            Setup().GetAwaiter().GetResult();
        }

        private async Task Setup()
        {
            try
            {
                if (!await _arango.Database.ExistAsync(_database))
                {
                    await _arango.Database.CreateAsync(_database);
                }

                if (!await _arango.Collection.ExistAsync(_database, _collection))
                {
                    await _arango.Collection.CreateAsync(_database, new ArangoCollection
                    {
                        Name = _collection,
                        KeyOptions = new ArangoKeyOptions
                        {
                            Type = ArangoKeyType.Padded
                        }
                    });
                }

                var indexes = (await _arango.Index.ListAsync(_database, _collection)).ToList();

                if (_indexLevel && indexes.All(x => x.Name != nameof(LogEventEntity.Level)))
                {
                    await _arango.Index.CreateAsync(_database, _collection, new ArangoIndex
                    {
                        Type = ArangoIndexType.Persistent,
                        Name = nameof(LogEventEntity.Level),
                        Fields = [nameof(LogEventEntity.Level)]
                    });
                }

                if (_indexTimestamp && indexes.All(x => x.Name != nameof(LogEventEntity.Timestamp)))
                {
                    await _arango.Index.CreateAsync(_database, _collection, new ArangoIndex
                    {
                        Type = ArangoIndexType.Persistent,
                        Name = nameof(LogEventEntity.Timestamp),
                        Fields = [nameof(LogEventEntity.Timestamp)]
                    });
                }

                if (_indexTemplate && indexes.All(x => x.Name != nameof(LogEventEntity.MessageTemplate)))
                {
                    await _arango.Index.CreateAsync(_database, _collection, new ArangoIndex
                    {
                        Type = ArangoIndexType.Persistent,
                        Name = nameof(LogEventEntity.MessageTemplate),
                        Fields = [nameof(LogEventEntity.MessageTemplate)]
                    });
                }
            }
            catch (Exception)
            {
                //
            }
        }

        public async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            try
            {
                var renderMessage = _renderMessage.HasFlag(LoggingRenderStrategy.RenderMessage);
                var storeTemplate = _renderMessage.HasFlag(LoggingRenderStrategy.StoreTemplate);

                await _arango.Document.CreateManyAsync(_database, _collection, events.Select(x => new LogEventEntity
                {
                    Level = x.Level.ToString(),
                    Timestamp = x.Timestamp.UtcDateTime,
                    Message = renderMessage
                        ? x.RenderMessage()
                        : null,
                    MessageTemplate =
                        storeTemplate
                            ? x.MessageTemplate.Text
                            : null,
                    Exception = x.Exception?.ToString(),
                    Properties = x.Properties.ToDictionary(
                        y => y.Key,
                        y => y.Value.ToString())
                }));
            }
            catch (Exception)
            {
                //
            }
        }

        public Task OnEmptyBatchAsync()
        {
            return Task.CompletedTask;
        }

        public class LogEventEntity
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string Key { get; set; }

            public DateTime Timestamp { get; set; }

            public string Level { get; set; }

            [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string Message { get; set; }

            [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string MessageTemplate { get; set; }

            public string Exception { get; set; }

            public Dictionary<string, string> Properties { get; set; }
        }
    }
}