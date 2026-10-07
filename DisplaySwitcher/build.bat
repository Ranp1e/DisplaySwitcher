@echo off
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:anycpu /utf8output /codepage:65001 /win32manifest:app.manifest /out:DisplaySwitcher.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll *.cs
if errorlevel 1 (
    echo BUILD FAILED
    exit /b 1
)
echo BUILD OK: DisplaySwitcher.exe
