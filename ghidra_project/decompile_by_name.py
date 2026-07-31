# Decompile functions whose name contains a substring.
#
# Usage: run_ghidra.bat decompile_by_name.py <substring> [maxResults]
#
# Il2CppDumper names look like Namespace$$Class$$Method, so a substring such as
# "PartnerData$$AddSatiety" or just "AddSatiety" finds it.
#
# Jython 2.7 - no f-strings, print is a statement.
# @category Il2Cpp
# @runtime Jython

from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor

args = getScriptArgs()
if len(args) < 1:
    print "ERROR: pass a name substring as the first script argument"
    raise Exception("missing substring argument")

needle = args[0].lower()
limit = int(args[1]) if len(args) > 1 else 5

fm = currentProgram.getFunctionManager()
matches = []
for f in fm.getFunctions(True):
    if needle in f.getName().lower():
        matches.append(f)
        if len(matches) >= limit:
            break

print "=== %d match(es) for '%s' ===" % (len(matches), args[0])

if not matches:
    # Labels are applied even where no function was created, so fall back to those.
    st = currentProgram.getSymbolTable()
    shown = 0
    for sym in st.getAllSymbols(True):
        if needle in sym.getName().lower():
            print "LABEL %s at %s" % (sym.getName(), sym.getAddress())
            shown += 1
            if shown >= limit:
                break
    if shown == 0:
        print "nothing found"

decomp = DecompInterface()
decomp.openProgram(currentProgram)
monitor = ConsoleTaskMonitor()

for f in matches:
    print ""
    print "----- %s  @ %s -----" % (f.getName(), f.getEntryPoint())
    res = decomp.decompileFunction(f, 60, monitor)
    if res.decompileCompleted():
        print res.getDecompiledFunction().getC()
    else:
        print "decompilation failed: " + str(res.getErrorMessage())

decomp.dispose()
print "done"
