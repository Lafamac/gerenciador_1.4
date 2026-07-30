@echo off
setlocal

set "CSC_PATH=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC_PATH%" (
  echo Compilador C# do .NET Framework nao encontrado.
  exit /b 1
)

if not exist "dist" mkdir "dist"

"%CSC_PATH%" /nologo /target:winexe /platform:x86 /optimize+ /warn:4 ^
  /out:"dist\GerenciadorColheita.exe" ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  Program.cs MainForm.cs HidDevice.cs EepromReport.cs PdfReportWriter.cs

if errorlevel 1 exit /b 1

echo Aplicativo gerado em dist\GerenciadorColheita.exe
exit /b 0
