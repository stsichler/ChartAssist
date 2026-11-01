#!/bin/sh
# Baut ChartAssist frisch (Debug) und startet es. Nur für die Entwicklung, nicht Teil der Releases.
cd "$(dirname "$0")" && exec dotnet run --project src/ChartAssist "$@"
