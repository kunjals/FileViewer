using FileViewer.Models;

namespace CentralApi.Services
{
    public interface IFileServerManager
    {
        Task<IEnumerable<FileServer>> GetServers();
        Task<FileServerResponse<IEnumerable<RootDirectory>>> GetRootDirectories(string serverId);
        Task<FileServerResponse<IEnumerable<FileItem>>> Browse(string serverId, string rootName, string path);
        Task<FileServerResponse<FileReadResult>> GetFileContents(string serverId, string rootName, string path);
        Task<bool> CheckServerHealth(string serverId);
        Task<FileSearchResponse> Search(FileSearchRequest request);
    }
}
