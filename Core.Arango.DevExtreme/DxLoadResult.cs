using System.ComponentModel;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Core.Arango.DevExtreme
{
    public class DxLoadResult
    {
        [JsonProperty("data")] 
        [JsonPropertyName("data")]
        public object Data { get; set; }


        [DefaultValue(-1)]
        [JsonProperty("totalCount", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("totalCount")]
        public long TotalCount { get; set; } = -1;

        [DefaultValue(-1)]
        [JsonProperty("groupCount", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("groupCount")]
        public long GroupCount { get; set; } = -1;

        
        [JsonProperty("summary", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("summary")]
        public decimal?[] Summary { get; set; }
    }
}