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
        private readonly LoggingRenderStrategy _renderMessage = LoggingRenderStrategy.RenderMessage;
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
            bool indexTemplate = false) : this(arango, database, collection)
        {
            _renderMessage = renderMessage;
            _indexLevel = indexLevel;
            _indexTimestamp = indexTimestamp;
            _indexTemplate = indexTemplate;
        }


        public ArangoSerilogSink(
            IArangoContext arango,
            string database = "logs",
            string collection = "logs")
        {
            _arango = arango;
            _database = database;
            _collection = collection;

            try
            {
                if (!_arango.Database.ExistAsync(_database).AsTask().GetAwaiter().GetResult())
                    _arango.Database.CreateAsync(_database).AsTask().Wait();

                var indexes = _arango.Index.ListAsync(_database, _collection)
                    .AsTask().GetAwaiter().GetResult();

                if (!_arango.Collection.ExistAsync(_database, collection).AsTask().GetAwaiter().GetResult())
                {
                    _arango.Collection.CreateAsync(_database, new ArangoCollection
                    {
                        Name = _collection,
                        KeyOptions = new ArangoKeyOptions
                        {
                            Type = ArangoKeyType.Padded
                        }
                    }).AsTask().Wait();
                }

                if (_indexLevel &&
                    indexes.Any(x => x.Name == nameof(LogEventEntity.Level)))
                {
                    _arango.Index.CreateAsync(_database, _collection, new ArangoIndex
                    {
                        Type = ArangoIndexType.Persistent,
                        Name = nameof(LogEventEntity.Level)
                    }).AsTask().Wait();
                }

                if (_indexTimestamp &&
                    indexes.Any(x => x.Name == nameof(LogEventEntity.Timestamp)))
                {
                    _arango.Index.CreateAsync(_database, _collection, new ArangoIndex
                    {
                        Type = ArangoIndexType.Persistent,
                        Name = nameof(LogEventEntity.Timestamp)
                    }).AsTask().Wait();
                }
                
                if (_indexTemplate &&
                    indexes.Any(x => x.Name == nameof(LogEventEntity.MessageTemplate)))
                {
                    _arango.Index.CreateAsync(_database, _collection, new ArangoIndex
                    {
                        Type = ArangoIndexType.Persistent,
                        Name = nameof(LogEventEntity.MessageTemplate)
                    }).AsTask().Wait();
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
                await _arango.Document.CreateManyAsync(_database, _collection, events.Select(x => new LogEventEntity
                {
                    Level = x.Level.ToString(),
                    Timestamp = x.Timestamp.UtcDateTime,
                    Message = _renderMessage.HasFlag(LoggingRenderStrategy.RenderMessage)
                        ? x.RenderMessage()
                        : null,
                    MessageTemplate =
                        _renderMessage.HasFlag(LoggingRenderStrategy.StoreTemplate)
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