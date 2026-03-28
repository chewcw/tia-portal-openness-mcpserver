using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    public class PlcTypeInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("namespace")]
        public string? Namespace { get; set; }
    }
}
