# Ghidra headless script to find real EnableItemPickPoint native implementation
# @category Il2Cpp

from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor

# Managed wrapper from script.json, plus nearby offsets to find the real impl
# AreaChangePatch pattern: wrapper at 0x25DA20, real impl at 0x25DAA0 (0x80 later)
targets = {
    "EnableItemPickPoint_wrapper": 0x3B8780,
    "EnableItemPickPoint_plus0x80": 0x3B8800,
    "EnableItemPickPoint_plus0xA0": 0x3B8820,
    "EnableItemPickPoint_plus0xC0": 0x3B8840,
    "EnableItemPickPoint_plus0xE0": 0x3B8860,
    "EnableItemPickPoint_plus0x100": 0x3B8880,
    "EnableItemPickPoint_plus0x120": 0x3B88A0,
    "EnableItemPickPoint_plus0x140": 0x3B88C0,
    "EnableItemPickPoint_plus0x160": 0x3B88E0,
    "EnableItemPickPoint_plus0x180": 0x3B8900,
    "EnableItemPickPoint_plus0x1A0": 0x3B8920,
    "EnableItemPickPoint_plus0x1C0": 0x3B8940,
    "EnableItemPickPoint_plus0x200": 0x3B8980,
    "EnableItemPickPoint_plus0x280": 0x3B8A00,
    "EnableItemPickPoint_plus0x300": 0x3B8A80,
    # Also check EnableAllItemPickPoint for pattern reference
    "EnableAllItemPickPoint_wrapper": 0x3B86B0,
    # For comparison: the AreaChangePatch addresses we know work
    "EnableAreaChangeTrigger_wrapper": 0x25DA20,
    "EnableAreaChangeTrigger_real": 0x25DAA0,
}

decomp = DecompInterface()
decomp.openProgram(currentProgram)
monitor = ConsoleTaskMonitor()

results = []

for name, offset in sorted(targets.items(), key=lambda x: x[1]):
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
        results.append("=== {} @ 0x{:X} (func entry: {}) ===\n{}\n".format(
            name, offset, func.getEntryPoint(), decomp_text))
    else:
        results.append("=== {} @ 0x{:X} === DECOMPILE FAILED\n".format(name, offset))

# Also scan for all functions between 0x3B8780 and 0x3B8B00
results.append("\n=== ALL FUNCTIONS IN RANGE 0x3B8780 - 0x3B8B00 ===\n")
listing = currentProgram.getListing()
func_iter = listing.getFunctions(currentProgram.getImageBase().add(0x3B8780), True)
while func_iter.hasNext():
    f = func_iter.next()
    offset = f.getEntryPoint().subtract(currentProgram.getImageBase())
    if offset > 0x3B8B00:
        break
    results.append("  Function at 0x{:X}: {} (size: {})\n".format(offset, f.getName(), f.getBody().getNumAddresses()))

import java.io
fw = java.io.FileWriter("C:\\Users\\Amethyst\\projects\\digimon world, next order\\ghidra_project\\pickpoint_output.txt")
for r in results:
    fw.write(r)
    fw.write("\n")
fw.close()

print("Done - output written to pickpoint_output.txt")
for r in results:
    print(r)
