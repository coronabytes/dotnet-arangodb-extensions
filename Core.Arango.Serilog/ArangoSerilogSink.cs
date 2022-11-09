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
        private readonly IArangoContext _arango;
        private readonly string _collection;
        private readonly string _database;

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
                if (!_arango.Database.ExistAsync(_database).Result)
                    _arango.Database.CreateAsync(_database).Wait();

                if (!_arango.Collection.ExistAsync(_database, collection).Result)
                    _arango.Collection.CreateAsync(_database, new ArangoCollection
                    {
                        Name = _collection,
                        KeyOptions = new ArangoKeyOptions
                        {
                            Type = ArangoKeyType.Padded
                        }
                    }).Wait();
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
                    Message = x.RenderMessage(),
                    Exception = x.Exception?.ToString(),
                    Properties = x.Properties.ToDictionary(y => y.Key,
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
            public string Message { get; set; }
            public string Exception { get; set; }
            public Dictionary<string, string> Properties { get; set; }
        }
    }
}