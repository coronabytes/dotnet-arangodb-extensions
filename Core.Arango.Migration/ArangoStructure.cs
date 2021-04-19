using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private string GetCSharpString(object o)
        {
            if (o is bool)
            {
                return $"{o.ToString().ToLower()}";
            }
            if (o is string)
            {
                return $"\"{o}\"";
            }
            if (o is int)
            {
                return $"{o}";
            }
            if (o is decimal)
            {
                return $"{o}m";
            }
            if (o is DateTime)
            {
                return $"DateTime.Parse(\"{o}\")";
            }
            if (o is Enum)
            {
                return $"{o.GetType().FullName}.{o}";
            }
            if (o is IEnumerable)
            {
                return $"new {GetClassName(o)} \r\n{{\r\n{GetItems((IEnumerable)o)}}}";
            }

            return CreateObject(o).ToString();
        }

        private string GetItems(IEnumerable items)
        {
            return items.Cast<object>().Aggregate(string.Empty, (current, item) => current + $"{GetCSharpString(item)},\r\n");
        }

        private StringBuilder CreateObject(object o)
        {
            var builder = new StringBuilder();
            builder.Append($"new {GetClassName(o)} \r\n{{\r\n");

            foreach (var property in o.GetType().GetProperties())
            {
                var value = property.GetValue(o);
                if (value != null)
                {
                    builder.Append($"{property.Name} = {GetCSharpString(value)},\r\n");
                }
            }

            builder.Append("}");
            return builder;
        }

        private string GetClassName(object o)
        {
            var type = o.GetType();

            if (type.IsGenericType)
            {
                var arg = type.GetGenericArguments().First().Name;
                return type.Name.Replace("`1", $"<{arg}>");
            }

            return type.Name;
        }

        /// <summary>
        ///  Serialize to C# object initializer code
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public string Serialize()
        {
            return CreateObject(this).ToString();
        }
    }
}