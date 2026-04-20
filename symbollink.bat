@echo off
REM Change to script directory (project root)
cd /d "%~dp0"

REM Check if .clinerules already exists
if exist .clinerules (
    echo [INFO] .clinerules already exists
    echo [INFO] Delete it first if you want to recreate the link
) else (
    echo [INFO] Creating symlink: .clinerules -^> .cursor
    mklink /d .clinerules .cursor
    if %errorlevel% equ 0 (
        echo [SUCCESS] Symlink created successfully
    ) else (
        echo [ERROR] Failed to create symlink
        echo [ERROR] Please run this script as Administrator
    )
)
pause