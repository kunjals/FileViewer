using CentralApi.Services;
using FileViewer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace CentralApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileApiController : ControllerBase
    {
        private readonly IFileServerManager _serverManager;
        private readonly ILogger<FileApiController> _logger;

        public FileApiController(
            IFileServerManager serverManager,
            ILogger<FileApiController> logger)
        {
            _serverManager = serverManager;
            _logger = logger;
        }

        [HttpGet("servers")]
        public async Task<ActionResult<IEnumerable<FileServer>>> GetServers()
        {
            try
            {
                var servers = await _serverManager.GetServers();
                // Remove sensitive information before sending to client
                return Ok(servers.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.IsHealthy,
                    s.LastChecked
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting servers");
                return StatusCode(500, new { error = "Error retrieving servers" });
            }
        }

        [HttpGet("browse/{serverId}")]
        public async Task<ActionResult<FileServerResponse<IEnumerable<FileItem>>>> Browse(
            string serverId, string rootName, string path = "")
        {
            try
            {
                var result = await _serverManager.Browse(serverId, rootName, path);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing server {ServerId}", serverId);
                return StatusCode(500, new { error = "Error browsing directory" });
            }
        }

        [HttpGet("file/{serverId}")]
        public async Task<ActionResult<FileServerResponse<FileReadResult>>> GetFileContents(
            string serverId, string rootName, string path)
        {
            try
            {
                var result = await _serverManager.GetFileContents(serverId, rootName, path);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading file from server {ServerId}", serverId);
                return StatusCode(500, new { error = "Error reading file" });
            }
        }

        [HttpGet("roots/{serverId}")]
        public async Task<ActionResult<FileServerResponse<IEnumerable<RootDirectory>>>> GetRootDirectories(string serverId)
        {
            try
            {
                var result = await _serverManager.GetRootDirectories(serverId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roots from server {ServerId}", serverId);
                return StatusCode(500, new { error = "Error retrieving root directories" });
            }
        }

        [HttpPost("{serverId}/search")]
        public async Task<ActionResult<FileSearchResponse>> Search([FromBody] FileSearchRequest request)
        {
            try
            {
                var result = _serverManager.SearchFiles(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching files on server {ServerId}", request.ServerId);
                return StatusCode(500, new { Error = "Error performing search" });
            }
        }
    }
}
