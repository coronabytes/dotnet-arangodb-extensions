using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Core.Arango.DataProtection
{
    internal class DataProtectionEntity
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Key { get; set; }

        public string FriendlyName { get; set; }

        public string Xml { get; set; }
    }
}