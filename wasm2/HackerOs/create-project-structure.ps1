# HackerOS Project Structure Creation Script
# This script creates the complete directory structure for the HackerOS simulator

Write-Host "Creating HackerOS Project Structure..." -ForegroundColor Green

$baseDir = "c:\Users\clefw\source\repos\HackerSimulator\wasm2\HackerOs"

# Define all directories to create
$directories = @(
    # Kernel module structure
    "Kernel",
    "Kernel\Core",
    "Kernel\Process", 
    "Kernel\Memory",
    
    # IO module structure
    "IO",
    "IO\FileSystem",
    "IO\Devices",
    
    # System module structure
    "System",
    "System\Services",
    
    # Shell module structure
    "Shell",
    "Shell\Commands",
    
    # Applications module structure
    "Applications",
    "Applications\BuiltIn",
    
    # Settings module
    "Settings",
    
    # User module
    "User",
    
    # Security module
    "Security",
    
    # Network module structure
    "Network",
    "Network\WebServer",
    "Network\WebServer\Example.com",
    "Network\WebServer\Example.com\Controllers",
    "Network\WebServer\Example.com\Views",
    "Network\WebServer\Example.com\wwwRoot",
    
    # Theme module
    "Theme"
)

# Create each directory
foreach ($dir in $directories) {
    $fullPath = Join-Path $baseDir $dir
    
    if (-not (Test-Path $fullPath)) {
        try {
            New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
            Write-Host " Created: $dir" -ForegroundColor Cyan
        }
        catch {
            Write-Host " Failed to create: $dir - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host " Already exists: $dir" 
    }
}

Write-Host "`nProject structure creation completed!" -ForegroundColor Green
Write-Host "Next step: Review the created structure and proceed with Phase 0.2" -ForegroundColor Magenta
