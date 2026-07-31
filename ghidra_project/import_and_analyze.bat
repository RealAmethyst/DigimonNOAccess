@echo off
REM One-off: import GameAssembly.dll, analyse it, then apply Il2CppDumper symbol names.
REM Takes hours. Run in the background and leave it.
REM NOTE: every SET is quoted as set "VAR=value" - the (x86) in the game path
REM breaks cmd's parser otherwise.
setlocal
set "GHIDRA=H:\projects\ghidra_11.3.2_PUBLIC\support\analyzeHeadless.bat"
set "PROJDIR=C:\Users\Amethyst\ghidra_dwno"
set "PROJ=DWNO"
set "GAMEDIR=C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order"
set "SCRIPTS=%~dp0"

if not exist "%PROJDIR%" mkdir "%PROJDIR%"

call "%GHIDRA%" "%PROJDIR%" "%PROJ%" -import "%GAMEDIR%\GameAssembly.dll" -scriptPath "%SCRIPTS%" -postScript apply_il2cpp_symbols.py "%GAMEDIR%\Il2CppDumper\script.json" -scriptlog "%SCRIPTS%import.log" -max-cpu 6 -overwrite
endlocal
