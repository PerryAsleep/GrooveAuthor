REM Build script invoked from Visual Studio

set PROJECT_DIR=%1
set TARGET_DIR=%2

REM remove quotes
set PROJECT_DIR=%PROJECT_DIR:"=%
REM remove trailing \
if %PROJECT_DIR:~-1%==\ set PROJECT_DIR=%PROJECT_DIR:~0,-1%

REM remove quotes
set TARGET_DIR=%TARGET_DIR:"=%
REM remove trailing \
if %TARGET_DIR:~-1%==\ set TARGET_DIR=%TARGET_DIR:~0,-1%

REM copy fmod libraries
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\x64\fmod.dll" "%TARGET_DIR%\fmod.dll"
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\x64\fmod_vc.lib" "%TARGET_DIR%\fmod_vc.lib"
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\x64\fmodL.dll" "%TARGET_DIR%\fmodL.dll"
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\x64\fmodL_vc.lib" "%TARGET_DIR%\fmodL_vc.lib"

REM remove MonoGame test Effects folder
@RD /S /Q "%TARGET_DIR%\Effects"