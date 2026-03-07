using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using TiaPortalMcpServer.Models;

namespace TiaPortalMcpServer.Tools
{
    /// <summary>
    /// MCP tools for reading and validating external files (CSV, Excel).
    /// Provides data extraction and format validation capabilities.
    /// </summary>
    [McpServerToolType]
    public class FileTools
    {
        private readonly ILogger<FileTools> _logger;
        private readonly Services.FileAdapter _fileAdapter;

        public FileTools(ILogger<FileTools> logger, Services.FileAdapter fileAdapter)
        {
            _logger = logger;
            _fileAdapter = fileAdapter;
        }

        /// <summary>
        /// Reads a CSV file and returns structured data (rows with column headers as keys).
        /// Supports custom delimiters. Path must be within allowed roots for security.
        /// </summary>
        [McpServerTool, Description("Read a CSV file and extract data as structured rows")]
        public string files_read_csv(
            [Description("Absolute or relative path to CSV file")] string filePath,
            [Description("Column delimiter character (default: comma)")] string delimiter = ",")
        {
            try
            {
                _logger.LogInformation("Reading CSV file: {FilePath}", filePath);

                // Validate delimiter
                if (string.IsNullOrEmpty(delimiter) || delimiter.Length != 1)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            "Delimiter must be a single character."));
                }

                var result = _fileAdapter.ReadCsvFile(filePath, delimiter[0]);
                if (!result.Success)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            result.Error));
                }

                var data = result.Data;
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        rowCount = data.Count,
                        columnCount = data.Count > 0 ? data[0].Keys.Count : 0,
                        headers = data.Count > 0 ? data[0].Keys.ToList() : new List<string>(),
                        rows = data
                    }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading CSV file: {FilePath}", filePath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        "Unexpected error reading CSV file.",
                        ex.Message));
            }
        }

        /// <summary>
        /// Lists all worksheet names in an Excel file.
        /// </summary>
        [McpServerTool, Description("List all worksheet names in an Excel file")]
        public string files_list_sheets(
            [Description("Absolute or relative path to Excel file (.xlsx or .xls)")] string filePath)
        {
            try
            {
                _logger.LogInformation("Listing sheets in Excel file: {FilePath}", filePath);

                var result = _fileAdapter.ListExcelSheets(filePath);
                if (!result.Success)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            result.Error));
                }

                var sheets = result.Data;
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        sheetCount = sheets.Count,
                        sheets = sheets
                    }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing Excel sheets: {FilePath}", filePath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        "Unexpected error listing Excel sheets.",
                        ex.Message));
            }
        }

        /// <summary>
        /// Reads data from a specific worksheet in an Excel file.
        /// Returns structured data with headers from the first row.
        /// </summary>
        [McpServerTool, Description("Read a worksheet from an Excel file and extract data as structured rows")]
        public string files_read_excel(
            [Description("Absolute or relative path to Excel file (.xlsx or .xls)")] string filePath,
            [Description("Worksheet name to read from")] string sheetName)
        {
            try
            {
                _logger.LogInformation("Reading Excel sheet {SheetName} from {FilePath}", sheetName, filePath);

                if (string.IsNullOrWhiteSpace(sheetName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            "Sheet name cannot be empty."));
                }

                var result = _fileAdapter.ReadExcelSheet(filePath, sheetName);
                if (!result.Success)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            result.Error));
                }

                var data = result.Data;
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        rowCount = data.Count,
                        columnCount = data.Count > 0 ? data[0].Keys.Count : 0,
                        headers = data.Count > 0 ? data[0].Keys.ToList() : new List<string>(),
                        rows = data
                    }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Excel sheet {SheetName} from {FilePath}", sheetName, filePath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Unexpected error reading Excel sheet '{sheetName}'.",
                        ex.Message));
            }
        }

        /// <summary>
        /// Validates CSV file format and structure.
        /// Checks for consistent column count, valid headers, and syntax errors.
        /// </summary>
        [McpServerTool, Description("Validate CSV file format and report any structural issues")]
        public string files_validate_format(
            [Description("Absolute or relative path to CSV file")] string filePath,
            [Description("Column delimiter character (default: comma)")] string delimiter = ",")
        {
            try
            {
                _logger.LogInformation("Validating CSV format: {FilePath}", filePath);

                // Validate delimiter
                if (string.IsNullOrEmpty(delimiter) || delimiter.Length != 1)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            "Delimiter must be a single character."));
                }

                var result = _fileAdapter.ValidateCsvFormat(filePath, delimiter[0]);
                if (!result.Success)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            result.Error));
                }

                var report = result.Data;
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        isValid = report.IsValid,
                        rowCount = report.RowCount,
                        columnCount = report.ColumnCount,
                        headers = report.Headers,
                        errors = report.Errors.Count > 0 ? report.Errors : null
                    }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating CSV format: {FilePath}", filePath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        "Unexpected error validating CSV format.",
                        ex.Message));
            }
        }

        /// <summary>
        /// Gets file information without loading the entire file.
        /// Useful for checking file size, existence, and basic properties.
        /// </summary>
        [McpServerTool, Description("Get basic file information (size, last modified, type)")]
        public string files_get_info(
            [Description("Absolute or relative path to file")] string filePath)
        {
            try
            {
                _logger.LogInformation("Getting file info: {FilePath}", filePath);

                var pathResult = _fileAdapter.ValidateFilePath(filePath);
                if (!pathResult.Success)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            pathResult.Error));
                }

                var fullPath = pathResult.Data;
                var fileInfo = new System.IO.FileInfo(fullPath);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        path = fullPath,
                        exists = fileInfo.Exists,
                        sizeBytes = fileInfo.Length,
                        sizeKB = Math.Round(fileInfo.Length / 1024.0, 2),
                        extension = fileInfo.Extension,
                        lastModified = fileInfo.LastWriteTimeUtc,
                        isReadable = IsFileReadable(fullPath)
                    }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file info: {FilePath}", filePath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        "Unexpected error getting file information.",
                        ex.Message));
            }
        }

        /// <summary>
        /// Helper method to check if a file is readable.
        /// </summary>
        private bool IsFileReadable(string filePath)
        {
            try
            {
                using (var file = System.IO.File.OpenRead(filePath))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
