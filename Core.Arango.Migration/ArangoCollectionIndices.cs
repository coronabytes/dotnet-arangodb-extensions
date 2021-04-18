using System.Collections.Generic;
using System.Text.Json.Serialization;
using Core.Arango.Protocol;
using Newtonsoft.Json;

namespace Core.Arango.Migration
{
    /// <summary>
    ///  Arango collection with index definitions
    /// </summary>
    public class ArangoCollectionIndices
    {
        /// <summary>
        ///  collection
        /// </summary>
        [JsonProperty("collection")]
        [JsonPropertyName("collection")]
        public ArangoCollection Collection { get; set; }

        /// <summary>
        ///  index definitions
        /// </summary>
        [JsonProperty("indices")]
        [JsonPropertyName("indices")]
        public ICollection<ArangoIndex> Indices { get; set; } = new List<ArangoIndex>();
    }
}