using Microsoft.AspNetCore.Mvc;
using ProxyServer.FileSystem.Management;
using ProxyServer.FileSystem.Models;
using ProxyServer.FileSystem.Operations;
using ProxyServer.Protocol.Models.FileSystem;
using System.Text.RegularExpressions;
using FSSharedFolderInfo = ProxyServer.FileSystem.Models.SharedFolderInfo;

namespace ProxyServer.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FoldersController : ControllerBase
    {
        private readonly SharedFolderManager _sharedFolderManager;
        private readonly ILogger<FoldersController> _logger;

        public FoldersController(SharedFolderManager sharedFolderManager, ILogger<FoldersController> logger)
        {
            _sharedFolderManager = sharedFolderManager;
            _logger = logger;
        }

        /// <summary>
        /// Get all shared folders
        /// </summary>
        [HttpGet]
        public ActionResult<ApiResponse<IEnumerable<FSSharedFolderInfo>>> GetSharedFolders()
        {
            try
            {
                var folders = _sharedFolderManager.GetSharedFolders();
                return ApiResponse<IEnumerable<FSSharedFolderInfo>>.SuccessResponse(folders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shared folders");
                return ApiResponse<IEnumerable<FSSharedFolderInfo>>.ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Get a shared folder by ID
        /// </summary>
        [HttpGet("{id}")]
        public ActionResult<ApiResponse<FSSharedFolderInfo>> GetSharedFolder(string id)
        {
            try
            {
                var folder = _sharedFolderManager.GetSharedFolder(id);
                if (folder == null)
                {
                    return NotFound(ApiResponse<FSSharedFolderInfo>.ErrorResponse($"Shared folder with ID {id} not found"));
                }

                return ApiResponse<FSSharedFolderInfo>.SuccessResponse(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting shared folder {id}");
                return ApiResponse<FSSharedFolderInfo>.ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Create a new shared folder
        /// </summary>
        [HttpPost]
        public ActionResult<ApiResponse<FSSharedFolderInfo>> CreateSharedFolder([FromBody] CreateSharedFolderRequest request)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(request.HostPath))
                {
                    return BadRequest(ApiResponse<FSSharedFolderInfo>.ErrorResponse("Host path is required"));
                }

                if (string.IsNullOrEmpty(request.Alias))
                {
                    return BadRequest(ApiResponse<FSSharedFolderInfo>.ErrorResponse("Alias is required"));
                }

                // Validate alias format (letters, numbers, underscore, dash)
                if (!Regex.IsMatch(request.Alias, "^[a-zA-Z0-9_\\-]+$"))
                {
                    return BadRequest(ApiResponse<FSSharedFolderInfo>.ErrorResponse(
                        "Alias can only contain letters, numbers, underscores, and dashes"));
                }

                // Parse permission
                SharedFolderPermission permission;
                if (request.Permission?.ToLower() == "read-write")
                {
                    permission = SharedFolderPermission.ReadWrite;
                }
                else
                {
                    permission = SharedFolderPermission.ReadOnly;
                }

                // Create shared folder
                var sharedFolder = _sharedFolderManager.AddSharedFolder(
                    request.HostPath,
                    request.Alias,
                    permission,
                    request.AllowedExtensions,
                    request.BlockedExtensions);

                _logger.LogInformation($"Created shared folder {sharedFolder.Id} with alias {sharedFolder.Alias}");

                return ApiResponse<FSSharedFolderInfo>.SuccessResponse(sharedFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shared folder");
                return ApiResponse<FSSharedFolderInfo>.ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Update a shared folder
        /// </summary>
        [HttpPut("{id}")]
        public ActionResult<ApiResponse<FSSharedFolderInfo>> UpdateSharedFolder(string id, [FromBody] UpdateSharedFolderRequest request)
        {
            try
            {
                // Check if folder exists
                var existingFolder = _sharedFolderManager.GetSharedFolder(id);
                if (existingFolder == null)
                {
                    return NotFound(ApiResponse<FSSharedFolderInfo>.ErrorResponse($"Shared folder with ID {id} not found"));
                }

                // Validate alias format if provided
                if (!string.IsNullOrEmpty(request.Alias) && !Regex.IsMatch(request.Alias, "^[a-zA-Z0-9_\\-]+$"))
                {
                    return BadRequest(ApiResponse<FSSharedFolderInfo>.ErrorResponse(
                        "Alias can only contain letters, numbers, underscores, and dashes"));
                }

                // Parse permission if provided
                SharedFolderPermission? permission = null;
                if (!string.IsNullOrEmpty(request.Permission))
                {
                    if (request.Permission.ToLower() == "read-write")
                    {
                        permission = SharedFolderPermission.ReadWrite;
                    }
                    else if (request.Permission.ToLower() == "read-only")
                    {
                        permission = SharedFolderPermission.ReadOnly;
                    }
                    else
                    {
                        return BadRequest(ApiResponse<FSSharedFolderInfo>.ErrorResponse(
                            "Permission must be either 'read-only' or 'read-write'"));
                    }
                }

                // Update shared folder
                bool updated = _sharedFolderManager.UpdateSharedFolder(
                    id,
                    request.Alias,
                    permission,
                    request.AllowedExtensions,
                    request.BlockedExtensions);

                if (!updated)
                {
                    return BadRequest(ApiResponse<FSSharedFolderInfo>.ErrorResponse("No changes were made"));
                }

                // Get updated folder
                var updatedFolder = _sharedFolderManager.GetSharedFolder(id);

                _logger.LogInformation($"Updated shared folder {id}");

                return ApiResponse<FSSharedFolderInfo>.SuccessResponse(updatedFolder!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating shared folder {id}");
                return ApiResponse<FSSharedFolderInfo>.ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Delete a shared folder
        /// </summary>
        [HttpDelete("{id}")]
        public ActionResult<ApiResponse<bool>> DeleteSharedFolder(string id)
        {
            try
            {
                // Check if folder exists
                var existingFolder = _sharedFolderManager.GetSharedFolder(id);
                if (existingFolder == null)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse($"Shared folder with ID {id} not found"));
                }

                // Delete shared folder
                bool deleted = _sharedFolderManager.RemoveSharedFolder(id);

                if (!deleted)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Failed to delete shared folder"));
                }

                _logger.LogInformation($"Deleted shared folder {id}");

                return ApiResponse<bool>.SuccessResponse(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting shared folder {id}");
                return ApiResponse<bool>.ErrorResponse(ex.Message);
            }
        }
    }
}
