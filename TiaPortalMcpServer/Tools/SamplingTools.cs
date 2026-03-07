using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    /// <summary>
    /// MCP server tools for requesting LLM sampling from the client.
    /// Provides functionality to generate code, summarize content, and get AI assistance
    /// for TIA Portal development tasks.
    /// </summary>
    [McpServerToolType]
    public class SamplingTools
    {
        private readonly ILogger<SamplingTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;

        /// <summary>
        /// Initializes a new instance of the SamplingTools class.
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic logging</param>
        /// <param name="sessionManager">Session manager for TIA Portal access</param>
        public SamplingTools(
            ILogger<SamplingTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Generates TIA Portal Openness API code for a given task description.
        /// </summary>
        /// <param name="server">The MCP server instance for sampling requests</param>
        /// <param name="taskDescription">Description of the TIA Portal task to generate code for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated code snippet</returns>
        [McpServerTool, Description("Generate TIA Portal Openness API code for a given task")]
        public async Task<string> sampling_generate_code(
            McpServer server,
            [Description("Description of the TIA Portal task to generate code for")] string taskDescription,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("sampling_generate_code called with task: {Task}", taskDescription);

            try
            {
                var samplingParams = new CreateMessageRequestParams
                {
                    Messages = new List<SamplingMessage>
                    {
                        new SamplingMessage
                        {
                            Role = Role.User,
                            Content = new List<ContentBlock>
                            {
                                new TextContentBlock
                                {
                                    Text = $"Generate C# code using TIA Portal Openness API for the following task: {taskDescription}. " +
                                           "Use proper error handling, logging, and follow the patterns from existing TIA Portal MCP Server tools. " +
                                           "Include necessary using statements and make the code production-ready."
                                }
                            }
                        }
                    },
                    SystemPrompt = "You are an expert in TIA Portal Openness API and C# development. " +
                                   "Generate clean, well-documented code that follows best practices.",
                    MaxTokens = 1000,
                    Temperature = 0.3f
                };

                var result = await server.SampleAsync(samplingParams, cancellationToken);

                var generatedCode = result.Content
                    .OfType<TextContentBlock>()
                    .FirstOrDefault()?.Text ?? "No code generated";

                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateSuccess(generatedCode)
                );
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("sampling"))
            {
                _logger.LogWarning(ex, "Sampling not supported by client");
                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateError(
                        ErrorCodes.OperationNotSupported,
                        "LLM sampling is not supported by the connected client. This feature requires a client with sampling capabilities."
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during code generation sampling");
                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateError(
                        ErrorCodes.InternalError,
                        $"Failed to generate code: {ex.Message}"
                    )
                );
            }
        }

        /// <summary>
        /// Summarizes TIA Portal project structure and provides insights.
        /// </summary>
        /// <param name="server">The MCP server instance for sampling requests</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Project summary and insights</returns>
        [McpServerTool, Description("Summarize the current TIA Portal project structure and provide insights")]
        public async Task<string> sampling_summarize_project(
            McpServer server,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("sampling_summarize_project called");

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<string>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use projects_open first."
                        )
                    );
                }

                // Gather project information
                var projectInfo = new
                {
                    Name = project.Name,
                    Path = project.Path?.ToString(),
                    DeviceCount = project.Devices.Count(),
                    PlcSoftwareCount = project.Devices
                        .SelectMany(d => d.DeviceItems)
                        .OfType<PlcSoftware>()
                        .Count(),
                    HmiTargetCount = project.Devices
                        .SelectMany(d => d.DeviceItems)
                        .OfType<Software>()
                        .Count(s => !(s is PlcSoftware)),
                };

                var projectDescription = $"Project Name: {projectInfo.Name}\n" +
                                       $"Path: {projectInfo.Path}\n" +
                                       $"Devices: {projectInfo.DeviceCount}\n" +
                                       $"PLC Software: {projectInfo.PlcSoftwareCount}\n" +
                                       $"HMI Targets: {projectInfo.HmiTargetCount}";

                var samplingParams = new CreateMessageRequestParams
                {
                    Messages = new List<SamplingMessage>
                    {
                        new SamplingMessage
                        {
                            Role = Role.User,
                            Content = new List<ContentBlock>
                            {
                                new TextContentBlock
                                {
                                    Text = $"Analyze this TIA Portal project structure and provide insights:\n\n{projectDescription}\n\n" +
                                           "Provide a summary of the project architecture, potential areas for optimization, " +
                                           "and suggestions for best practices in TIA Portal development."
                                }
                            }
                        }
                    },
                    SystemPrompt = "You are an expert TIA Portal engineer. Provide insightful analysis of project structures.",
                    MaxTokens = 800,
                    Temperature = 0.4f
                };

                var result = await server.SampleAsync(samplingParams, cancellationToken);

                var summary = result.Content
                    .OfType<TextContentBlock>()
                    .FirstOrDefault()?.Text ?? "No summary generated";

                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateSuccess(summary)
                );
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("sampling"))
            {
                _logger.LogWarning(ex, "Sampling not supported by client");
                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateError(
                        ErrorCodes.OperationNotSupported,
                        "LLM sampling is not supported by the connected client."
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during project summarization sampling");
                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateError(
                        ErrorCodes.InternalError,
                        $"Failed to summarize project: {ex.Message}"
                    )
                );
            }
        }

        /// <summary>
        /// Provides intelligent suggestions for TIA Portal development tasks.
        /// </summary>
        /// <param name="server">The MCP server instance for sampling requests</param>
        /// <param name="context">Context or question about TIA Portal development</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>AI-generated suggestions and advice</returns>
        [McpServerTool, Description("Get intelligent suggestions for TIA Portal development tasks")]
        public async Task<string> sampling_get_suggestions(
            McpServer server,
            [Description("Context or question about TIA Portal development")] string context,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("sampling_get_suggestions called with context: {Context}", context);

            try
            {
                var samplingParams = new CreateMessageRequestParams
                {
                    Messages = new List<SamplingMessage>
                    {
                        new SamplingMessage
                        {
                            Role = Role.User,
                            Content = new List<ContentBlock>
                            {
                                new TextContentBlock
                                {
                                    Text = $"Provide expert advice for this TIA Portal development question/context: {context}\n\n" +
                                           "Consider best practices, performance implications, and practical implementation tips."
                                }
                            }
                        }
                    },
                    SystemPrompt = "You are a senior TIA Portal engineer with extensive experience in industrial automation. " +
                                   "Provide practical, actionable advice based on industry best practices.",
                    MaxTokens = 600,
                    Temperature = 0.5f
                };

                var result = await server.SampleAsync(samplingParams, cancellationToken);

                var suggestions = result.Content
                    .OfType<TextContentBlock>()
                    .FirstOrDefault()?.Text ?? "No suggestions generated";

                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateSuccess(suggestions)
                );
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("sampling"))
            {
                _logger.LogWarning(ex, "Sampling not supported by client");
                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateError(
                        ErrorCodes.OperationNotSupported,
                        "LLM sampling is not supported by the connected client."
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during suggestions sampling");
                return JsonConvert.SerializeObject(
                    ToolResponse<string>.CreateError(
                        ErrorCodes.InternalError,
                        $"Failed to get suggestions: {ex.Message}"
                    )
                );
            }
        }
    }
}
