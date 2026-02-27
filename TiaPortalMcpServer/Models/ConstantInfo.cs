using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    /// <summary>
    /// Represents a user or system constant within a tag table.
    /// </summary>
    public class ConstantInfo
    {
        /// <summary>
        /// Name of the constant.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Data type of the constant.
        /// </summary>
        [JsonProperty("dataTypeName")]
        public string DataTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Value of the constant as string.
        /// </summary>
        [JsonProperty("value")]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Type of constant: "user" or "system".
        /// </summary>
        [JsonProperty("constantType")]
        public string ConstantType { get; set; } = string.Empty;

        /// <summary>
        /// Tag table name this constant belongs to.
        /// </summary>
        [JsonProperty("tagTableName")]
        public string TagTableName { get; set; } = string.Empty;
    }
}
