# Script pour g�n�rer un rapport d'erreurs .NET en Markdown

param(
    [string]$OutputFile = "erreurs-compilation.md",
    [string]$ProjectPath = ".",
    [bool]$MarkAsResolved = $false # Par d�faut, marquer les erreurs comme r�solues
)

# D�termine le chemin absolu du projet
if (-not [System.IO.Path]::IsPathRooted($ProjectPath)) {
    $ProjectPath = Join-Path (Get-Location).Path $ProjectPath
}

# Assure que le chemin de sortie est absolu
if (-not [System.IO.Path]::IsPathRooted($OutputFile)) {
    $OutputFile = Join-Path (Get-Location).Path $OutputFile
}

# Fonction pour parser les erreurs .NET
function Get-DotNetErrors {
    param([string[]]$BuildOutput)
    
    $errors = @()
    foreach ($line in $BuildOutput) {
        # Pattern pour les erreurs .NET: Path(line,col): error CSxxxx: message
        if ($line -match '(.+?)\((\d+),(\d+)\):\s*error\s+(\w+):\s*(.+)') {
            $errors += [PSCustomObject]@{
                File = Split-Path $matches[1] -Leaf
                FullPath = $matches[1]
                Line = $matches[2]
                Column = $matches[3]
                Code = $matches[4]
                Message = $matches[5].Trim()
            }
        }
        # Pattern alternatif pour certaines erreurs
        elseif ($line -match 'error\s+(\w+):\s*(.+)') {
            $errors += [PSCustomObject]@{
                File = "G�n�ral"
                FullPath = ""
                Line = ""
                Column = ""
                Code = $matches[1]
                Message = $matches[2].Trim()
            }
        }
    }
    return $errors
}

# Compilation et capture des erreurs
Write-Host "Compilation en cours..." -ForegroundColor Yellow
$buildOutput = dotnet build $ProjectPath 2>&1

# Parser les erreurs
$errors = Get-DotNetErrors -BuildOutput $buildOutput

if ($errors.Count -eq 0) {
    Write-Host "? Aucune erreur trouv�e !" -ForegroundColor Green
    return
}

# Grouper par fichier
$errorsByFile = $errors | Group-Object -Property File

# D�finir le statut des t�ches
$taskStatus = if ($MarkAsResolved) { "x" } else { " " }

# G�n�rer le contenu Markdown
$markdown = @"
# Rapport d'erreurs de compilation

**Date :** $(Get-Date -Format "dd/MM/yyyy HH:mm")  
**Projet :** $ProjectPath  
**Total erreurs :** $($errors.Count)

---

"@

foreach ($fileGroup in $errorsByFile) {
    $fileName = $fileGroup.Name
    $fileErrors = $fileGroup.Group
    $errorCount = $fileErrors.Count
      $markdown += @"

## $fileName
**$errorCount erreur$(if($errorCount -gt 1){"s"})**

"@

    foreach ($error in $fileErrors) {
        $lineInfo = if ($error.Line) { " (ligne $($error.Line))" } else { "" }
        # Utiliser la variable taskStatus pour d�terminer si les erreurs sont marqu�es comme r�solues
        $markdown += "* [$taskStatus] **$($error.Code):**$lineInfo $($error.Message)`n"
    }
}

# Ajouter une section de statistiques
$markdown += @"

---

## Statistiques

"@

$errorsByCode = $errors | Group-Object -Property Code | Sort-Object Count -Descending
foreach ($codeGroup in $errorsByCode) {
    $markdown += "* **$($codeGroup.Name):** $($codeGroup.Count) occurrence$(if($codeGroup.Count -gt 1){"s"})`n"
}

# Sauvegarder le fichier
[System.IO.File]::WriteAllText($OutputFile, $markdown, [System.Text.Encoding]::GetEncoding(1252))

Write-Host " Rapport g�n�r�: $OutputFile" -ForegroundColor Green
Write-Host " $($errors.Count) erreur(s) trouv�e(s) dans $($errorsByFile.Count) fichier(s)" -ForegroundColor Cyan
Write-Host " Pour plus de d�tails, consultez le fichier $OutputFile" -ForegroundColor Cyan