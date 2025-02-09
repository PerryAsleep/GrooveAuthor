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

REM copy Linux fmod libraries
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\linux\x86_64\libfmod.so" "%TARGET_DIR%\libfmod.so"
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\linux\x86_64\libfmod.so.13" "%TARGET_DIR%\libfmod.so.13"
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\linux\x86_64\libfmod.so.13.3" "%TARGET_DIR%\libfmod.so.13.3"
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\linux\x86_64\libfmodL.so" "%TARGET_DIR%\libfmodL.so"
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\linux\x86_64\libfmodL.so.13" "%TARGET_DIR%\libfmodL.so.13"
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\StepManiaLibrary\Fumen\Fumen\Sound\fmod\lib\linux\x86_64\libfmodL.so.13.3" "%TARGET_DIR%\libfmodL.so.13.3"

REM copy Linux cimgui libraries
echo F | xcopy /f /d /y /i "%PROJECT_DIR%\..\ImGui.NET\deps\cimgui\linux-x64\cimgui.so" "%TARGET_DIR%\cimgui.so"
