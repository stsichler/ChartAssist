@echo off
rem Baut ChartAssist frisch (Debug) und startet es. Nur für die Entwicklung, nicht Teil der Releases.
cd /d "%~dp0" && dotnet run --project src\ChartAssist %*
