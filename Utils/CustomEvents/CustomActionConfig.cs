using System.Collections.Generic;
using Newtonsoft.Json;

namespace ReportsPlus.Utils.CustomEvents{
    public class CustomActionConfig{
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("target")] public string Target { get; set; } // "Namespace.Class.Method"

        [JsonProperty("parameters")] public List<ActionParameterConfig> Parameters { get; set; } = new List<ActionParameterConfig>();
    }

    public class ActionParameterConfig{
        [JsonProperty("Type")] public string Type { get; set; } // "System.Boolean"

        [JsonProperty("Value")] public string Value { get; set; } // "false"
    }
}