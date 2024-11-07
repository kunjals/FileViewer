using FileViewer.Models;
using FileViewerApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FileViewerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileApiController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<FileApiController> _logger;

        public FileApiController(IFileService fileService, ILogger<FileApiController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        [HttpGet("roots")]
        public ActionResult<IEnumerable<RootDirectory>> GetRootDirectories()
        {
            try
            {
                return Ok(_fileService.GetRootDirectories());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting root directories");
                return StatusCode(500, new { error = "Error retrieving root directories" });
            }
        }

        [HttpGet("browse")]
        public ActionResult<IEnumerable<FileItem>> Browse(string rootName, string path = "")
        {
            try
            {
                return Ok(_fileService.GetDirectoryContents(rootName, path));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing directory");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("file")]
        public ActionResult<FileReadResult> GetFileContents(string rootName, string path)
        {
            try
            {
                return Ok(_fileService.ReadFileContents(rootName, path));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading file");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}
