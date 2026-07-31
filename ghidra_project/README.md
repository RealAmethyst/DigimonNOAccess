# Ghidra setup for GameAssembly.dll

Why this exists: `decompiled\` is an Il2CppInterop proxy dump. It gives every
class, field, method signature and enum, but every method body is an
`il2cpp_runtime_invoke` thunk. It cannot tell you what a function *does*.
Ghidra on `GameAssembly.dll` can.

Nothing in this folder is committed (see `.gitignore`) — Ghidra work products
and the analysed database stay local.

## Paths

- Ghidra 11.3.2: `H:\projects\ghidra_11.3.2_PUBLIC`
- Project (local disk, deliberately not on the H: share — Ghidra is very slow
  over a network share): `C:\Users\Amethyst\ghidra_dwno`, project name `DWNO`
- Binary: `C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order\GameAssembly.dll`
- Il2CppDumper output: `C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order\Il2CppDumper\`
  — `script.json` (method names and RVAs), `il2cpp.h` (structs), `dump.cs`
  (full class dump with field offsets), `stringliteral.json`

## First-time setup

`import_and_analyze.bat` does the one-off work: imports GameAssembly.dll,
runs full analysis, then applies Il2CppDumper's symbol names via
`apply_il2cpp_symbols.py`.

This takes hours — a 26 MB IL2CPP binary with hundreds of thousands of
functions. Run it once, in the background, and leave it.

## Running a script afterwards

`run_ghidra.bat <script.py>` — opens the already-analysed project with
`-noanalysis` and runs your script. Scripts live in this folder and are
Jython 2.7 (no f-strings, `print` is a statement).

## Why symbols only, not structs

Il2CppDumper also ships `ghidra_with_struct.py`, which additionally parses
`il2cpp.h` — 22 MB of C headers. That pass is extremely slow and frequently
fails part way. Named functions are what make the decompiler readable, so
`apply_il2cpp_symbols.py` does names, string literals and function creation
only. If a specific struct is ever needed, import that one type by hand.

Both of Il2CppDumper's own scripts call `askFile()`, a GUI file chooser, so
neither works under `analyzeHeadless`. `apply_il2cpp_symbols.py` is the same
work driven by a script argument instead.
