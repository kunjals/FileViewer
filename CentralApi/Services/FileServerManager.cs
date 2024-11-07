using FileViewer.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CentralApi.Services
{
    public class FileServerManager : IFileServerManager
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<FileServerManager> _logger;
        private readonly IMemoryCache _cache;
        private static List<FileServer> _servers;
        public FileServerManager(
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<FileServerManager> logger,
            IMemoryCache cache)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
        }

        public async Task<IEnumerable<FileServer>> GetServers()
        {
            _servers = _configuration.GetSection("FileServers").Get<List<FileServer>>()
                ?? new List<FileServer>();

            foreach (var server in _servers)
            {
                server.IsHealthy = await CheckServerHealth(server.Id);
            }

            return _servers;
        }

        public async Task<bool> CheckServerHealth(string serverId)
        {
            var server = await GetServerById(serverId);
            if (server == null) return false;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{server.InternalUrl}/api/FileApi/health");
                request.Headers.Add("X-API-Key", server.ApiKey);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health for server {ServerId}", serverId);
                return false;
            }
        }

        public async Task<FileServerResponse<IEnumerable<RootDirectory>>> GetRootDirectories(string serverId)
        {
            return await SendRequest<IEnumerable<RootDirectory>>(serverId, "api/FileApi/roots");
        }

        public async Task<FileServerResponse<IEnumerable<FileItem>>> Browse(string serverId, string rootName, string path)
        {
            return await SendRequest<IEnumerable<FileItem>>(
                serverId,
                $"api/FileApi/browse?rootName={Uri.EscapeDataString(rootName)}&path={Uri.EscapeDataString(path ?? "")}");
        }

        public async Task<FileServerResponse<FileReadResult>> GetFileContents(string serverId, string rootName, string path)
        {
            return await SendRequest<FileReadResult>(
                serverId,
                $"api/FileApi/file?rootName={Uri.EscapeDataString(rootName)}&path={Uri.EscapeDataString(path)}");
        }

        public async Task<FileSearchResponse> Search(FileSearchRequest request)
        {
            var server = await GetServerById(request.ServerId);
            if (server == null)
            {
                return new FileSearchResponse
                {
                    Success = false,
                    Error = "Server not found",
                    Results = new List<FileSearchResult>()
                };
            }

            var searchRequest = new HttpRequestMessage(HttpMethod.Post,
                $"{server.InternalUrl}/api/FileApi/search");
            searchRequest.Headers.Add("X-API-Key", server.ApiKey);
            searchRequest.Content = JsonContent.Create(request);

            var response = await _httpClient.SendAsync(searchRequest);
            if (!response.IsSuccessStatusCode)
            {
                return new FileSearchResponse
                {
                    Success = false,
                    Error = "Error performing search",
                    Results = new List<FileSearchResult>()
                };
            }

            var result = await response.Content.ReadFromJsonAsync<FileSearchResponse>();

            return result;
        }

        private async Task<FileServerResponse<T>> SendRequest<T>(string serverId, string relativePath)
        {
            var server = await GetServerById(serverId);
            if (server == null)
            {
                return new FileServerResponse<T>
                {
                    Success = false,
                    ErrorMessage = "Server not found",
                    ServerId = serverId
                };
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{server.InternalUrl}/{relativePath}");
                request.Headers.Add("X-API-Key", server.ApiKey);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<T>();
                    return new FileServerResponse<T>
                    {
                        Success = true,
                        Data = data,
                        ServerId = serverId
                    };
                }

                return new FileServerResponse<T>
                {
                    Success = false,
                    ErrorMessage = await response.Content.ReadAsStringAsync(),
                    ServerId = serverId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing server {ServerId}", serverId);
                return new FileServerResponse<T>
                {
                    Success = false,
                    ErrorMessage = "Error communicating with file server",
                    ServerId = serverId
                };
            }
        }

        private async Task<FileServer> GetServerById(string serverId)
        {
            //var servers = await GetServers();
            return _servers.FirstOrDefault(s => s.Id == serverId);
        }
    }
}
