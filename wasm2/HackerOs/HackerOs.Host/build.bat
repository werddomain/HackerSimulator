@echo off
REM Script de publication pour HackerOs.Host
REM Usage: build.bat [platform] [configuration]
REM Plateformes supportees: win-x64, linux-x64, osx-x64
REM Configurations: Debug, Release

set PLATFORM=%1
set CONFIGURATION=%2
set PROJECT_NAME=HackerOs.Host

if "%PLATFORM%"=="" set PLATFORM=win-x64
if "%CONFIGURATION%"=="" set CONFIGURATION=Release

echo 🚀 Publication de %PROJECT_NAME% pour %PLATFORM% en mode %CONFIGURATION%

REM Nettoyer les builds précédents
echo 🧹 Nettoyage...
dotnet clean %PROJECT_NAME% -c %CONFIGURATION%

REM Publier le projet
echo 📦 Publication...
dotnet publish %PROJECT_NAME% -c %CONFIGURATION% -r %PLATFORM% --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false

if %ERRORLEVEL% equ 0 (
    echo ✅ Publication réussie!
    echo 📂 Fichiers publiés dans: %PROJECT_NAME%\bin\%CONFIGURATION%\net9.0\%PLATFORM%\publish\
    
    REM Afficher la taille du fichier exécutable
    if "%PLATFORM%"=="win-x64" (
        set EXECUTABLE_PATH=%PROJECT_NAME%\bin\%CONFIGURATION%\net9.0\%PLATFORM%\publish\%PROJECT_NAME%.exe
    ) else (
        set EXECUTABLE_PATH=%PROJECT_NAME%\bin\%CONFIGURATION%\net9.0\%PLATFORM%\publish\%PROJECT_NAME%
    )
    
    if exist "%EXECUTABLE_PATH%" (
        echo 🎯 Exécutable: %EXECUTABLE_PATH%
    )
) else (
    echo ❌ Échec de la publication
    exit /b 1
)
