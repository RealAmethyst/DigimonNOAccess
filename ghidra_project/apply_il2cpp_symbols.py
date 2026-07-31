# Applies Il2CppDumper's script.json to the loaded program, headless.
#
# Il2CppDumper ships ghidra.py / ghidra_with_struct.py, but both call askFile()
# to pick script.json, which is a GUI file chooser and cannot run under
# analyzeHeadless. This is the same work driven by a script argument instead.
#
# Usage (from analyzeHeadless):
#   -postScript apply_il2cpp_symbols.py <path to script.json>
#
# Symbols only. The struct variant applies il2cpp.h (22 MB of headers) and is
# both very slow and failure-prone; named functions are what make the
# decompiler readable, so that is what this does.
#
# Jython 2.7 - no f-strings, print is a statement.
# @category Il2Cpp
# @runtime Jython

import json

processFields = [
    "ScriptMethod",
    "ScriptString",
    "ScriptMetadata",
    "ScriptMetadataMethod",
    "Addresses",
]

baseAddress = currentProgram.getImageBase()
USER_DEFINED = ghidra.program.model.symbol.SourceType.USER_DEFINED


def get_addr(addr):
    return baseAddress.add(addr)


def set_name(addr, name):
    try:
        createLabel(addr, name.replace(' ', '-'), True, USER_DEFINED)
        return True
    except:
        return False


def make_function(start):
    if getFunctionAt(start) is None:
        try:
            createFunction(start, None)
        except:
            pass


args = getScriptArgs()
if len(args) < 1:
    print "ERROR: pass the path to script.json as the first script argument"
    raise Exception("missing script.json argument")

script_json = args[0]
print "Reading " + script_json
data = json.loads(open(script_json, 'rb').read().decode('utf-8'))

named = 0
failed = 0

if "ScriptMethod" in data and "ScriptMethod" in processFields:
    scriptMethods = data["ScriptMethod"]
    monitor.initialize(len(scriptMethods))
    monitor.setMessage("Methods")
    print "Naming " + str(len(scriptMethods)) + " methods"
    for scriptMethod in scriptMethods:
        addr = get_addr(scriptMethod["Address"])
        if set_name(addr, scriptMethod["Name"].encode("utf-8")):
            named += 1
        else:
            failed += 1
        monitor.incrementProgress(1)

if "ScriptString" in data and "ScriptString" in processFields:
    index = 1
    scriptStrings = data["ScriptString"]
    monitor.initialize(len(scriptStrings))
    monitor.setMessage("Strings")
    print "Labelling " + str(len(scriptStrings)) + " string literals"
    for scriptString in scriptStrings:
        addr = get_addr(scriptString["Address"])
        value = scriptString["Value"].encode("utf-8")
        try:
            createLabel(addr, "StringLiteral_" + str(index), True, USER_DEFINED)
            setEOLComment(addr, value)
        except:
            pass
        index += 1
        monitor.incrementProgress(1)

if "ScriptMetadata" in data and "ScriptMetadata" in processFields:
    scriptMetadatas = data["ScriptMetadata"]
    monitor.initialize(len(scriptMetadatas))
    monitor.setMessage("Metadata")
    print "Naming " + str(len(scriptMetadatas)) + " metadata entries"
    for scriptMetadata in scriptMetadatas:
        addr = get_addr(scriptMetadata["Address"])
        name = scriptMetadata["Name"].encode("utf-8")
        set_name(addr, name)
        try:
            setEOLComment(addr, name)
        except:
            pass
        monitor.incrementProgress(1)

if "ScriptMetadataMethod" in data and "ScriptMetadataMethod" in processFields:
    scriptMetadataMethods = data["ScriptMetadataMethod"]
    monitor.initialize(len(scriptMetadataMethods))
    monitor.setMessage("Metadata Methods")
    print "Naming " + str(len(scriptMetadataMethods)) + " metadata methods"
    for scriptMetadataMethod in scriptMetadataMethods:
        addr = get_addr(scriptMetadataMethod["Address"])
        name = scriptMetadataMethod["Name"].encode("utf-8")
        set_name(addr, name)
        try:
            setEOLComment(addr, name)
        except:
            pass
        monitor.incrementProgress(1)

if "Addresses" in data and "Addresses" in processFields:
    addresses = data["Addresses"]
    monitor.initialize(len(addresses))
    monitor.setMessage("Addresses")
    print "Creating functions at " + str(len(addresses)) + " addresses"
    for index in range(len(addresses) - 1):
        make_function(get_addr(addresses[index]))
        monitor.incrementProgress(1)

print "Done. Named " + str(named) + " methods, " + str(failed) + " failed."
