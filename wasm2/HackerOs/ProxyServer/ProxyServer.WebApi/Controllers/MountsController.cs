using Microsoft.AspNetCore.Mvc;
using ProxyServer.FileSystem.Management;
using ProxyServer.FileSystem.Models;
using ProxyServer.FileSystem.Operations;
using ProxyServer.Protocol.Models.FileSystem;

namespace ProxyServer.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MountsController : ControllerBase
    {
        private readonly MountPointManager _mountPointManager;
        private readonly SharedFolderManager _sharedFolderManager;
        private readonly FileSystemOperations _fileSystemOperations;
        private readonly ILogger<MountsController> _logger;

        public MountsController(
            MountPointManager mountPointManager,
            SharedFolderManager sharedFolderManager,
            FileSystemOperations fileSystemOperations,
            ILogger<MountsController> logger)
        {
            _mountPointManager = mountPointManager;
            _sharedFolderManager = sharedFolderManager;
            _fileSystemOperations = fileSystemOperations;
            _logger = logger;
        }

        /// <summary>
        /// Get all mount points
        /// </summary>
        [HttpGet]
        public ActionResult<ApiResponse<IEnumerable<MountPointDto>>> GetMountPoints()
        {
            try
            {
                var mountPoints = _mountPointManager.GetMountPoints();
                var mountPointDtos = mountPoints.Select(mp => MapToDto(mp));
                
                return ApiResponse<IEnumerable<MountPointDto>>.SuccessResponse(mountPointDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mount points");
                return ApiResponse<IEnumerable<MountPointDto>>.ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Get a mount point by ID
        /// </summary>
        [HttpGet("{id}")]
        public ActionResult<ApiResponse<MountPointDto>> GetMountPoint(string id)
        {
            try
            {
                var mountPoint = _mountPointManager.GetMountPoint(id);
                if (mountPoint == null)
                {
                    return NotFound(ApiResponse<MountPointDto>.ErrorResponse($"Mount point with ID {id} not found"));
                }

                return ApiResponse<MountPointDto>.SuccessResponse(MapToDto(mountPoint));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting mount point {id}");
                return ApiResponse<MountPointDto>.ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Create a new mount point
        /// </summary>
        [HttpPost]
        public ActionResult<ApiResponse<MountPointDto>> CreateMountPoint([FromBody] CreateMountPointRequest request)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(request.SharedFolderId))
                {
                    return BadRequest(ApiResponse<MountPointDto>.ErrorResponse("Shared folder ID is required"));
                }

                if (string.IsNullOrEmpty(request.VirtualPath))
                {
                    return BadRequest(ApiResponse<MountPointDto>.ErrorResponse("Virtual path is required"));
                }

                // Check if shared folder exists
                var sharedFolder = _sharedFolderManager.GetSharedFolder(request.SharedFolderId);
                if (sharedFolder == null)
                {
                    return NotFound(ApiResponse<MountPointDto>.ErrorResponse($"Shared folder with ID {request.SharedFolderId} not found"));
                }

                // Map mount options
                var options = MapFromDto(request.Options);

                // Create mount point through the FileSystemOperations
                var mountPoint = _fileSystemOperations.CreateMountPoint(
                    request.SharedFolderId,
                    request.VirtualPath,
                    options);

                _logger.LogInformation($"Created mount point {mountPoint.Id} at {mountPoint.VirtualPath}");

                return ApiResponse<MountPointDto>.SuccessResponse(MapToDto(mountPoint));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating mount point");
                return ApiResponse<MountPointDto>.ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Update a mount point
        /// </summary>
        [HttpPut("{id}")]
        public ActionResult<ApiResponse<MountPointDto>> UpdateMountPoint(string id, [FromBody] MountOptionsDto options)
        {
            try
            {
                // Check if mount point exists
                var mountPoint = _mountPointManager.GetMountPoint(id);
                if (mountPoint == null)
                {
                    return NotFound(ApiResponse<MountPointDto>.ErrorResponse($"Mount point with ID {id} not found"));
                }

                // Map mount options
                var mappedOptions = MapFromDto(options);

                // Update mount point
                bool updated = _mountPointManager.UpdateMountPoint(
                    id,
                    null,  // Don't change the virtual path
                    mappedOptions);

                if (!updated)
                {
                    return BadRequest(ApiResponse<MountPointDto>.ErrorResponse("No changes were made"));
                }

                // Get updated mount point
                var updatedMountPoint = _mountPointManager.GetMountPoint(id);

                _logger.LogInformation($"Updated mount point {id}");

                return ApiResponse<MountPointDto>.SuccessResponse(MapToDto(updatedMountPoint!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating mount point {id}");
                return ApiResponse<MountPointDto>.ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Delete a mount point
        /// </summary>
        [HttpDelete("{id}")]
        public ActionResult<ApiResponse<bool>> DeleteMountPoint(string id, [FromQuery] bool permanently = false)
        {
            try
            {
                // Check if mount point exists
                var mountPoint = _mountPointManager.GetMountPoint(id);
                if (mountPoint == null)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse($"Mount point with ID {id} not found"));
                }

                // Delete mount point through FileSystemOperations
                bool removed = _fileSystemOperations.RemoveMountPoint(id, permanently);

                if (!removed)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Failed to remove mount point"));
                }

                _logger.LogInformation($"{(permanently ? "Permanently deleted" : "Deactivated")} mount point {id}");

                return ApiResponse<bool>.SuccessResponse(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting mount point {id}");
                return ApiResponse<bool>.ErrorResponse(ex.Message);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Maps a MountPoint to MountPointDto
        /// </summary>
        private MountPointDto MapToDto(MountPoint mountPoint)
        {
            return new MountPointDto
            {
                Id = mountPoint.Id,
                VirtualPath = mountPoint.VirtualPath,
                SharedFolderId = mountPoint.SharedFolderId,
                SharedFolderAlias = mountPoint.SharedFolder?.Alias ?? "",
                Options = new MountOptionsDto
                {
                    ReadOnly = mountPoint.Options.ReadOnly,
                    CaseSensitive = mountPoint.Options.CaseSensitive,
                    TrackAccess = mountPoint.Options.TrackAccess,
                    FollowSymlinks = mountPoint.Options.FollowSymlinks,
                    MaxFileSize = mountPoint.Options.MaxFileSize,
                    CustomOptions = mountPoint.Options.CustomOptions?.ToDictionary(kv => kv.Key, kv => kv.Value)
                },
                CreatedAt = mountPoint.CreatedAt,
                LastAccessed = mountPoint.LastAccessed,
                IsActive = mountPoint.IsActive
            };
        }

        /// <summary>
        /// Maps a MountOptionsDto to MountOptions
        /// </summary>
        private MountOptions MapFromDto(MountOptionsDto dto)
        {
            return new MountOptions
            {
                ReadOnly = dto.ReadOnly,
                CaseSensitive = dto.CaseSensitive,
                TrackAccess = dto.TrackAccess,
                FollowSymlinks = dto.FollowSymlinks,
                MaxFileSize = dto.MaxFileSize,
                CustomOptions = dto.CustomOptions?.ToDictionary(kv => kv.Key, kv => kv.Value) 
                              ?? new Dictionary<string, string>()
            };
        }

        #endregion
    }
}
