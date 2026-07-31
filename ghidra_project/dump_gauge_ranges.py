# Decompiles the PartnerData care-stat setters so their real clamp ranges can be
# read off, rather than inferred from MIN_/MAX_ constant names that may belong to
# a different scale entirely.
#
# Usage: run_ghidra.bat dump_gauge_ranges.py
#
# Jython 2.7 - no f-strings, print is a statement.
# @category Il2Cpp
# @runtime Jython

from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor

targets = [
    "PartnerData$$AddSatiety",
    "PartnerData$$SetSatiety",
    "PartnerData$$AddFatigue",
    "PartnerData$$SetFatigue",
    "PartnerData$$AddMood",
    "PartnerData$$SetMood",
    "PartnerData$$AddBonds",
    "PartnerData$$SetBonds",
    "PartnerData$$AddUpbringing",
    "PartnerData$$SetUpbringing",
    "PartnerData$$AddCurse",
    "PartnerData$$SetCurse",
]

fm = currentProgram.getFunctionManager()
byName = {}
for f in fm.getFunctions(True):
    byName[f.getName()] = f

decomp = DecompInterface()
decomp.openProgram(currentProgram)
monitor = ConsoleTaskMonitor()

for t in targets:
    hit = None
    for name in byName:
        if name.endswith(t) or t in name:
            hit = byName[name]
            break
    if hit is None:
        print ""
        print "----- %s : NOT FOUND -----" % t
        continue

    print ""
    print "----- %s  @ %s -----" % (hit.getName(), hit.getEntryPoint())
    res = decomp.decompileFunction(hit, 60, monitor)
    if res.decompileCompleted():
        print res.getDecompiledFunction().getC()
    else:
        print "decompilation failed: " + str(res.getErrorMessage())

decomp.dispose()
print "done"
