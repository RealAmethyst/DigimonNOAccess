@echo off
REM Run a script against the already-analysed project.
REM Usage: run_ghidra.bat myscript.py [arg1] [arg2] [arg3]
setlocal
set "GHIDRA=H:\projects\ghidra_11.3.2_PUBLIC\support\analyzeHeadless.bat"
set "PROJDIR=C:\Users\Amethyst\ghidra_dwno"
set "PROJ=DWNO"
set "SCRIPTS=%~dp0"

call "%GHIDRA%" "%PROJDIR%" "%PROJ%" -process GameAssembly.dll -noanalysis -scriptPath "%SCRIPTS%" -postScript %1 %2 %3 %4 -scriptlog "%SCRIPTS%script.log"
endlocal
