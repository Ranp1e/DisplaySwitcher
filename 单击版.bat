@echo off
setlocal enabledelayedexpansion

set "SCRIPTDIR=%~dp0"
set "VBS=%SCRIPTDIR%Run.vbs"
set "ROOT=HKCU\Software\Classes\DesktopBackground\Shell"

:menu
cls
echo ============================
echo   右键分辨率管理器（一级菜单版）
echo ============================
echo 1. 添加分辨率
echo 2. 删除分辨率
echo 3. 查看当前已添加的项
echo 4. 退出
echo.
set /p choice=请选择: 

if "%choice%"=="1" goto add
if "%choice%"=="2" goto del
if "%choice%"=="3" goto list
if "%choice%"=="4" exit /b
goto menu

:add
echo.
set /p W=请输入宽度 (如 1920): 
set /p H=请输入高度 (如 1080): 
set /p R=请输入刷新率 (如 240，不填则保持当前刷新率): 

set "KEYNAME=QuickRes_%W%x%H%"
set "LABEL=切换到 %W%x%H%"
if not "%R%"=="" set "LABEL=%LABEL% @%R%Hz"

reg add "%ROOT%\%KEYNAME%" /ve /t REG_SZ /d "%LABEL%" /f >nul
reg add "%ROOT%\%KEYNAME%" /v "Icon" /t REG_SZ /d "shell32.dll,-14" /f >nul
reg add "%ROOT%\%KEYNAME%\command" /ve /t REG_SZ /d "wscript.exe \"%VBS%\" %W% %H% %R%" /f >nul

echo.
echo 已添加：%LABEL%
pause
goto menu

:del
echo.
echo 当前已添加的项（键名在最后一段 QuickRes_ 开头）：
reg query "%ROOT%" 2>nul | findstr /i "QuickRes_"
echo.
echo 请输入要删除的分辨率，格式如 1920x1080（不带QuickRes_前缀）
set /p DELRES=分辨率: 
reg delete "%ROOT%\QuickRes_%DELRES%" /f
echo.
echo 已删除
pause
goto menu

:list
echo.
reg query "%ROOT%" 2>nul | findstr /i "QuickRes_"
echo.
pause
goto menu
