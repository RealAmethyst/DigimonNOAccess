# Ghidra headless script to decompile EnableAreaChangeTrigger and related methods
# @category Il2Cpp

from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor

# Method offsets from Il2CppDumper script.json
targets = {
    "EnableAreaChangeTrigger": 0x25DA20,
    "_ChangeAreaByTrigger": 0x253590,
    "AreaChangeMapTriggerCB": 0x251DB0,
    "ClearAreaChange": 0x276DB0,
}

decomp = DecompInterface()
decomp.openProgram(currentProgram)
monitor = ConsoleTaskMonitor()

output_path = askString("Output", "Output file path") if False else None

results = []

for name, offset in targets.items():
    addr = currentProgram.getImageBase().add(offset)
    func = getFunctionAt(addr)
    if func is None:
        func = createFunction(addr, name)
    if func is None:
        results.append("=== {} @ 0x{:X} === COULD NOT CREATE FUNCTION\n".format(name, offset))
        continue

    res = decomp.decompileFunction(func, 120, monitor)
    if res and res.decompileCompleted():
        decomp_text = res.getDecompiledFunction().getC()
        results.append("=== {} @ 0x{:X} ===\n{}\n".format(name, offset, decomp_text))
    else:
        results.append("=== {} @ 0x{:X} === DECOMPILE FAILED\n".format(name, offset))

# Write results to file
import java.io
fw = java.io.FileWriter("C:\\Users\\Amethyst\\projects\\digimon world, next order\\ghidra_project\\decompiled_output.txt")
for r in results:
    fw.write(r)
    fw.write("\n")
fw.close()

print("Done - output written to decompiled_output.txt")
for r in results:
    print(r)
