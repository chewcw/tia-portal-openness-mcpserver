using Xunit;
using TiaPortalMcpServer;

namespace TiaPortalMcpServer.Tests
{
    public class McpTransportOptionsTests
    {
        [Fact]
        public void McpTransportOptions_Defaults_AreCorrect()
        {
            var opts = new McpTransportOptions();

            Assert.Equal("127.0.0.1", opts.Host);
            Assert.Equal(3000, opts.Port);
        }
    }
}
