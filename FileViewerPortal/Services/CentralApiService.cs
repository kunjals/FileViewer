using FileViewer.Models;

namespace FileViewerPortal.Services
{
    public class CentralApiService : ICentralApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CentralApiService> _logger;

        public CentralApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CentralApiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_configuration["CentralApiUrl"]);
        }

        public async Task<IEnumerable<FileServer>> GetServers()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<FileServer>>("api/FileApi/servers")
                    ?? Array.Empty<FileServer>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting servers from central API");
                return Array.Empty<FileServer>();
            }
        }

        public async Task<FileServerResponse<IEnumerable<RootDirectory>>> GetRootDirectories(string serverId)
        {
            return await GetFromApi<IEnumerable<RootDirectory>>($"api/FileApi/roots/{serverId}");
        }

        public async Task<FileServerResponse<IEnumerable<FileItem>>> Browse(string serverId, string rootName, string path)
        {
            return await GetFromApi<IEnumerable<FileItem>>(
                $"api/FileApi/browse/{serverId}?rootName={Uri.EscapeDataString(rootName)}&path={Uri.EscapeDataString(path ?? "")}");
        }

        public async Task<FileServerResponse<FileReadResult>> GetFileContents(string serverId, string rootName, string path)
        {
            var url = $"api/FileApi/file/{serverId}?rootName={Uri.EscapeDataString(rootName)}&path={Uri.EscapeDataString(path)}";
            _logger.LogInformation(url);
            //return await GetFromApi<FileReadResult>(
            //    $"api/FileApi/file/{serverId}?rootName={Uri.EscapeDataString(rootName)}&path={Uri.EscapeDataString(path)}");
            return await GetFromApi<FileReadResult>(url);
        }

        public async Task<FileServerResponse<FileSearchResult>> SearchFiles(FileSearchRequest request)
        {
            return await GetFromApi<FileSearchResult>(
                $"api/FileApi/{request.ServerId}/search", request);
        }

        private async Task<FileServerResponse<T>> GetFromApi<T>(string url)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<FileServerResponse<T>>(url)
                    ?? new FileServerResponse<T> { Success = false, ErrorMessage = "No response from API" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing central API: {Url}", url);
                return new FileServerResponse<T>
                {
                    Success = false,
                    ErrorMessage = "Error communicating with central API"
                };
            }
        }

        private async Task<FileServerResponse<T>> GetFromApi<T>(string url, object request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, request);
                return await response.Content.ReadFromJsonAsync<FileServerResponse<T>>()
                    ?? new FileServerResponse<T> { Success = false, ErrorMessage = "No response from API" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing central API: {Url}", url);
                return new FileServerResponse<T>
                {
                    Success = false,
                    ErrorMessage = "Error communicating with central API"
                };
            }
        }
    }
}
