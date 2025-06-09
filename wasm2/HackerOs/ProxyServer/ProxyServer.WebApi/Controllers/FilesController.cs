using Microsoft.AspNetCore.Mvc;
using ProxyServer.FileSystem.Models;
using ProxyServer.FileSystem.Operations;
using ProxyServer.FileSystem.Security;
using ProxyServer.Protocol.Models.FileSystem;
using System.Net.Mime;
using System.Text;

namespace ProxyServer.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly FileSystemOperations _fileSystemOperations;
        private readonly FileSystemSecurity _fileSystemSecurity;
        private readonly ILogger<FilesController> _logger;

        public FilesController(
            FileSystemOperations fileSystemOperations,
            FileSystemSecurity fileSystemSecurity,
            ILogger<FilesController> logger)
        {
            _fileSystemOperations = fileSystemOperations;
            _fileSystemSecurity = fileSystemSecurity;
            _logger = logger;
        }

        /// <summary>
        /// List directory contents
        /// </summary>
        /// <param name="path">Virtual path of the directory to list</param>
        [HttpGet]
        public ActionResult<ApiResponse<IEnumerable<FileSystemEntryDto>>> ListDirectory([FromQuery] string path)
        {
            try
            {
                // Validate virtual path
                if (!_fileSystemSecurity.IsValidVirtualPath(path))
                {
                    return BadRequest(ApiResponse<IEnumerable<FileSystemEntryDto>>.ErrorResponse($"Invalid path format: {path}"));
                }

                // List directory contents
                var entries = _fileSystemOperations.ListDirectoryByVirtualPath(path);
                
                // Map to DTOs
                var entryDtos = entries.Select(e => new FileSystemEntryDto
                {
                    Name = e.Name,
                    VirtualPath = e.VirtualPath ?? "",
                    IsDirectory = e.IsDirectory,
                    Size = e.Size,
                    LastModified = e.LastModified,
                    LastAccessed = e.LastAccessed,
                    CreationTime = e.CreationTime,
                    Attributes = e.Attributes.ToString()
                });
                
                return ApiResponse<IEnumerable<FileSystemEntryDto>>.SuccessResponse(entryDtos);
            }
            catch (DirectoryNotFoundException ex)
            {
                return NotFound(ApiResponse<IEnumerable<FileSystemEntryDto>>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error listing directory {path}");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    ApiResponse<IEnumerable<FileSystemEntryDto>>.ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Get file content
        /// </summary>
        /// <param name="path">Virtual path of the file to read</param>
        [HttpGet("content")]
        public async Task<IActionResult> GetFileContent([FromQuery] string path)
        {
            try
            {
                // Validate path
                if (!_fileSystemSecurity.IsValidVirtualPath(path))
                {
                    return BadRequest($"Invalid path format: {path}");
                }

                // Get file content
                var content = await _fileSystemOperations.ReadFileByVirtualPathAsync(path);

                // Try to determine content type
                var contentType = GetContentType(path);

                // Send the file
                return File(content, contentType);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading file {path}");
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Write file content
        /// </summary>
        [HttpPost("content")]
        public async Task<ActionResult<ApiResponse<bool>>> WriteFileContent([FromBody] WriteFileRequest request)
        {
            try
            {
                // Validate path
                if (!_fileSystemSecurity.IsValidVirtualPath(request.VirtualPath))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse($"Invalid path format: {request.VirtualPath}"));
                }

                // Decode content (from Base64)
                byte[] content;
                try
                {
                    content = Convert.FromBase64String(request.Content);
                }
                catch (FormatException)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid content format. Expected Base64 encoded string."));
                }

                // Write file content
                bool result = await _fileSystemOperations.WriteFileByVirtualPathAsync(
                    request.VirtualPath, 
                    content, 
                    overwrite: request.Overwrite);

                return ApiResponse<bool>.SuccessResponse(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (IOException ex)
            {
                return Conflict(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error writing file {request.VirtualPath}");
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Delete a file or directory
        /// </summary>        [HttpDelete]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteFileOrDirectory(
            [FromQuery] string path, 
            [FromQuery] bool recursive = false,
            [FromQuery] string username = "anonymous")
        {
            try
            {
                // Validate path
                if (!_fileSystemSecurity.IsValidVirtualPath(path))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse($"Invalid path format: {path}"));
                }

                // Determine if it's a file or directory
                var resolved = _fileSystemOperations.ResolveVirtualPath(path);
                if (resolved == null)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse($"Path not found: {path}"));
                }

                var (hostPath, _, _) = resolved.Value;
                
                bool result;                if (System.IO.File.Exists(hostPath))
                {
                    // Delete file
                    result = await _fileSystemOperations.DeleteFileByVirtualPathAsync(path, username);
                }
                else if (Directory.Exists(hostPath))
                {
                    // Delete directory
                    result = await _fileSystemOperations.DeleteDirectoryByVirtualPathAsync(path, recursive, username);
                }
                else
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse($"Path not found: {path}"));
                }

                return ApiResponse<bool>.SuccessResponse(true);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (IOException ex)
            {
                return Conflict(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (NotImplementedException ex)
            {
                return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting {path}");
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Create a directory
        /// </summary>        [HttpPost("mkdir")]
        public async Task<ActionResult<ApiResponse<bool>>> CreateDirectory([FromBody] CreateDirectoryRequest request, [FromQuery] string username = "anonymous")
        {
            try
            {
                // Validate path
                if (!_fileSystemSecurity.IsValidVirtualPath(request.VirtualPath))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse($"Invalid path format: {request.VirtualPath}"));
                }                // Create directory using the extension method
                bool result = await _fileSystemOperations.CreateDirectoryByVirtualPathAsync(request.VirtualPath, request.CreateParents, username);

                return ApiResponse<bool>.SuccessResponse(true);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (IOException ex)
            {
                return Conflict(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (NotImplementedException ex)
            {
                return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating directory {request.VirtualPath}");
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Copy files or directories
        /// </summary>        [HttpPost("copy")]
        public async Task<ActionResult<ApiResponse<bool>>> CopyFileOrDirectory([FromBody] CopyFileRequest request, [FromQuery] string username = "anonymous")
        {
            try
            {
                // Validate paths
                if (!_fileSystemSecurity.IsValidVirtualPath(request.SourcePath))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse($"Invalid source path format: {request.SourcePath}"));
                }

                if (!_fileSystemSecurity.IsValidVirtualPath(request.DestinationPath))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse($"Invalid destination path format: {request.DestinationPath}"));
                }                // Copy file or directory using the extension method
                bool result = await _fileSystemOperations.CopyByVirtualPathAsync(
                    request.SourcePath, 
                    request.DestinationPath, 
                    request.Overwrite,
                    request.Recursive,
                    username);

                return ApiResponse<bool>.SuccessResponse(true);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (IOException ex)
            {
                return Conflict(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (NotImplementedException ex)
            {
                return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error copying from {request.SourcePath} to {request.DestinationPath}");
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Move files or directories
        /// </summary>        [HttpPost("move")]
        public async Task<ActionResult<ApiResponse<bool>>> MoveFileOrDirectory([FromBody] MoveFileRequest request, [FromQuery] string username = "anonymous")
        {
            try
            {
                // Validate paths
                if (!_fileSystemSecurity.IsValidVirtualPath(request.SourcePath))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse($"Invalid source path format: {request.SourcePath}"));
                }

                if (!_fileSystemSecurity.IsValidVirtualPath(request.DestinationPath))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse($"Invalid destination path format: {request.DestinationPath}"));
                }                // Move file or directory using the extension method
                bool result = await _fileSystemOperations.MoveByVirtualPathAsync(
                    request.SourcePath, 
                    request.DestinationPath, 
                    request.Overwrite,
                    username);

                return ApiResponse<bool>.SuccessResponse(true);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (IOException ex)
            {
                return Conflict(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (NotImplementedException ex)
            {
                return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error moving from {request.SourcePath} to {request.DestinationPath}");
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }
        
        #region Helper Methods

        /// <summary>
        /// Gets the MIME content type for a file based on its extension
        /// </summary>
        private string GetContentType(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            
            return extension switch
            {
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "text/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".ico" => "image/x-icon",
                ".svg" => "image/svg+xml",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".mp4" => "video/mp4",
                ".zip" => "application/zip",
                ".doc" or ".docx" => "application/msword",
                ".xls" or ".xlsx" => "application/vnd.ms-excel",
                ".ppt" or ".pptx" => "application/vnd.ms-powerpoint",
                _ => "application/octet-stream"  // Default binary file type
            };
        }

        #endregion
    }
}
