using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering.HW;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class HardwareTools
    {
        private readonly ILogger<HardwareTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;

        public HardwareTools(
            ILogger<HardwareTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }
    }
}
