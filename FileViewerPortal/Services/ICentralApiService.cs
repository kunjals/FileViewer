using FileViewer.Models;

namespace FileViewerPortal.Services
{
    public interface ICentralApiService
    {
        Task<IEnumerable<FileServer>> GetServers();
        Task<FileServerResponse<IEnumerable<RootDirectory>>> GetRootDirectories(string serverId);
        Task<FileServerResponse<IEnumerable<FileItem>>> Browse(string serverId, string rootName, string path);
        Task<FileServerResponse<FileReadResult>> GetFileContents(string serverId, string rootName, string path);
        Task<FileSearchResponse> SearchFiles(FileSearchRequest request);
    }
}
