@echo off
setlocal

rem Run this file from the Config directory.
rem This script follows the Luban v4.7.0 example style.

set LUBAN_DLL=.\Luban\Luban.dll
set CONF_ROOT=.

set CODE_DIR=..\Assets\Scripts\Game\Data\Generated
set BIN_DIR=..\Assets\Data\Bin
set JSON_DIR=..\Assets\Data\Json
set PROJECT_ROOT=..

if not exist "%CODE_DIR%" mkdir "%CODE_DIR%"
if not exist "%BIN_DIR%" mkdir "%BIN_DIR%"
if not exist "%JSON_DIR%" mkdir "%JSON_DIR%"

dotnet %LUBAN_DLL% ^
    -t client ^
    -c cs-bin ^
    -d bin ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir=%CODE_DIR% ^
    -x outputDataDir=%BIN_DIR% ^
    -x pathValidator.rootDir=%PROJECT_ROOT%

if errorlevel 1 (
    echo Generate bin failed.
    pause
    exit /b 1
)

dotnet %LUBAN_DLL% ^
    -t client ^
    -d json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputDataDir=%JSON_DIR% ^
    -x pathValidator.rootDir=%PROJECT_ROOT%

if errorlevel 1 (
    echo Generate json failed.
    pause
    exit /b 1
)

echo Generate data success.
REM pause