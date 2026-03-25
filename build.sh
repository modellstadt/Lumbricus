#!/bin/bash
cd "$(dirname "$0")/Lumbricus"
dotnet build && cp bin/Debug/net7.0/Lumbricus.dll "$HOME/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/Grasshopper (b45a29b1-4343-4035-989e-044e8580d9cf)/Libraries/Lumbricus.gha" && echo "✓ Copied to GH Libraries. Close+reopen Grasshopper to reload."
