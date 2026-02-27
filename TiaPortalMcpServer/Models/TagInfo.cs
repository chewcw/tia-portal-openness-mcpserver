using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    /// <summary>
    /// Represents an individual PLC tag within a tag table.
    /// </summary>
    public class TagInfo
    {
        /// <summary>
        /// Name of the tag.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Data type name (e.g., "Bool", "Int", "UDT_MyType", "Array[0..9] of Real").
        /// </summary>
        [JsonProperty("dataTypeName")]
        public string DataTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Logical address (e.g., "%M0.0", "%DB1.DBX0.0", "%I0.0").
        /// </summary>
        [JsonProperty("logicalAddress")]
        public string LogicalAddress { get; set; } = string.Empty;

        /// <summary>
        /// Optional comment/description for the tag.
        /// </summary>
        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)]
        public string? Comment { get; set; }

        /// <summary>
        /// Tag table name this tag belongs to.
        /// </summary>
        [JsonProperty("tagTableName")]
        public string TagTableName { get; set; } = string.Empty;
    }
}
