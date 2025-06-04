#!/bin/bash

# Script de déploiement pour HackerOs.Host
# Ce script compile le projet Blazor WebAssembly et le déploie dans le Host

echo "🚀 Déploiement de HackerOS vers HackerOs.Host"

# Configuration
HACKEROS_PROJECT="../HackerOs/HackerOs.csproj"
HOST_WWWROOT="./wwwroot"
BUILD_CONFIG="Release"

# Vérifier que le projet HackerOs existe
if [ ! -f "$HACKEROS_PROJECT" ]; then
    echo "❌ Projet HackerOs introuvable: $HACKEROS_PROJECT"
    echo "💡 Ce script doit être exécuté depuis le dossier HackerOs.Host"
    exit 1
fi

echo "📁 Nettoyage du dossier wwwroot..."
rm -rf "$HOST_WWWROOT"/*

echo "🔨 Compilation du projet HackerOs en mode $BUILD_CONFIG..."
dotnet publish "$HACKEROS_PROJECT" -c $BUILD_CONFIG -o "./temp_publish"

if [ $? -eq 0 ]; then
    echo "✅ Compilation réussie!"
    
    # Copier les fichiers publiés vers wwwroot
    echo "📦 Copie des fichiers vers wwwroot..."
    cp -r "./temp_publish/wwwroot/"* "$HOST_WWWROOT/"
    
    # Nettoyer le dossier temporaire
    rm -rf "./temp_publish"
    
    echo "✅ Déploiement terminé!"
    echo "🌐 Vous pouvez maintenant lancer: dotnet run"
else
    echo "❌ Échec de la compilation du projet HackerOs"
    echo "💡 Vérifiez que le projet HackerOs compile correctement"
    exit 1
fi
