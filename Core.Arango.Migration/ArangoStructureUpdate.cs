using System.Collections.Generic;
using System.Text.Json.Serialization;
using Core.Arango.Protocol;
using Newtonsoft.Json;

namespace Core.Arango.Migration
{
    public class ArangoStructureUpdate
    {
        [JsonProperty("collections")]
        [JsonPropertyName("collections")]
        public ICollection<ArangoCollectionIndices> Collections { get; set; } = new List<ArangoCollectionIndices>();

        [JsonProperty("graphs")]
        [JsonPropertyName("graphs")]
        public ICollection<ArangoGraph> Graphs { get; set; } = new List<ArangoGraph>();

        [JsonProperty("analyzers")]
        [JsonPropertyName("analyzers")]
        public ICollection<ArangoAnalyzer> Analyzers { get; set; } = new List<ArangoAnalyzer>();

        [JsonProperty("views")]
        [JsonPropertyName("views")]
        public ICollection<ArangoView> Views { get; set; } = new List<ArangoView>();
    }
}