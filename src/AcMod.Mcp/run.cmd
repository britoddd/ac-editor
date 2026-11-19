@echo off
rem Starts acmod-mcp from a copy of its build output, so a running server never locks bin\ and a build always works (M8).
rem Each start gets its own folder under %TEMP%\acmod-mcp; it holds program files only, never car data.
rem Old copies are removed only when no server uses them: Windows refuses to rename a folder while a running program has files open in it.
setlocal
set "ROOT=%TEMP%\acmod-mcp"
for /d %%d in ("%ROOT%\*") do ren "%%d" "%%~nxd.old" 2>nul && rd /s /q "%%d.old"
set "DEST=%ROOT%\%RANDOM%%RANDOM%"
robocopy "%~dp0bin\Debug\net10.0" "%DEST%" /e /njh /njs /nfl /ndl /np >nul
if not exist "%DEST%\acmod-mcp.dll" (
  echo acmod-mcp isn't built. Run: dotnet build AcMod.slnx 1>&2
  exit /b 1
)
dotnet "%DEST%\acmod-mcp.dll"
