<#
.SYNOPSIS
Runs either browser or non-browser tests after the solution has been built.
.DESCRIPTION
Classifies actual test projects by their Playwright dependency, rather than a
test-name filter that could accidentally start a browser fixture on CI.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('NonBrowser', 'Browser')]
    [string] $Suite,
    [string] $Configuration = 'Release',
    [switch] $ListOnly
)

$ErrorActionPreference = 'Stop'
$solutionRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$selectedProjects = @(
    Get-ChildItem (Join-Path $solutionRoot 'Tests') -Directory | Sort-Object Name | ForEach-Object {
        Get-ChildItem $_.FullName -Filter '*.csproj' -File | ForEach-Object {
            [xml] $project = [IO.File]::ReadAllText($_.FullName)
            $packages = @($project.SelectNodes('//PackageReference') | ForEach-Object { $_.GetAttribute('Include') })
            if ($packages -notcontains 'Microsoft.NET.Test.Sdk') { return }
            $references = @($project.SelectNodes('//ProjectReference') | ForEach-Object { $_.GetAttribute('Include') })
            $usesBrowser = $packages -contains 'Microsoft.Playwright' -or
                @($references | Where-Object { $_ -match 'HackerOs\.E2E\.Support\.csproj$' }).Count -gt 0
            if ($usesBrowser -eq ($Suite -eq 'Browser')) { $_ }
        }
    }
)
if ($selectedProjects.Count -eq 0) { throw "No test projects selected for $Suite." }

Write-Output "$Suite suite: $($selectedProjects.Count) test projects."
if ($ListOnly) {
    $selectedProjects | ForEach-Object { [IO.Path]::GetRelativePath($solutionRoot, $_.FullName) }
    return
}

# Keep projects sequential: browser suites can publish the same host and share
# intermediate outputs. Record every failure so later projects still run.
$failedProjects = @()
Push-Location $solutionRoot
try {
    foreach ($testProject in $selectedProjects) {
        $resultsDirectory = Join-Path $solutionRoot "artifacts/test-results/$Suite/$($testProject.BaseName)"
        & dotnet test $testProject.FullName --configuration $Configuration --no-build --no-restore `
            --logger 'trx;LogFilePrefix=hackeros' --results-directory $resultsDirectory
        if ($LASTEXITCODE -ne 0) { $failedProjects += $testProject.BaseName }
    }
}
finally { Pop-Location }

if ($failedProjects.Count -gt 0) {
    throw "Test projects failed: $($failedProjects -join ', ')"
}
