using FileViewer.Models;

namespace FileViewerAgent.Services
{
    public interface IFileService
    {
        IEnumerable<RootDirectory> GetRootDirectories();
        IEnumerable<FileItem> GetDirectoryContents(string rootName, string path);
        FileReadResult ReadFileContents(string rootName, string path);
        Task<FileSearchResponse> SearchFilesAsync(FileSearchRequest request);
    }
}
