using FileViewer.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace FileViewerAgent.Services
{
    public class FileService : IFileService
    {
        private readonly Dictionary<string, string> _rootPaths;
        private readonly string[] _allowedExtensions = { ".log", ".txt" };
        private readonly int _maxFileSizeInMB = 10; // Maximum file size to read
        private readonly ILogger<FileService> _logger;
        private readonly ParallelOptions _parallelOptions;

        public FileService(IConfiguration configuration, ILogger<FileService> logger)
        {
            _logger = logger;
            var rootSection = configuration.GetSection("FileSettings:RootPaths");
            _rootPaths = rootSection.Get<Dictionary<string, string>>()
                ?? throw new ArgumentNullException("RootPaths configuration is missing");
            _parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount // Use all available cores
            };
        }

        public IEnumerable<RootDirectory> GetRootDirectories()
        {
            return _rootPaths.Select(kvp => new RootDirectory
            {
                Name = kvp.Key,
                Path = kvp.Value
            });
        }

        public IEnumerable<FileItem> GetDirectoryContents(string rootName, string relativePath = "")
        {
            if (!_rootPaths.TryGetValue(rootName, out var rootPath))
            {
                throw new ArgumentException($"Invalid root directory: {rootName}");
            }

            // Normalize the path separators and remove any potentially harmful characters
            relativePath = NormalizePath(relativePath);

            var fullPath = Path.Combine(rootPath, relativePath);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
            }

            // Ensure we're still within the root path (security check)
            if (!IsPathWithinRoot(fullPath, rootPath))
            {
                throw new UnauthorizedAccessException("Access to this path is not allowed");
            }

            var items = new List<FileItem>();

            // Add directories
            foreach (var dir in Directory.GetDirectories(fullPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                items.Add(new FileItem
                {
                    Name = dirInfo.Name,
                    Path = ConvertToWebPath(Path.GetRelativePath(rootPath, dir)),
                    IsDirectory = true,
                    RootName = rootName,
                    LastModified = dirInfo.LastWriteTime,
                    Size = 0 // Directories don't have a meaningful size
                });
            }

            // Add files with allowed extensions
            foreach (var file in Directory.GetFiles(fullPath))
            {
                var extension = Path.GetExtension(file).ToLower();
                if (_allowedExtensions.Contains(extension))
                {
                    var fileInfo = new FileInfo(file);
                    items.Add(new FileItem
                    {
                        Name = Path.GetFileName(file),
                        Path = ConvertToWebPath(Path.GetRelativePath(rootPath, file)),
                        IsDirectory = false,
                        RootName = rootName,
                        LastModified = fileInfo.LastWriteTime,
                        Size = fileInfo.Length
                    });
                }
            }

            return items.OrderBy(i => !i.IsDirectory).ThenBy(i => i.Name);
        }

        public FileReadResult ReadFileContents(string rootName, string relativePath)
        {
            if (!_rootPaths.TryGetValue(rootName, out var rootPath))
            {
                throw new ArgumentException($"Invalid root directory: {rootName}");
            }

            relativePath = NormalizePath(relativePath);
            var fullPath = Path.Combine(rootPath, relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

            if (!IsPathWithinRoot(fullPath, rootPath))
            {
                throw new UnauthorizedAccessException("Access to this file is not allowed");
            }

            var extension = Path.GetExtension(fullPath).ToLower();
            if (!_allowedExtensions.Contains(extension))
            {
                throw new UnauthorizedAccessException($"File type not allowed: {extension}");
            }

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > _maxFileSizeInMB * 1024 * 1024)
            {
                return new FileReadResult
                {
                    Success = false,
                    ErrorMessage = $"File is too large. Maximum size is {_maxFileSizeInMB}MB.",
                    FileSizeBytes = fileInfo.Length
                };
            }

            try
            {
                // First try to detect the encoding
                var encoding = DetectFileEncoding(fullPath);
                var contents = File.ReadAllText(fullPath, encoding);

                return new FileReadResult
                {
                    Success = true,
                    Contents = contents,
                    Encoding = encoding.WebName,
                    FileSizeBytes = fileInfo.Length
                };
            }
            catch (Exception ex)
            {
                return new FileReadResult
                {
                    Success = false,
                    ErrorMessage = $"Error reading file: {ex.Message}",
                    FileSizeBytes = fileInfo.Length
                };
            }
        }

        public async Task<FileSearchResponse> SearchFilesAsync(FileSearchRequest request)
        {
            try
            {
                if (!_rootPaths.TryGetValue(request.RootName, out var rootPath))
                {
                    throw new ArgumentException($"Invalid root directory: {request.RootName}");
                }

                var searchPath = string.IsNullOrEmpty(request.Path) ?
                    rootPath :
                    Path.Combine(rootPath, request.Path);

                if (!Directory.Exists(searchPath))
                {
                    throw new DirectoryNotFoundException($"Directory not found: {searchPath}");
                }

                var searchResults = new ConcurrentBag<FileSearchResult>();
                var searchPattern = new Regex(Regex.Escape(request.SearchTerm), RegexOptions.IgnoreCase | RegexOptions.Compiled);

                // Get all matching files
                var files = Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _allowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                // Process files in parallel
                await Parallel.ForEachAsync(files, _parallelOptions, async (file, token) =>
                {
                    var result = await SearchFileAsync(file, searchPath, searchPattern);
                    if (result != null)
                    {
                        searchResults.Add(result);
                    }
                });

                return new FileSearchResponse
                {
                    Success = true,
                    Results = searchResults.OrderBy(r => r.FilePath).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching files with pattern {SearchTerm}", request.SearchTerm);
                return new FileSearchResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<FileSearchResult> SearchFileAsync(string file, string basePath, Regex searchPattern)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Length > 10 * 1024 * 1024) // Skip files larger than 10MB
                {
                    _logger.LogWarning("Skipping large file: {FilePath}", file);
                    return null;
                }

                // Read file in chunks for better memory usage
                using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                using var streamReader = new StreamReader(fileStream);

                string line;
                int lineNumber = 0;
                var buffer = new char[4096];
                var currentLine = new StringBuilder();
                var resultLine = string.Empty;
                var matchLineNumber = 0;

                while (true)
                {
                    var bytesRead = await streamReader.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        if (buffer[i] == '\n')
                        {
                            lineNumber++;
                            line = currentLine.ToString().TrimEnd('\r');
                            var match = searchPattern.Match(line);

                            if (match.Success)
                            {
                                resultLine = line;
                                matchLineNumber = lineNumber;
                                break;
                            }

                            currentLine.Clear();
                        }
                        else
                        {
                            currentLine.Append(buffer[i]);
                        }
                    }

                    if (!string.IsNullOrEmpty(resultLine))
                        break;
                }

                if (!string.IsNullOrEmpty(resultLine))
                {
                    var relativePath = Path.GetRelativePath(basePath, file);

                    // Get context around the match
                    var match = searchPattern.Match(resultLine);
                    var startIndex = Math.Max(0, match.Index - 50);
                    var length = Math.Min(resultLine.Length - startIndex, 100);
                    var context = resultLine.Substring(startIndex, length);

                    if (startIndex > 0)
                        context = "..." + context;
                    if (startIndex + length < resultLine.Length)
                        context = context + "...";

                    return new FileSearchResult
                    {
                        FilePath = relativePath.Replace('\\', '/'),
                        FileName = Path.GetFileName(file),
                        LastModified = fileInfo.LastWriteTimeUtc,
                        LineNumber = matchLineNumber,
                        MatchedContent = context
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching file: {FilePath}", file);
            }

            return null;
        }

        private string FormatMobileNumberPattern(string mobileNumber)
        {
            // Remove any non-digit characters from the search term
            var cleanNumber = new string(mobileNumber.Where(char.IsDigit).ToArray());

            if (string.IsNullOrEmpty(cleanNumber))
                throw new ArgumentException("Invalid mobile number format");

            // Create a pattern that matches different mobile number formats
            // For example: "1234567890" should match:
            // 1234567890
            // 123-456-7890
            // (123) 456-7890
            // +1 123.456.7890
            // etc.
            return $@"[^0-9]*{string.Join(@"[^0-9]*", cleanNumber.ToCharArray())}[^0-9]*";
        }

        private Encoding DetectFileEncoding(string filePath)
        {
            // Read the BOM
            var bom = new byte[4];
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0) return Encoding.UTF32; //UTF-32LE
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return new UTF32Encoding(true, true);

            // If no BOM was found, try to detect UTF-8
            using (var reader = new StreamReader(filePath, Encoding.Default, true))
            {
                var sample = new char[4096];
                reader.Read(sample, 0, sample.Length);

                // If StreamReader detected a Unicode encoding, return UTF8
                if (reader.CurrentEncoding != Encoding.Default)
                {
                    return Encoding.UTF8;
                }
            }

            // Default to system default encoding
            return Encoding.Default;
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Replace web path separators with system path separators
            path = path.Replace('/', Path.DirectorySeparatorChar);

            // Remove any potentially harmful characters
            var invalidChars = Path.GetInvalidPathChars();
            return string.Join("", path.Split(invalidChars));
        }

        private string ConvertToWebPath(string path)
        {
            // Convert system path separators to web path separators
            return path.Replace(Path.DirectorySeparatorChar, '/');
        }

        private bool IsPathWithinRoot(string fullPath, string rootPath)
        {
            var normalizedFullPath = Path.GetFullPath(fullPath);
            var normalizedRootPath = Path.GetFullPath(rootPath);
            return normalizedFullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
