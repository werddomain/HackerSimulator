#!/bin/bash

# Script d'installation de .NET pour HackerOs.Host
# Ce script installe .NET 8.0 et .NET 9.0 si nécessaire

echo "🔍 Vérification de l'installation de .NET..."

# Vérifier si .NET est déjà installé
if command -v dotnet &> /dev/null; then
    echo "✅ .NET est déjà installé: $(dotnet --version)"
    echo "📋 SDKs disponibles:"
    dotnet --list-sdks
else
    echo "❌ .NET n'est pas installé ou n'est pas dans le PATH"
    echo "📥 Installation de .NET 8.0..."
    
    # Installer .NET 8.0
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
    
    # Ajouter .NET au PATH
    export PATH="$HOME/.dotnet:$PATH"
    echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc
    
    if command -v dotnet &> /dev/null; then
        echo "✅ .NET 8.0 installé avec succès: $(dotnet --version)"
    else
        echo "❌ Échec de l'installation de .NET"
        exit 1
    fi
fi

echo ""
echo "🚀 .NET est prêt pour HackerOs.Host!"
echo "📂 Pour tester le projet:"
echo "   cd HackerOs.Host"
echo "   dotnet run"
