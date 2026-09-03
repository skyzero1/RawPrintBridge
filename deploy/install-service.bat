@echo off
REM ============================================================
REM  Install RawPrintBridge as a Windows Service
REM  !! Run this script as Administrator !!
REM  Put this .bat in the same folder as RawPrintBridge.exe
REM ============================================================
set SERVICENAME=RawPrintBridge
set BINPATH=%~dp0RawPrintBridge.exe

if not exist "%BINPATH%" (
  echo [ERROR] RawPrintBridge.exe not found at %BINPATH%
  pause
  exit /b 1
)

sc create %SERVICENAME% binPath= "%BINPATH%" start= auto DisplayName= "Raw Print Bridge Service"
sc description %SERVICENAME% "Receives RAW print jobs on TCP 9100 and forwards them to the local printer. Config: appsettings.json"
sc failure %SERVICENAME% reset= 86400 actions= restart/5000/restart/5000/restart/5000
sc start %SERVICENAME%

echo.
echo Service installed and started. Edit appsettings.json to change config,
REM then restart with:  sc stop RawPrintBridge  &&  sc start RawPrintBridge
pause
