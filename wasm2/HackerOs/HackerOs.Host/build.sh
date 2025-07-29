#!/bin/bash

# Script de publication pour HackerOs.Host
# Usage: ./build.sh [platform] [configuration]
# Plateformes supportées: win-x64, linux-x64, osx-x64
# Configurations: Debug, Release

PLATFORM=${1:-win-x64}
CONFIGURATION=${2:-Release}
PROJECT_NAME="HackerOs.Host"

echo "🚀 Publication de $PROJECT_NAME pour $PLATFORM en mode $CONFIGURATION"

# Nettoyer les builds précédents
echo "🧹 Nettoyage..."
dotnet clean $PROJECT_NAME -c $CONFIGURATION

# Publier le projet
echo "📦 Publication..."
dotnet publish $PROJECT_NAME \
    -c $CONFIGURATION \
    -r $PLATFORM \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false

if [ $? -eq 0 ]; then
    echo "✅ Publication réussie!"
    echo "📂 Fichiers publiés dans: $PROJECT_NAME/bin/$CONFIGURATION/net9.0/$PLATFORM/publish/"
    
    # Afficher la taille du fichier exécutable
    if [ "$PLATFORM" = "win-x64" ]; then
        EXECUTABLE_PATH="$PROJECT_NAME/bin/$CONFIGURATION/net9.0/$PLATFORM/publish/$PROJECT_NAME.exe"
    else
        EXECUTABLE_PATH="$PROJECT_NAME/bin/$CONFIGURATION/net9.0/$PLATFORM/publish/$PROJECT_NAME"
    fi
    
    if [ -f "$EXECUTABLE_PATH" ]; then
        SIZE=$(du -h "$EXECUTABLE_PATH" | cut -f1)
        echo "📊 Taille de l'exécutable: $SIZE"
        echo "🎯 Exécutable: $EXECUTABLE_PATH"
    fi
else
    echo "❌ Échec de la publication"
    exit 1
fi
