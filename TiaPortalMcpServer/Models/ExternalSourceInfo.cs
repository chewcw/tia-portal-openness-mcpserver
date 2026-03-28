using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    public class ExternalSourceInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("path")]
        public string Path { get; set; } = string.Empty;
    }
}
