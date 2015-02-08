@echo off
cd /d "%~dp0"
set configurationName=%1
echo Compressing NetRevisionTool executable from %configurationName%...
if exist NetRevisionTool.exe.gz del NetRevisionTool.exe.gz
rem %ProgramFiles% is "C:\Program Files (x86)" in Visual Studio :(
"C:\Program Files\7-Zip\7z.exe" a NetRevisionTool.exe.gz ..\NetRevisionTool\bin\%configurationName%\NetRevisionTool.exe -mx=9 || exit 1
exit 0
