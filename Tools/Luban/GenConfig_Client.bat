Cd /d %~dp0
echo %CD%

set WORKSPACE=..\..

set LUBAN_DLL=%WORKSPACE%\Tools\Luban\LubanRelease\Luban.dll
set CONF_ROOT=%WORKSPACE%\Config\Excel\GameConfig

::Client
dotnet %LUBAN_DLL% ^
    -t Client ^
    -c cs-bin ^
    -d bin ^
    --conf %CONF_ROOT%\__luban__.conf ^
    -x outputCodeDir=%WORKSPACE%\Unity\Assets\GameScripts\GameProto\Generate\Config ^
    -x bin.outputDataDir=%WORKSPACE%\Config\Generate\GameConfig\c ^
    -x lineEnding=CRLF

if errorlevel 1 exit /b %ERRORLEVEL%


echo ==================== FuncConfig : GenClientFinish ====================

pause
