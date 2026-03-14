using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using TiaPortalMcpServer.Models;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class UserInteractionTools
    {
        private readonly ILogger<UserInteractionTools> _logger;

        public UserInteractionTools(ILogger<UserInteractionTools> logger)
        {
            _logger = logger;
        }

        [McpServerTool, Description("Ask the user for missing information using MCP elicitation. Returns a key/value object with the user's responses.")]
        public async Task<string> utilities_elicit_user_input(
            McpServer server,
            [Description("Elicitation request containing message and fields")] ElicitationRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("utilities_elicit_user_input called with {FieldCount} fields", request?.Fields?.Count ?? 0);

            try
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                if (request == null || request.Fields == null || request.Fields.Count == 0)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            "No fields provided for elicitation."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema();
                var required = new List<string>();

                foreach (var field in request.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Name))
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<object>.CreateError(
                                ErrorCodes.InvalidParameter,
                                "Each field must include a non-empty name."
                            )
                        );
                    }

                    var normalizedType = (field.Type ?? "string").Trim().ToLowerInvariant();
                    ElicitRequestParams.PrimitiveSchemaDefinition definition = normalizedType switch
                    {
                        "boolean" or "bool" => new ElicitRequestParams.BooleanSchema
                        {
                            Description = field.Description
                        },
                        "number" or "float" or "double" or "int" or "integer" => new ElicitRequestParams.NumberSchema
                        {
                            Description = field.Description
                        },
                        _ => new ElicitRequestParams.StringSchema
                        {
                            Description = field.Description
                        }
                    };

                    schema.Properties[field.Name] = definition;
                    if (field.Required)
                    {
                        required.Add(field.Name);
                    }
                }

                schema.Required = required.Count > 0 ? required : null;

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = request.Message,
                    RequestedSchema = schema
                }, cancellationToken);

                if (!string.Equals(response.Action, "accept", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.UserCancelled,
                            "User cancelled or declined the request."
                        )
                    );
                }

                var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (response.Content != null)
                {
                    foreach (var entry in response.Content)
                    {
                        result[entry.Key] = ConvertJsonElement(entry.Value);
                    }
                }

                return JsonConvert.SerializeObject(
                    ToolResponse<Dictionary<string, object?>>.CreateSuccess(result)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user elicitation");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InternalError,
                        $"Failed to elicit user input: {ex.Message}"
                    )
                );
            }
        }

        private static object? ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in element.EnumerateObject())
                    {
                        obj[property.Name] = ConvertJsonElement(property.Value);
                    }
                    return obj;
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElement(item));
                    }
                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var longValue))
                    {
                        return longValue;
                    }
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                default:
                    return null;
            }
        }
    }
}
