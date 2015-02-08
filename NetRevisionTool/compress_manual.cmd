@echo off
cd /d "%~dp0"
echo Compressing Manual.txt file...
if exist Manual.txt.gz del Manual.txt.gz
rem %ProgramFiles% is "C:\Program Files (x86)" in Visual Studio :(
"C:\Program Files\7-Zip\7z.exe" a Manual.txt.gz Manual.txt -mx=9 || exit 1
exit 0
