using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using TiaPortalMcpServer.Services;
using Xunit;

namespace TiaPortalMcpServer.Tests
{
    public class FileAdapterTests : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly FileAdapter _fileAdapter;
        private readonly string _testDirectory;
        private Microsoft.Extensions.Logging.ILogger _logger;

        public FileAdapterTests()
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Create test directory in a safe location
            _testDirectory = Path.Combine(Path.GetTempPath(), $"FileAdapterTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDirectory);

            // Setup DI with FileAdapter
            var configBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "FileHandling:MaxFileSizeMB", "10" },
                    { "FileHandling:AllowedRootPaths:0", _testDirectory }
                });

            var config = configBuilder.Build();
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog());
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<FileAdapter>();

            _serviceProvider = services.BuildServiceProvider();
            _fileAdapter = _serviceProvider.GetRequiredService<FileAdapter>();
            _logger = _serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileAdapterTests>>();
        }

        #region Path Validation Tests

        [Fact]
        public void ValidateFilePath_WithEmptyPath_ReturnsError()
        {
            // Arrange & Act
            var result = _fileAdapter.ValidateFilePath("");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateFilePath_WithNullPath_ReturnsError()
        {
            // Arrange & Act
            var result = _fileAdapter.ValidateFilePath(null);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateFilePath_WithPathTraversal_ReturnsError()
        {
            // Arrange & Act
            var result = _fileAdapter.ValidateFilePath("../../../etc/passwd");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateFilePath_WithNonExistentFile_ReturnsError()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.csv");

            // Act
            var result = _fileAdapter.ValidateFilePath(nonExistentPath);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("does not exist", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateFilePath_WithValidFile_ReturnsSuccess()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.txt");
            File.WriteAllText(testFile, "test content");

            // Act
            var result = _fileAdapter.ValidateFilePath(testFile);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void ValidateFilePath_WithFileOutsideAllowedRoot_ReturnsError()
        {
            // Arrange
            var outsidePath = Path.Combine(Path.GetTempPath(), "outside", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outsidePath));
            File.WriteAllText(outsidePath, "test");

            // Act
            var result = _fileAdapter.ValidateFilePath(outsidePath);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("outside allowed", result.Error, StringComparison.OrdinalIgnoreCase);

            // Cleanup
            File.Delete(outsidePath);
            Directory.Delete(Path.GetDirectoryName(outsidePath));
        }

        #endregion

        #region Encoding Detection Tests

        [Fact]
        public void DetectFileEncoding_WithUTF8WithoutBOM_ReturnsUTF8()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "utf8_no_bom.txt");
            File.WriteAllText(testFile, "Hello UTF8", Encoding.UTF8);

            // Act
            var encoding = _fileAdapter.DetectFileEncoding(testFile);

            // Assert
            Assert.NotNull(encoding);
            Assert.Equal(Encoding.UTF8.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectFileEncoding_WithInvalidPath_ReturnsFallbackUTF8()
        {
            // Arrange
            var invalidPath = Path.Combine(_testDirectory, "nonexistent.txt");

            // Act
            var encoding = _fileAdapter.DetectFileEncoding(invalidPath);

            // Assert
            Assert.NotNull(encoding);
            Assert.Equal(Encoding.UTF8.CodePage, encoding.CodePage);
        }

        #endregion

        #region CSV Reading Tests

        [Fact]
        public void ReadCsvFile_WithValidCSV_ReturnsStructuredData()
        {
            // Arrange
            var csvFile = Path.Combine(_testDirectory, "test.csv");
            var csvContent = "Name,Age,City\nJohn,30,NYC\nJane,25,LA";
            File.WriteAllText(csvFile, csvContent);

            // Act
            var result = _fileAdapter.ReadCsvFile(csvFile);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.Count); // 2 data rows
            Assert.Contains("Name", result.Data[0].Keys);
            Assert.Equal("John", result.Data[0]["Name"]);
        }

        [Fact]
        public void ReadCsvFile_WithCustomDelimiter_ReturnsStructuredData()
        {
            // Arrange
            var csvFile = Path.Combine(_testDirectory, "test_tab.csv");
            var csvContent = "Name\tAge\tCity\nJohn\t30\tNYC";
            File.WriteAllText(csvFile, csvContent);

            // Act
            var result = _fileAdapter.ReadCsvFile(csvFile, '\t');

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data.Count);
            Assert.Equal("30", result.Data[0]["Age"]);
        }

        [Fact]
        public void ReadCsvFile_WithEmptyCSV_ReturnsSuccessWithNoRows()
        {
            // Arrange
            var csvFile = Path.Combine(_testDirectory, "empty.csv");
            File.WriteAllText(csvFile, "Name,Age\n");

            // Act
            var result = _fileAdapter.ReadCsvFile(csvFile);

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.Data);
        }

        [Fact]
        public void ReadCsvFile_WithNonExistentFile_ReturnsError()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.csv");

            // Act
            var result = _fileAdapter.ReadCsvFile(nonExistentFile);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        #endregion

        #region CSV Validation Tests

        [Fact]
        public void ValidateCsvFormat_WithValidCSV_ReturnsValidReport()
        {
            // Arrange
            var csvFile = Path.Combine(_testDirectory, "valid.csv");
            var csvContent = "Name,Age\nJohn,30\nJane,25";
            File.WriteAllText(csvFile, csvContent);

            // Act
            var result = _fileAdapter.ValidateCsvFormat(csvFile);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.Data.IsValid);
            Assert.Equal(2, result.Data.RowCount);
            Assert.Equal(2, result.Data.ColumnCount);
            Assert.Empty(result.Data.Errors);
        }

        [Fact]
        public void ValidateCsvFormat_WithInconsistentColumns_ReturnsInvalidReport()
        {
            // Arrange
            var csvFile = Path.Combine(_testDirectory, "inconsistent.csv");
            var csvContent = "Name,Age\nJohn,30,Extra\nJane,25"; // Row 1 has 3 columns, row 2 has 2
            File.WriteAllText(csvFile, csvContent);

            // Act
            var result = _fileAdapter.ValidateCsvFormat(csvFile);

            // Assert
            Assert.True(result.Success);
            Assert.False(result.Data.IsValid);
            Assert.NotEmpty(result.Data.Errors);
        }

        [Fact]
        public void ValidateCsvFormat_WithEmptyFile_ReturnsInvalidReport()
        {
            // Arrange
            var csvFile = Path.Combine(_testDirectory, "empty_headers.csv");
            File.WriteAllText(csvFile, "");

            // Act
            var result = _fileAdapter.ValidateCsvFormat(csvFile);

            // Assert
            Assert.True(result.Success);
            Assert.False(result.Data.IsValid);
            Assert.NotEmpty(result.Data.Errors);
        }

        #endregion

        #region CSV Writing Tests

        [Fact]
        public void WriteCsvFile_WithValidData_CreatesFile()
        {
            // Arrange
            var outputFile = Path.Combine(_testDirectory, "output.csv");
            var data = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Name", "John" }, { "Age", "30" } },
                new Dictionary<string, string> { { "Name", "Jane" }, { "Age", "25" } }
            };

            // Act
            var result = _fileAdapter.WriteCsvFile(outputFile, data);

            // Assert
            Assert.True(result.Success);
            Assert.True(File.Exists(outputFile));

            // Verify content
            var content = File.ReadAllText(outputFile);
            Assert.Contains("Name", content);
            Assert.Contains("John", content);
        }

        [Fact]
        public void WriteCsvFile_WithEmptyData_CreatesEmptyFile()
        {
            // Arrange
            var outputFile = Path.Combine(_testDirectory, "empty_output.csv");
            var data = new List<Dictionary<string, string>>();

            // Act
            var result = _fileAdapter.WriteCsvFile(outputFile, data);

            // Assert
            Assert.True(result.Success);
            Assert.True(File.Exists(outputFile));
        }

        #endregion

        #region Excel Tests (Basic)

        [Fact]
        public void ListExcelSheets_WithNonExistentFile_ReturnsError()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.xlsx");

            // Act
            var result = _fileAdapter.ListExcelSheets(nonExistentFile);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public void ReadExcelSheet_WithNonExistentFile_ReturnsError()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.xlsx");

            // Act
            var result = _fileAdapter.ReadExcelSheet(nonExistentFile, "Sheet1");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        #endregion

        public void Dispose()
        {
            // Cleanup test directory
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            (_serviceProvider as IDisposable)?.Dispose();
            Log.CloseAndFlush();
        }
    }
}
