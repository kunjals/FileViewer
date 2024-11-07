namespace FileViewer.Models
{
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public string Error { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public UserInfo User { get; set; }
    }
}
