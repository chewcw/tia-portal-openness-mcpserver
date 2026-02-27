using System;
using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    /// <summary>
    /// Represents the result of a tag operation (create, delete, modify, export).
    /// </summary>
    public class TagOperationResult
    {
        /// <summary>
        /// Name of the entity that was operated on.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Type of operation: "create", "delete", "modify", "export".
        /// </summary>
        [JsonProperty("operation")]
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Type of entity: "tagtable", "group", "tag", "constant".
        /// </summary>
        [JsonProperty("entityType")]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// Additional details about the operation.
        /// </summary>
        [JsonProperty("details", NullValueHandling = NullValueHandling.Ignore)]
        public string? Details { get; set; }

        /// <summary>
        /// Timestamp when operation was performed.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
