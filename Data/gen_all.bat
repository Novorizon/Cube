@echo off
CALL  gen_client.bat
IF ERRORLEVEL 1 EXIT /B 1
CALL  gen_wave_all_no_overwrite.bat
IF ERRORLEVEL 1 EXIT /B 1
