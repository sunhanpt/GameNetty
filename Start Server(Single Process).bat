@echo off
setlocal
set "serverRoot=%~dp0"
pushd "%serverRoot%" || exit /b 1
powershell.exe -NoProfile -File "%serverRoot%Tools\Test-LocalServerPorts.ps1"
set "serverExit=%errorlevel%"
if not "%serverExit%"=="0" goto failed

echo [GameNetty] Building the local server...
dotnet msbuild "Server\Server.sln" -restore -m:1 -nologo -v:minimal
set "serverExit=%errorlevel%"
if not "%serverExit%"=="0" goto failed

cd /d "%serverRoot%Bin"
set "serverExit=%errorlevel%"
if not "%serverExit%"=="0" goto failed
".\App.exe" --Process=1 --StartConfig=StartConfig/Localhost --Console=1
set "serverExit=%errorlevel%"
if not "%serverExit%"=="0" goto failed
popd
exit /b 0

:failed
echo [GameNetty] Startup stopped. Exit code %serverExit%.
pause
popd
exit /b %serverExit%
