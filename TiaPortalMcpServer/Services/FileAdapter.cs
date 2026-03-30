using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using OfficeOpenXml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TiaPortalMcpServer.Services
{
    /// <summary>
    /// Provides unified file I/O abstraction for reading CSV, Excel, and other files.
    /// Handles security validation, encoding detection, and structured data extraction.
    /// </summary>
    public class FileAdapter
    {
        private readonly ILogger<FileAdapter> _logger;
        private readonly IConfiguration _configuration;
        private readonly long _maxFileSizeBytes;
        private readonly List<string> _allowedRootPaths;

        public FileAdapter(ILogger<FileAdapter> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Default 10MB, configurable via appsettings
            var maxSizeMb = _configuration.GetValue("FileHandling:MaxFileSizeMB", 10);
            _maxFileSizeBytes = maxSizeMb * 1024 * 1024;

            // Get allowed root paths from config; default to current directory if not configured
            _allowedRootPaths = _configuration
                .GetSection("FileHandling:AllowedRootPaths")
                .Get<List<string>>() ?? new List<string> { Directory.GetCurrentDirectory() };

            _logger.LogInformation("FileAdapter initialized with {AllowedPathsCount} allowed root paths, max file size {MaxSizeMB}MB",
                _allowedRootPaths.Count, maxSizeMb);
        }

        /// <summary>
        /// Validates a file path for security and accessibility.
        /// Checks for path traversal, file existence, and size constraints.
        /// </summary>
        public Result<string> ValidateFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return Result<string>.CreateError("File path cannot be empty.");

            try
            {
                var fullPath = Path.GetFullPath(filePath);

                // Check if path contains suspicious patterns
                if (filePath.Contains("..") || filePath.Contains("..\\") || filePath.Contains("../"))
                    return Result<string>.CreateError("Path traversal patterns are not allowed.");

                // Verify path is within allowed roots
                var isAllowed = _allowedRootPaths.Any(root => IsPathWithinRoot(fullPath, root));

                if (!isAllowed)
                    return Result<string>.CreateError(
                        $"File path '{fullPath}' is outside allowed root directories: {string.Join(", ", _allowedRootPaths)}");

                // Check file exists
                if (!File.Exists(fullPath))
                    return Result<string>.CreateError($"File '{fullPath}' does not exist.");

                // Check file size
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > _maxFileSizeBytes)
                    return Result<string>.CreateError(
                        $"File size {fileInfo.Length / (1024 * 1024)}MB exceeds maximum allowed size {_maxFileSizeBytes / (1024 * 1024)}MB.");

                _logger.LogInformation("File path validated successfully: {FilePath}", fullPath);
                return Result<string>.CreateSuccess(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file path: {FilePath}", filePath);
                return Result<string>.CreateError($"Error validating file path: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects the encoding of a text file using byte order marks and heuristics.
        /// Falls back to UTF-8 if detection fails.
        /// </summary>
        public Encoding DetectFileEncoding(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return Encoding.UTF8;

            try
            {
                // Read first few bytes to detect BOM
                using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4))
                {
                    var buffer = new byte[4];
                    file.Read(buffer, 0, 4);

                    // UTF-8 BOM: EF BB BF
                    if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    {
                        _logger.LogInformation("Detected UTF-8 with BOM in {FilePath}", filePath);
                        return Encoding.UTF8;
                    }

                    // UTF-16 LE BOM: FF FE
                    if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    {
                        _logger.LogInformation("Detected UTF-16 LE with BOM in {FilePath}", filePath);
                        return Encoding.Unicode;
                    }

                    // UTF-16 BE BOM: FE FF
                    if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    {
                        _logger.LogInformation("Detected UTF-16 BE with BOM in {FilePath}", filePath);
                        return Encoding.BigEndianUnicode;
                    }
                }

                // Default to UTF-8 if no BOM detected
                _logger.LogInformation("No encoding BOM detected in {FilePath}, using UTF-8 fallback", filePath);
                return Encoding.UTF8;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting encoding for {FilePath}, using UTF-8 fallback", filePath);
                return Encoding.UTF8;
            }
        }

        /// <summary>
        /// Reads a CSV file and returns structured data as a list of dictionaries.
        /// Keys are column headers; values are cell content.
        /// </summary>
        public Result<List<Dictionary<string, string>>> ReadCsvFile(string filePath, char delimiter = ',')
        {
            var validationResult = ValidateFilePath(filePath);
            if (!validationResult.Success)
                return Result<List<Dictionary<string, string>>>.CreateError(validationResult.Error ?? "Validation failed");

            try
            {
                var fullPath = validationResult.Data ?? filePath;
                var encoding = DetectFileEncoding(fullPath);
                var records = new List<Dictionary<string, string>>();

                using (var reader = new StreamReader(fullPath, encoding))
                {
                    var csvConfig = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
                    {
                        Delimiter = delimiter.ToString()
                    };
                    using (var csv = new CsvReader(reader, csvConfig))
                    {
                        if (!csv.Read())
                        {
                            _logger.LogWarning("CSV file {FilePath} is empty", fullPath);
                            return Result<List<Dictionary<string, string>>>.CreateSuccess(records);
                        }

                        csv.ReadHeader();

                        if (csv.HeaderRecord == null || csv.HeaderRecord.Length == 0)
                        {
                            _logger.LogWarning("CSV file {FilePath} has no headers", fullPath);
                            return Result<List<Dictionary<string, string>>>.CreateSuccess(records);
                        }

                        while (csv.Read())
                        {
                            var row = new Dictionary<string, string>();
                            foreach (var header in csv.HeaderRecord)
                            {
                                row[header] = csv.GetField(header) ?? string.Empty;
                            }
                            records.Add(row);
                        }
                    }
                }

                _logger.LogInformation("CSV file {FilePath} read successfully with {RecordCount} records",
                    fullPath, records.Count);
                return Result<List<Dictionary<string, string>>>.CreateSuccess(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading CSV file: {FilePath}", filePath);
                return Result<List<Dictionary<string, string>>>.CreateError($"Error reading CSV file: {ex.Message}");
            }
        }

        /// <summary>
        /// Lists all worksheet names in an Excel file.
        /// </summary>
        public Result<List<string>> ListExcelSheets(string filePath)
        {
            var validationResult = ValidateFilePath(filePath);
            if (!validationResult.Success)
                return Result<List<string>>.CreateError(validationResult.Error ?? "Validation failed");

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage(new FileInfo(validationResult.Data ?? filePath)))
                {
                    var sheetNames = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();
                    _logger.LogInformation("Excel file {FilePath} has {SheetCount} sheets", validationResult.Data, sheetNames.Count);
                    return Result<List<string>>.CreateSuccess(sheetNames);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing Excel sheets: {FilePath}", filePath);
                return Result<List<string>>.CreateError($"Error listing Excel sheets: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads data from a specific sheet in an Excel file.
        /// Returns structured data as a list of dictionaries (headers from row 1).
        /// </summary>
        public Result<List<Dictionary<string, string>>> ReadExcelSheet(string filePath, string? sheetName)
        {
            var validationResult = ValidateFilePath(filePath);
            if (!validationResult.Success)
                return Result<List<Dictionary<string, string>>>.CreateError(validationResult.Error ?? "Validation failed");

            try
            {
                if (string.IsNullOrWhiteSpace(sheetName))
                    return Result<List<Dictionary<string, string>>>.CreateError("Sheet name cannot be empty");

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage(new FileInfo(validationResult.Data ?? filePath)))
                {
                    var worksheet = package.Workbook.Worksheets[sheetName];
                    if (worksheet == null)
                        return Result<List<Dictionary<string, string>>>.CreateError(
                            $"Sheet '{sheetName}' not found in Excel file.");

                    var records = new List<Dictionary<string, string>>();

                    // Read headers from first row
                    var headers = new List<string>();
                    for (int col = 1; col <= (worksheet.Dimension?.Columns ?? 0); col++)
                    {
                        headers.Add(worksheet.Cells[1, col].Value?.ToString() ?? $"Column{col}");
                    }

                    // Read data rows starting from row 2
                    if (worksheet.Dimension != null)
                    {
                        for (int row = 2; row <= worksheet.Dimension.Rows; row++)
                        {
                            var rowDict = new Dictionary<string, string>();
                            for (int col = 1; col <= worksheet.Dimension.Columns; col++)
                            {
                                var header = headers[col - 1];
                                rowDict[header] = worksheet.Cells[row, col].Value?.ToString() ?? string.Empty;
                            }
                            records.Add(rowDict);
                        }
                    }

                    _logger.LogInformation("Excel sheet {SheetName} from {FilePath} read successfully with {RecordCount} records",
                        sheetName, validationResult.Data, records.Count);
                    return Result<List<Dictionary<string, string>>>.CreateSuccess(records);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Excel sheet {SheetName} from {FilePath}", sheetName, filePath);
                return Result<List<Dictionary<string, string>>>.CreateError(
                    $"Error reading Excel sheet '{sheetName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Validates CSV file format and structure without fully parsing.
        /// Checks for consistent column count and basic syntax.
        /// </summary>
        public Result<ValidationReport> ValidateCsvFormat(string filePath, char delimiter = ',')
        {
            var validationResult = ValidateFilePath(filePath);
            if (!validationResult.Success)
                return Result<ValidationReport>.CreateError(validationResult.Error ?? "Validation failed");

            try
            {
                var fullPath = validationResult.Data ?? filePath;
                var encoding = DetectFileEncoding(fullPath);
                var report = new ValidationReport { IsValid = true, Errors = new List<string>() };

                using (var reader = new StreamReader(fullPath, encoding))
                {
                    var csvConfig = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
                    {
                        Delimiter = delimiter.ToString()
                    };
                    using (var csv = new CsvReader(reader, csvConfig))
                    {
                        if (!csv.Read())
                        {
                            report.IsValid = false;
                            report.Errors.Add("CSV file has no headers or is empty.");
                            return Result<ValidationReport>.CreateSuccess(report);
                        }

                        csv.ReadHeader();

                        if (csv.HeaderRecord == null || csv.HeaderRecord.Length == 0)
                        {
                            report.IsValid = false;
                            report.Errors.Add("CSV file has no headers or is empty.");
                            return Result<ValidationReport>.CreateSuccess(report);
                        }

                        var expectedColumnCount = csv.HeaderRecord.Length;
                        var rowCount = 0;

                        while (csv.Read())
                        {
                            rowCount++;
                            if (csv.Parser.Count != expectedColumnCount)
                            {
                                report.IsValid = false;
                                report.Errors.Add($"Row {rowCount + 1} has {csv.Parser.Count} columns, expected {expectedColumnCount}.");
                            }
                        }

                        report.RowCount = rowCount;
                        report.ColumnCount = expectedColumnCount;
                        report.Headers = csv.HeaderRecord?.ToList();

                        _logger.LogInformation("CSV validation complete: {FilePath} - Valid: {IsValid}, Rows: {RowCount}, Columns: {ColumnCount}",
                            fullPath, report.IsValid, report.RowCount, report.ColumnCount);
                    }
                }

                return Result<ValidationReport>.CreateSuccess(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating CSV file: {FilePath}", filePath);
                return Result<ValidationReport>.CreateError($"Error validating CSV file: {ex.Message}");
            }
        }

        /// <summary>
        /// Writes data to a CSV file. Overwrites existing file.
        /// </summary>
        public Result<string> WriteCsvFile(string filePath, List<Dictionary<string, string>> data)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return Result<string>.CreateError("File path cannot be empty.");

            try
            {
                var outputPath = Path.GetFullPath(filePath);
                var outputDir = Path.GetDirectoryName(outputPath);

                if (string.IsNullOrEmpty(outputDir))
                    return Result<string>.CreateError("Invalid output directory");

                // Verify output directory is within allowed roots
                var isAllowed = _allowedRootPaths.Any(root => IsPathWithinRoot(outputDir, root));

                if (!isAllowed)
                    return Result<string>.CreateError(
                        $"Output directory '{outputDir}' is outside allowed root directories: {string.Join(", ", _allowedRootPaths)}");

                // Ensure directory exists
                Directory.CreateDirectory(outputDir);

                using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
                using (var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture))
                {
                    if (data.Count > 0)
                    {
                        // Write headers
                        var headers = data[0].Keys;
                        foreach (var header in headers)
                        {
                            csv.WriteField(header);
                        }
                        csv.NextRecord();

                        // Write data rows
                        foreach (var row in data)
                        {
                            foreach (var header in headers)
                            {
                                csv.WriteField(row.ContainsKey(header) ? row[header] : string.Empty);
                            }
                            csv.NextRecord();
                        }
                    }
                }

                _logger.LogInformation("CSV file written successfully: {FilePath} with {RecordCount} records", outputPath, data.Count);
                return Result<string>.CreateSuccess(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing CSV file: {FilePath}", filePath);
                return Result<string>.CreateError($"Error writing CSV file: {ex.Message}");
            }
        }

        private static bool IsPathWithinRoot(string path, string root)
        {
            var fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var rootWithSeparator = fullRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Generic result wrapper for success/error handling.
    /// </summary>
    public class Result<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }

        public static Result<T> CreateSuccess(T data) => new() { Success = true, Data = data };
        public static Result<T> CreateError(string error) => new() { Success = false, Error = error };
    }

    /// <summary>
    /// Validation report for CSV/Excel file format checks.
    /// </summary>
    public class ValidationReport
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public List<string>? Headers { get; set; }
    }
}
