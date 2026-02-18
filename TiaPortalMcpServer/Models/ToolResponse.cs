using System;
using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    /// <summary>
    /// Standardized response format for all MCP tool operations
    /// </summary>
    public class ToolResponse<T>
    {
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
                Details = details
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
        public const string BlockExists = "BLOCK_EXISTS";
        public const string BlockNotFound = "BLOCK_NOT_FOUND";
        public const string CompilationError = "COMPILATION_ERROR";
        public const string AlreadyOpen = "ALREADY_OPEN";
        public const string NotOpen = "NOT_OPEN";
        public const string NotImplemented = "NOT_IMPLEMENTED";
    }
}
