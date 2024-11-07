using FileViewer.Models;
using System.Net.Http.Headers;

namespace FileViewerPortal.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_configuration["CentralApiUrl"]);
        }

        public async Task<AuthResponse> LoginAsync(string username, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/authentication", new LoginReq
                {
                    Username = username,
                    Password = password
                });

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AuthResponse>();
                }

                return new AuthResponse
                {
                    Success = false,
                    Error = "Invalid credentials"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return new AuthResponse
                {
                    Success = false,
                    Error = "An error occurred during login"
                };
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("/api/authentication/logout", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return false;
            }
        }

        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
        {
            throw new NotImplementedException();
        }
    }
}
