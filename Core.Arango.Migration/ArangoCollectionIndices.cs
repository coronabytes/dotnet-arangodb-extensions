using System.Collections.Generic;
using System.Text.Json.Serialization;
using Core.Arango.Protocol;
using Newtonsoft.Json;

namespace Core.Arango.Migration
{
    public class ArangoCollectionIndices
    {
        [JsonProperty("collection")]
        [JsonPropertyName("collection")]
        public ArangoCollection Collection { get; set; }

        [JsonProperty("indices")]
        [JsonPropertyName("indices")]
        public ICollection<ArangoIndex> Indices { get; set; } = new List<ArangoIndex>();
    }
}