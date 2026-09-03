@echo off
REM ============================================================
REM  Uninstall RawPrintBridge Windows Service
REM  !! Run this script as Administrator !!
REM ============================================================
set SERVICENAME=RawPrintBridge

sc stop %SERVICENAME%
sc delete %SERVICENAME%

echo Service removed.
pause
