@echo off
setlocal enabledelayedexpansion

rem Run this file from the Config directory.
rem Generate bytes/json for Assets\Data\Excel\Wave\wave*.xlsx.
rem This script does not overwrite Assets\Data\Excel\wave.xlsx.
rem This script only clears:
rem   Assets\Data\Bin\Wave
rem   Assets\Data\Json\Wave
rem It does not clear or overwrite gen_client.bat outputs outside Wave folders.

set LUBAN_DLL=.\Luban\Luban.dll
set PROJECT_ROOT=..

set EXCEL_ROOT=..\Assets\Data\Excel
set WAVE_EXCEL_DIR=%EXCEL_ROOT%\Wave

set BIN_ROOT=..\Assets\Data\Bin
set JSON_ROOT=..\Assets\Data\Json
set BIN_WAVE_DIR=%BIN_ROOT%\Wave
set JSON_WAVE_DIR=%JSON_ROOT%\Wave

set TEMP_ROOT=.\TempWaveOutput
set TEMP_BIN_DIR=%TEMP_ROOT%\Bin
set TEMP_JSON_DIR=%TEMP_ROOT%\Json

set TEMP_CONF=.\luban_wave_temp.conf
set TEMP_DEFINES=.\Defines_WaveTemp
set TEMP_XML=%TEMP_DEFINES%\wave.xml

set GENERATED_BIN=%TEMP_BIN_DIR%\tbwave.bytes
set GENERATED_JSON=%TEMP_JSON_DIR%\tbwave.json

if not exist "%LUBAN_DLL%" (
    echo Missing Luban dll: %LUBAN_DLL%
    pause
    exit /b 1
)

if not exist "%WAVE_EXCEL_DIR%" (
    echo Missing wave excel directory: %WAVE_EXCEL_DIR%
    pause
    exit /b 1
)

rem Clean only wave output folders.
if exist "%BIN_WAVE_DIR%" (
    rmdir /S /Q "%BIN_WAVE_DIR%"
)

if exist "%JSON_WAVE_DIR%" (
    rmdir /S /Q "%JSON_WAVE_DIR%"
)

mkdir "%BIN_WAVE_DIR%"
mkdir "%JSON_WAVE_DIR%"

rem Clean temp files.
if exist "%TEMP_CONF%" del "%TEMP_CONF%"

if exist "%TEMP_DEFINES%" (
    rmdir /S /Q "%TEMP_DEFINES%"
)

if exist "%TEMP_ROOT%" (
    rmdir /S /Q "%TEMP_ROOT%"
)

mkdir "%TEMP_DEFINES%"
mkdir "%TEMP_BIN_DIR%"
mkdir "%TEMP_JSON_DIR%"

set FOUND=0

for %%F in ("%WAVE_EXCEL_DIR%\wave*.xlsx") do (
    set FOUND=1
    set WAVE_NAME=%%~nF
    set WAVE_INPUT=Wave/%%~nxF

    echo.
    echo Generate !WAVE_NAME! from !WAVE_INPUT!

    call :clean_temp_output
    call :write_temp_conf
    call :write_temp_xml "!WAVE_INPUT!"

    dotnet %LUBAN_DLL% ^
        -t client ^
        -d bin ^
        --conf %TEMP_CONF% ^
        -x outputDataDir=%TEMP_BIN_DIR% ^
        -x pathValidator.rootDir=%PROJECT_ROOT%

    if errorlevel 1 (
        echo Generate bin failed: !WAVE_NAME!
        goto failed
    )

    dotnet %LUBAN_DLL% ^
        -t client ^
        -d json ^
        --conf %TEMP_CONF% ^
        -x outputDataDir=%TEMP_JSON_DIR% ^
        -x pathValidator.rootDir=%PROJECT_ROOT%

    if errorlevel 1 (
        echo Generate json failed: !WAVE_NAME!
        goto failed
    )

    if not exist "%GENERATED_BIN%" (
        echo Missing generated bin: %GENERATED_BIN%
        goto failed
    )

    if not exist "%GENERATED_JSON%" (
        echo Missing generated json: %GENERATED_JSON%
        goto failed
    )

    if not exist "%BIN_WAVE_DIR%" mkdir "%BIN_WAVE_DIR%"
    if not exist "%JSON_WAVE_DIR%" mkdir "%JSON_WAVE_DIR%"

    copy /Y "%GENERATED_BIN%" "%BIN_WAVE_DIR%\!WAVE_NAME!.bytes" >nul

    if errorlevel 1 (
        echo Copy bin failed: !WAVE_NAME!
        goto failed
    )

    copy /Y "%GENERATED_JSON%" "%JSON_WAVE_DIR%\!WAVE_NAME!.json" >nul

    if errorlevel 1 (
        echo Copy json failed: !WAVE_NAME!
        goto failed
    )

    echo Generated !WAVE_NAME!.bytes and !WAVE_NAME!.json
)

if "%FOUND%"=="0" (
    echo No wave*.xlsx found in %WAVE_EXCEL_DIR%
)

goto success


:clean_temp_output
if exist "%TEMP_BIN_DIR%" (
    rmdir /S /Q "%TEMP_BIN_DIR%"
)

if exist "%TEMP_JSON_DIR%" (
    rmdir /S /Q "%TEMP_JSON_DIR%"
)

mkdir "%TEMP_BIN_DIR%"
mkdir "%TEMP_JSON_DIR%"
exit /b 0


:write_temp_conf
(
echo {
echo   "groups":
echo   [
echo     {"names":["client"], "default":true}
echo   ],
echo   "schemaFiles":
echo   [
echo     {"fileName":"Defines_WaveTemp", "type":""}
echo   ],
echo   "dataDir": "../Assets/Data/Excel",
echo   "targets":
echo   [
echo     {"name":"client", "manager":"Tables", "groups":["client"], "topModule":"Game"}
echo   ],
echo   "xargs":
echo   [
echo   ]
echo }
) > "%TEMP_CONF%"
exit /b 0


:write_temp_xml
set INPUT_FILE=%~1
(
echo ^<?xml version="1.0" encoding="UTF-8"?^>
echo ^<module name=""^>
echo   ^<bean name="WaveConfig"^>
echo     ^<var name="id" type="int"/^>
echo     ^<var name="npcConfigId" type="int"/^>
echo     ^<var name="count" type="int"/^>
echo     ^<var name="interval" type="float"/^>
echo     ^<var name="startDelay" type="float"/^>
echo     ^<var name="spawnMode" type="int"/^>
echo     ^<var name="description" type="string"/^>
echo   ^</bean^>
echo.
echo   ^<table name="TbWave" value="WaveConfig" input="%INPUT_FILE%" mode="map" index="id"/^>
echo ^</module^>
) > "%TEMP_XML%"
exit /b 0


:success
call :cleanup

echo.
echo Generate all wave data success.
pause
exit /b 0


:failed
call :cleanup

echo.
echo Generate wave data failed.
pause
exit /b 1


:cleanup
if exist "%TEMP_CONF%" del "%TEMP_CONF%"

if exist "%TEMP_DEFINES%" (
    rmdir /S /Q "%TEMP_DEFINES%"
)

if exist "%TEMP_ROOT%" (
    rmdir /S /Q "%TEMP_ROOT%"
)

exit /b 0