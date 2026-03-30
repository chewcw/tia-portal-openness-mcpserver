using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using ModelContextProtocol.Server;
using Xunit;

namespace TiaPortalMcpServer.Tests
{
    [Trait("Category", "Unit")]
    public class ToolContractTests
    {
        private static readonly Regex ToolNamePattern = new Regex(
            "^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Type[] ExplicitNameRequiredToolTypes =
        {
            typeof(TiaPortalMcpServer.DeviceItemTools),
            typeof(TiaPortalMcpServer.SoftwareTools)
        };

        [Fact]
        public void Tool_names_follow_namespace_action_pattern()
        {
            var assembly = typeof(TiaPortalMcpServer.Program).Assembly;

            var invalidNames = new List<string>();

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<McpServerToolTypeAttribute>() != null))
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                    if (toolAttr == null)
                    {
                        continue;
                    }

                    var resolvedName = string.IsNullOrWhiteSpace(toolAttr.Name)
                        ? method.Name
                        : toolAttr.Name;

                    if (!ToolNamePattern.IsMatch(resolvedName))
                    {
                        invalidNames.Add(string.Format("{0}.{1} => '{2}'", type.Name, method.Name, resolvedName));
                    }
                }
            }

            invalidNames = invalidNames.OrderBy(x => x).ToList();

            Assert.True(
                invalidNames.Count == 0,
                "Invalid MCP tool names found:\n" + string.Join("\n", invalidNames));
        }

        [Fact]
        public void Monitored_tools_require_explicit_name_attribute()
        {
            var missingExplicitNames = new List<string>();

            foreach (var type in ExplicitNameRequiredToolTypes)
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                    if (toolAttr == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(toolAttr.Name))
                    {
                        missingExplicitNames.Add(string.Format("{0}.{1}", type.Name, method.Name));
                    }
                }
            }

            missingExplicitNames = missingExplicitNames.OrderBy(x => x).ToList();

            Assert.True(
                missingExplicitNames.Count == 0,
                "Monitored MCP tools must declare McpServerTool(Name = ...). Missing on:\n" + string.Join("\n", missingExplicitNames));
        }
    }
}
