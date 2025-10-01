using CentralApi.Models;
using FileViewer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;


namespace CentralApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsPrincipalFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthenticationController> _logger;

        public AuthenticationController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory,
            IConfiguration configuration,
            ILogger<AuthenticationController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _claimsPrincipalFactory = claimsPrincipalFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginReq request)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(request.Username);

                if (user == null)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Error = "Invalid credentials"
                    });
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);
                if (!result.Succeeded)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Error = "Invalid credentials"
                    });
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // Generate token
                var principal = await _claimsPrincipalFactory.CreateAsync(user);
                var roles = await _userManager.GetRolesAsync(user);

                //var token = principal.CreateBearerToken(
                //    new()
                //    {
                //        ExpiresIn = TimeSpan.FromHours(1),
                //        Issuer = _configuration["Jwt:Issuer"],
                //        Audience = _configuration["Jwt:Audience"],
                //        SigningKey = _configuration["Jwt:SigningKey"]
                //    });

                return Ok(new AuthResponse
                {
                    Success = true,
                    Token = "", //token,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.UserName,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Roles = roles.ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login attempt for user {Username}", request.Username);
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Error = "An error occurred during login"
                });
            }
        }

        [HttpPost]
        [Authorize]
        [Route("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { Success = false, Error = "An error occurred during logout" });
            }
        }

        [HttpGet]
        [Authorize]
        [Route("me")]
        public async Task<ActionResult<UserInfo>> GetCurrentUser()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound(new { Error = "User not found" });
                }

                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new UserInfo
                {
                    Id = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles.ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user info");
                return StatusCode(500, new { Error = "An error occurred retrieving user information" });
            }
        }
    }
}
