@echo off
cd /d "%~dp0"
echo Compressing Manual.txt file...
gzip -c9 Manual.txt >Manual.txt.gz || exit 1
exit 0
