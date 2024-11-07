using FileViewer.Models;
using System.Text;

namespace FileViewerApi.Services
{
    public class FileService : IFileService
    {
        private readonly Dictionary<string, string> _rootPaths;
        private readonly string[] _allowedExtensions = { ".log", ".txt" };
        private readonly int _maxFileSizeInMB = 10; // Maximum file size to read

        public FileService(IConfiguration configuration)
        {
            var rootSection = configuration.GetSection("FileSettings:RootPaths");
            _rootPaths = rootSection.Get<Dictionary<string, string>>()
                ?? throw new ArgumentNullException("RootPaths configuration is missing");
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
                    RootName = rootName
                });
            }

            // Add files with allowed extensions
            foreach (var file in Directory.GetFiles(fullPath))
            {
                var extension = Path.GetExtension(file).ToLower();
                if (_allowedExtensions.Contains(extension))
                {
                    items.Add(new FileItem
                    {
                        Name = Path.GetFileName(file),
                        Path = ConvertToWebPath(Path.GetRelativePath(rootPath, file)),
                        IsDirectory = false,
                        RootName = rootName
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
