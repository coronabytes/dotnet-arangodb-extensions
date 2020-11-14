using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Core.Arango.DevExtreme
{
    public class DxGroupResult
    {
        [JsonProperty("key")] 
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonProperty("display", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("display")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Display { get; set; }

        [JsonProperty("items")]
        [JsonPropertyName("items")]
        public List<DxGroupResult> Items { get; set; }

        [JsonProperty("count")]
        [JsonPropertyName("count")]
        public int? Count { get; set; }

        [JsonProperty("summary")]
        [JsonPropertyName("summary")]
        public decimal?[] Summary { get; set; }
    }
}