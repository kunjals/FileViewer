using FileViewer.Models;
using FileViewerPortal.Models;
using FileViewerPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FileViewerPortal.Controllers
{
    [Authorize]
    public class FilePortalController : Controller
    {
        private readonly ICentralApiService _centralApiService;
        private readonly ILogger<FilePortalController> _logger;

        public FilePortalController(
            ICentralApiService centralApiService,
            ILogger<FilePortalController> logger)
        {
            _centralApiService = centralApiService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var servers = await _centralApiService.GetServers();
                return View(servers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving server list");
                return View("Error", new ErrorViewModel
                {
                    Message = "Unable to retrieve server list. Please try again later."
                });
            }
        }

        public async Task<IActionResult> Browse(string serverId, string rootName = "", string path = "")
        {
            try
            {
                // Store current server information for breadcrumb navigation
                var servers = await _centralApiService.GetServers();
                var currentServer = servers.FirstOrDefault(s => s.Id == serverId);

                if (currentServer == null)
                    return NotFound("Server not found");

                if (!currentServer.IsHealthy)
                    return View("Error", new ErrorViewModel
                    {
                        Message = $"Server '{currentServer.Name}' is currently unavailable"
                    });

                ViewBag.Server = currentServer;
                ViewBag.RootName = rootName;
                ViewBag.Path = path;

                // If no root name specified, show root directories
                if (string.IsNullOrEmpty(rootName))
                {
                    var rootResponse = await _centralApiService.GetRootDirectories(serverId);
                    if (!rootResponse.Success)
                    {
                        return View("Error", new ErrorViewModel
                        {
                            Message = rootResponse.ErrorMessage ?? "Error retrieving root directories"
                        });
                    }

                    return View("Roots", rootResponse.Data);
                }

                // Browse directory contents
                var browseResponse = await _centralApiService.Browse(serverId, rootName, path);
                if (!browseResponse.Success)
                {
                    return View("Error", new ErrorViewModel
                    {
                        Message = browseResponse.ErrorMessage ?? "Error browsing directory"
                    });
                }

                return View(browseResponse.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing server {ServerId}", serverId);
                return View("Error", new ErrorViewModel
                {
                    Message = "An error occurred while browsing the server"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFileContents(string serverId, string rootName, string path)
        {
            try
            {
                var response = await _centralApiService.GetFileContents(serverId, rootName, path);

                if (response.Success)
                {
                    return Json(new
                    {
                        success = true,
                        contents = response.Data.Contents,
                        encoding = response.Data.Encoding,
                        fileSizeBytes = response.Data.FileSizeBytes
                    });
                }

                return Json(new
                {
                    success = false,
                    message = response.ErrorMessage ?? "Error reading file contents",
                    fileSizeBytes = response.Data?.FileSizeBytes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading file from server {ServerId}", serverId);
                return Json(new
                {
                    success = false,
                    message = "Error reading file contents"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> RefreshServerStatus(string serverId)
        {
            try
            {
                var servers = await _centralApiService.GetServers();
                var server = servers.FirstOrDefault(s => s.Id == serverId);

                if (server == null)
                    return Json(new { success = false, message = "Server not found" });

                return Json(new
                {
                    success = true,
                    isHealthy = server.IsHealthy,
                    lastChecked = server.LastChecked.ToString("g")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing server status {ServerId}", serverId);
                return Json(new { success = false, message = "Error checking server status" });
            }
        }

        [HttpPost]
        public IActionResult DownloadFile(string serverId, string rootName, string path)
        {
            // Redirect to an error page if the file download fails
            TempData["ErrorMessage"] = "File download is not implemented in this version";
            return RedirectToAction("Browse", new { serverId, rootName, path });
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                Message = "An error occurred while processing your request"
            });
        }

        [HttpPost]
        public async Task<IActionResult> Search([FromBody] FileSearchRequest request)
        {
            try
            {
                var response = await _centralApiService.SearchFiles(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing file search");
                return StatusCode(500, new { success = false, error = "Error performing search" });
            }
        }
    }
}
