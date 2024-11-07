using System.ComponentModel.DataAnnotations;

namespace FileViewer.Models
{
    public class LoginReq
    {
        [Required]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
