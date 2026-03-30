using System;
using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    /// <summary>
    /// Standardized response format for all MCP tool operations
    /// </summary>
    public class ToolResponse<T>
    {
        private static readonly bool IncludeErrorDetails =
            string.Equals(Environment.GetEnvironmentVariable("TIA_MCP_INCLUDE_ERROR_DETAILS"), "true", StringComparison.OrdinalIgnoreCase);

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public T Data { get; set; } = default!;

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string? Error { get; set; }

        [JsonProperty("errorCode", NullValueHandling = NullValueHandling.Ignore)]
        public string? ErrorCode { get; set; }

        [JsonProperty("details", NullValueHandling = NullValueHandling.Ignore)]
        public string? Details { get; set; }

        public static ToolResponse<T> CreateSuccess(T data)
        {
            return new ToolResponse<T>
            {
                Success = true,
                Data = data
            };
        }

        public static ToolResponse<T> CreateError(string errorCode, string error, string? details = null)
        {
            return new ToolResponse<T>
            {
                Success = false,
                Error = error,
                ErrorCode = errorCode,
                Details = IncludeErrorDetails ? details : null
            };
        }
    }

    /// <summary>
    /// Common error codes used throughout the application
    /// </summary>
    public static class ErrorCodes
    {
        public const string NoProject = "NO_PROJECT";
        public const string ProjectNotFound = "PROJECT_NOT_FOUND";
        public const string TiaError = "TIA_ERROR";
        public const string ComError = "COM_ERROR";
        public const string InvalidParameter = "INVALID_PARAMETER";
        public const string DeviceNotFound = "DEVICE_NOT_FOUND";
        public const string DeviceItemNotFound = "DEVICE_ITEM_NOT_FOUND";
        public const string BlockExists = "BLOCK_EXISTS";
        public const string BlockNotFound = "BLOCK_NOT_FOUND";
        public const string CompilationError = "COMPILATION_ERROR";
        public const string AlreadyOpen = "ALREADY_OPEN";
        public const string NotOpen = "NOT_OPEN";
        public const string NotImplemented = "NOT_IMPLEMENTED";
        public const string TagTableNotFound = "TAG_TABLE_NOT_FOUND";
        public const string TagGroupNotFound = "TAG_GROUP_NOT_FOUND";
        public const string TagNotFound = "TAG_NOT_FOUND";
        public const string TagTableIsDefault = "TAG_TABLE_IS_DEFAULT";
        public const string InvalidTagName = "INVALID_TAG_NAME";
        public const string OperationNotSupported = "OPERATION_NOT_SUPPORTED";
        public const string InternalError = "INTERNAL_ERROR";
        public const string UserCancelled = "USER_CANCELLED";
    }
}
