using System;
using System.Collections.Generic;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json;

namespace TiaPortalMcpServer.Models
{
    public static class McpToolResults
    {
        private static readonly bool IncludeErrorDetails =
            string.Equals(Environment.GetEnvironmentVariable("TIA_MCP_INCLUDE_ERROR_DETAILS"), "true", StringComparison.OrdinalIgnoreCase);

        public static CallToolResult Success(object data)
        {
            return new CallToolResult
            {
                IsError = false,
                StructuredContent = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    success = true,
                    data
                }),
                Content = new List<ContentBlock>
                {
                    new TextContentBlock
                    {
                        Text = data?.ToString() ?? "Success"
                    }
                }
            };
        }

        public static CallToolResult Error(string errorCode, string error, string? details = null)
        {
            return new CallToolResult
            {
                IsError = true,
                StructuredContent = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    success = false,
                    error,
                    errorCode,
                    details = IncludeErrorDetails ? details : null
                }),
                Content = new List<ContentBlock>
                {
                    new TextContentBlock
                    {
                        Text = $"{errorCode}: {error}"
                    }
                }
            };
        }

        public static CallToolResult From(object envelope)
        {
            var envelopeJson = JsonConvert.SerializeObject(envelope);
            var structuredContent = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(envelopeJson);
            var successProperty = envelope.GetType().GetProperty("Success");
            var isSuccess = successProperty?.GetValue(envelope) is bool success && success;

            return new CallToolResult
            {
                IsError = !isSuccess,
                StructuredContent = structuredContent,
                Content = new List<ContentBlock>
                {
                    new TextContentBlock
                    {
                        Text = envelopeJson
                    }
                }
            };
        }
    }
}
