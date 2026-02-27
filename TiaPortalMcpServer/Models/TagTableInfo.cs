using System;
using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    /// <summary>
    /// Represents metadata about a PLC tag table.
    /// </summary>
    public class TagTableInfo
    {
        /// <summary>
        /// Name of the tag table.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this is the default tag table (cannot be deleted).
        /// </summary>
        [JsonProperty("isDefault")]
        public bool IsDefault { get; set; }

        /// <summary>
        /// Last modification timestamp (UTC).
        /// </summary>
        [JsonProperty("modifiedTimeStamp")]
        public DateTime ModifiedTimeStamp { get; set; }

        /// <summary>
        /// Number of tags in this table.
        /// </summary>
        [JsonProperty("tagCount")]
        public int TagCount { get; set; }

        /// <summary>
        /// Number of user-defined constants in this table.
        /// </summary>
        [JsonProperty("userConstantCount")]
        public int UserConstantCount { get; set; }

        /// <summary>
        /// Number of system constants in this table.
        /// </summary>
        [JsonProperty("systemConstantCount")]
        public int SystemConstantCount { get; set; }

        /// <summary>
        /// Parent group name (empty for system group, group name for user groups).
        /// </summary>
        [JsonProperty("groupName")]
        public string GroupName { get; set; } = string.Empty;
    }
}
