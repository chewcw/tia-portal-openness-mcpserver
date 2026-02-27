using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    /// <summary>
    /// Represents a user-defined group (folder) for organizing tag tables.
    /// </summary>
    public class TagGroupInfo
    {
        /// <summary>
        /// Name of the tag group.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Number of tag tables directly in this group.
        /// </summary>
        [JsonProperty("tagTableCount")]
        public int TagTableCount { get; set; }

        /// <summary>
        /// Number of sub-groups within this group.
        /// </summary>
        [JsonProperty("subGroupCount")]
        public int SubGroupCount { get; set; }

        /// <summary>
        /// Names of direct child sub-groups.
        /// </summary>
        [JsonProperty("subGroupNames")]
        public string[] SubGroupNames { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Names of tag tables in this group.
        /// </summary>
        [JsonProperty("tagTableNames")]
        public string[] TagTableNames { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Parent group name (empty if this is top-level user group).
        /// </summary>
        [JsonProperty("parentGroupName")]
        public string? ParentGroupName { get; set; }
    }
}
