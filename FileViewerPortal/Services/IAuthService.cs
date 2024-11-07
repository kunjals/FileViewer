using FileViewer.Models;

namespace FileViewerPortal.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(string username, string password);
        Task<AuthResponse> RefreshTokenAsync(string refreshToken);
        Task<bool> LogoutAsync();
    }
}
