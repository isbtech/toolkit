@echo off

rem Microsoft .NET 4.0 for 64-bit system
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe

rem Microsoft .NET 2.0 for 32-bit system
rem set CSC=%WINDIR%\Microsoft.NET\Framework\v2.0.50727\csc.exe

if not exist %CSC% goto error-csc

if "%1"=="" goto usage
if "%1"=="all" goto all
if "%1"=="list" goto list
if "%1"=="wpm" goto build-wpm
if "%1"=="whois" goto build-whois
if "%1"=="geticon" goto build-geticon
if "%1"=="html2png" goto build-html2png
goto build

:build
rem "~n1" - "1" to get first argument from command line, "~n" to get file name without extension
%CSC% /target:exe /out:bin\%~n1.exe src\%~n1.cs
goto end

:build-whois
%CSC% /reference:System.Core.dll /reference:System.Xml.Linq.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Data.dll /reference:System.Xml.dll /target:exe /out:bin\whois.exe src\whois.cs
goto end

:build-geticon
%CSC% /reference:System.Drawing.dll /target:exe /out:bin\geticon.exe src\geticon.cs
goto end

:build-html2png
%CSC% /target:exe /out:bin\html2png.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /define:PNG src\html2png.cs
%CSC% /target:exe /out:bin\html2jpg.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /define:JPEG src\html2png.cs
goto end

:all
rem for /F %%i in ('dir /s /b *.cs') do (
for /F "tokens=* delims=;" %%i in ('dir /s /b *.cs') do (
	echo Building %%i
	
	rem Example for next line: "build.bat c:\toolkit\src\md5.cs"
	call %0 %%i
)
goto end

:list
rem List src file names without extension
for /F "tokens=* delims=;" %%i in ('dir /s /b *.cs') do echo %%~ni
goto end

:usage
echo Usage:
echo   make all
echo   make list
echo   make [name]
echo.
echo Example:
echo   make all
echo   make md5
echo   make whois
goto end

:test
rem todo: unit test
goto end

:error-csc
echo "csc.exe" cannot be found. Edit "build.bat" and change CSC path.
echo Path: %CSC%
goto end

:end