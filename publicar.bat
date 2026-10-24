@echo off
rem Gera o BaraoTechExplode pronto para levar a outro PC (nao precisa instalar o .NET la).
rem Saida: ..\BaraoTechExplode-dist\BaraoTechExplode.exe + LEIA-ME.txt
rem Sempre recompila do zero a partir do codigo atual desta pasta.
cd /d "%~dp0"
echo Feche o BaraoTechExplode normalmente antes de publicar para permitir a restauracao das sessoes.
echo Gerando BaraoTechExplode.exe a partir do codigo atual ...
rmdir /s /q obj\Release >nul 2>&1
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o "..\BaraoTechExplode-dist" -nologo -v q
if errorlevel 1 (
    echo.
    echo FALHOU. Veja os erros acima. O .exe antigo NAO foi atualizado.
    pause
    exit /b 1
)
copy /y "LEIA-ME.txt" "..\BaraoTechExplode-dist\LEIA-ME.txt" >nul
copy /y "LICENSE" "..\BaraoTechExplode-dist\LICENSE" >nul
echo.
echo Pronto: %~dp0..\BaraoTechExplode-dist
for %%F in ("..\BaraoTechExplode-dist\BaraoTechExplode.exe") do echo Arquivo gerado em %%~tF (confira se e de agora^)
pause
