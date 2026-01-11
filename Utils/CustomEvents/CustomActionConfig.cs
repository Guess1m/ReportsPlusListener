using System.Collections.Generic;
using Newtonsoft.Json;

namespace ReportsPlus.Utils.CustomEvents{
    /// <summary>
    ///     Configuration model for a custom action sent from the server.
    /// </summary>
    public class CustomActionConfig{
        /// <summary>
        ///     The unique name of the action.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        ///     The method target string in "Namespace.Class.Method" format.
        /// </summary>
        [JsonProperty("target")]
        public string Target { get; set; }

        /// <summary>
        ///     The list of arguments used when invoking the target method.
        /// </summary>
        [JsonProperty("parameters")]
        public List<ActionParameterConfig> Parameters { get; set; } = new List<ActionParameterConfig>();
    }

    /// <summary>
    ///     Represents a single parameter used in a custom action.
    /// </summary>
    public class ActionParameterConfig{
        /// <summary>
        ///     The fully qualified name of the system type.
        /// </summary>
        [JsonProperty("Type")]
        public string Type { get; set; }

        /// <summary>
        ///     The raw string value to be converted to the target type.
        /// </summary>
        [JsonProperty("Value")]
        public string Value { get; set; }
    }
}