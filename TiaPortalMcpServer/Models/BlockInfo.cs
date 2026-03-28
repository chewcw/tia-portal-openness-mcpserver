using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    public class BlockInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("programmingLanguage")]
        public string? ProgrammingLanguage { get; set; }

        [JsonProperty("autoNumber")]
        public bool AutoNumber { get; set; }

        [JsonProperty("creationDate")]
        public System.DateTime? CreationDate { get; set; }

        [JsonProperty("modifiedDate")]
        public System.DateTime? ModifiedDate { get; set; }

        [JsonProperty("compileDate")]
        public System.DateTime? CompileDate { get; set; }
    }
}
