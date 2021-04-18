using System.Collections.Generic;
using System.Text.Json.Serialization;
using Core.Arango.Protocol;
using Newtonsoft.Json;

namespace Core.Arango.Migration
{
    /// <summary>
    /// Structure information of an ArangoDB database
    /// </summary>
    public class ArangoStructure
    {
        /// <summary>
        ///  collections
        /// </summary>
        [JsonProperty("collections")]
        [JsonPropertyName("collections")]
        public ICollection<ArangoCollectionIndices> Collections { get; set; } = new List<ArangoCollectionIndices>();

        /// <summary>
        ///  graphs
        /// </summary>
        [JsonProperty("graphs")]
        [JsonPropertyName("graphs")]
        public ICollection<ArangoGraph> Graphs { get; set; } = new List<ArangoGraph>();

        /// <summary>
        ///  analyzers
        /// </summary>
        [JsonProperty("analyzers")]
        [JsonPropertyName("analyzers")]
        public ICollection<ArangoAnalyzer> Analyzers { get; set; } = new List<ArangoAnalyzer>();

        /// <summary>
        /// views
        /// </summary>
        [JsonProperty("views")]
        [JsonPropertyName("views")]
        public ICollection<ArangoView> Views { get; set; } = new List<ArangoView>();

        /// <summary>
        ///  functions
        /// </summary>
        [JsonProperty("functions")]
        [JsonPropertyName("functions")]
        public ICollection<ArangoFunctionDefinition> Functions { get; set; } = new List<ArangoFunctionDefinition>();
    }
}