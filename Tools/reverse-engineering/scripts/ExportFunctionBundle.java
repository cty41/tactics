// ExportFunctionBundle.java
// Ghidra headless script: export decompile text, callers, callees, and string references.
//@category Analysis

import java.io.File;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.JsonArray;
import com.google.gson.JsonObject;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileOptions;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressRange;
import ghidra.program.model.address.AddressSetView;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionManager;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceManager;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolTable;

public class ExportFunctionBundle extends GhidraScript {

    private static final int DEFAULT_TIMEOUT = 600;

    @Override
    public void run() throws Exception {
        Map<String, String> args = parseArgs(getScriptArgs());
        String mode = args.getOrDefault("mode", "entry-bundle");
        String outDir = args.getOrDefault("outDir", "D:\\Ghi\\export");

        if (!"entry-bundle".equals(mode)) {
            println("ERROR: only mode=entry-bundle is supported, got: " + mode);
            return;
        }

        String targetsRaw = args.get("targets");
        if (targetsRaw == null || targetsRaw.isEmpty()) {
            println("ERROR: targets parameter is required");
            return;
        }

        File dir = new File(outDir);
        if (!dir.exists() && !dir.mkdirs()) {
            println("ERROR: failed to create output directory: " + outDir);
            return;
        }

        DecompInterface decompiler = setupDecompiler();
        if (decompiler == null) {
            return;
        }

        try {
            FunctionManager functionManager = currentProgram.getFunctionManager();
            Gson gson = new GsonBuilder().setPrettyPrinting().disableHtmlEscaping().create();
            JsonArray index = new JsonArray();

            for (String rawTarget : targetsRaw.split(",")) {
                String target = rawTarget.trim();
                if (target.isEmpty() || monitor.isCancelled()) {
                    continue;
                }

                Function function = findFunction(functionManager, target);
                if (function == null) {
                    println("WARN: function not found: " + target);
                    continue;
                }

                println("Processing: " + function.getName() + " @ " + function.getEntryPoint());
                JsonObject bundle = exportBundle(function, decompiler);
                String safeName = function.getName().replaceAll("[^a-zA-Z0-9_\\.\\-]", "_");
                File outFile = new File(dir, safeName + ".json");
                try (PrintWriter writer = new PrintWriter(new FileWriter(outFile))) {
                    writer.print(gson.toJson(bundle));
                }

                JsonObject item = new JsonObject();
                item.addProperty("name", function.getName());
                item.addProperty("address", function.getEntryPoint().toString());
                item.addProperty("file", safeName + ".json");
                index.add(item);
            }

            try (PrintWriter writer = new PrintWriter(new FileWriter(new File(dir, "index.json")))) {
                writer.print(gson.toJson(index));
            }
        } finally {
            decompiler.dispose();
        }
    }

    private Map<String, String> parseArgs(String[] scriptArgs) {
        Map<String, String> result = new HashMap<>();
        if (scriptArgs == null) {
            return result;
        }
        for (String arg : scriptArgs) {
            if (arg == null) {
                continue;
            }
            int separator = arg.indexOf('=');
            if (separator > 0) {
                result.put(arg.substring(0, separator).trim(), arg.substring(separator + 1).trim());
            }
        }
        return result;
    }

    private DecompInterface setupDecompiler() {
        DecompInterface decompiler = new DecompInterface();
        DecompileOptions options = new DecompileOptions();
        options.setMaxWidth(200);
        decompiler.setOptions(options);
        decompiler.toggleCCode(true);
        decompiler.toggleSyntaxTree(true);
        decompiler.setSimplificationStyle("decompile");
        if (!decompiler.openProgram(currentProgram)) {
            println("ERROR: decompiler.openProgram failed: " + decompiler.getLastMessage());
            decompiler.dispose();
            return null;
        }
        return decompiler;
    }

    private Function findFunction(FunctionManager functionManager, String name) {
        SymbolTable symbolTable = currentProgram.getSymbolTable();
        for (Symbol symbol : symbolTable.getSymbols(name)) {
            if (symbol.getObject() instanceof Function) {
                return (Function) symbol.getObject();
            }
        }

        String addressText = name.startsWith("0x") ? name.substring(2) : name;
        try {
            long offset = Long.parseUnsignedLong(addressText, 16);
            Address address = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(offset);
            Function function = functionManager.getFunctionAt(address);
            if (function != null) {
                return function;
            }
        } catch (NumberFormatException ignored) {
            // The target is a symbol rather than an address.
        }

        for (Function function : functionManager.getFunctions(true)) {
            if (function.getName().contains(name)) {
                return function;
            }
        }
        return null;
    }

    private JsonObject exportBundle(Function function, DecompInterface decompiler) throws Exception {
        JsonObject result = new JsonObject();
        result.addProperty("name", function.getName());
        result.addProperty("address", function.getEntryPoint().toString());
        result.addProperty("signature", function.getSignature().getPrototypeString());
        result.addProperty("decompile", decompile(function, decompiler));

        JsonArray callers = new JsonArray();
        List<Function> callerFunctions = new ArrayList<>(function.getCallingFunctions(monitor));
        Collections.sort(callerFunctions, (left, right) -> left.getEntryPoint().compareTo(right.getEntryPoint()));
        for (Function caller : callerFunctions) {
            callers.add(functionReference(caller));
        }
        result.add("callers", callers);

        JsonArray callees = new JsonArray();
        List<Function> calleeFunctions = new ArrayList<>(function.getCalledFunctions(monitor));
        Collections.sort(calleeFunctions, (left, right) -> left.getEntryPoint().compareTo(right.getEntryPoint()));
        for (Function callee : calleeFunctions) {
            callees.add(functionReference(callee));
        }
        result.add("callees", callees);

        JsonArray strings = new JsonArray();
        Set<String> seen = new HashSet<>();
        AddressSetView body = function.getBody();
        Memory memory = currentProgram.getMemory();
        ReferenceManager referenceManager = currentProgram.getReferenceManager();

        for (AddressRange range : body) {
            for (Address address = range.getMinAddress(); address.compareTo(range.getMaxAddress()) <= 0; address = address.next()) {
                if (monitor.isCancelled()) {
                    break;
                }
                for (Reference reference : referenceManager.getReferencesFrom(address)) {
                    Address target = reference.getToAddress();
                    if (target == null) {
                        continue;
                    }
                    String value = readStringAt(memory, target);
                    if (value != null && seen.add(value)) {
                        JsonObject stringReference = new JsonObject();
                        stringReference.addProperty("value", value.length() > 512 ? value.substring(0, 512) + "..." : value);
                        stringReference.addProperty("address", target.toString());
                        stringReference.addProperty("refSite", address.toString());
                        strings.add(stringReference);
                    }
                }
            }
        }
        result.add("strings", strings);
        return result;
    }

    private JsonObject functionReference(Function function) {
        JsonObject result = new JsonObject();
        result.addProperty("name", function.getName());
        result.addProperty("address", function.getEntryPoint().toString());
        return result;
    }

    private String decompile(Function function, DecompInterface decompiler) {
        DecompileResults results = decompiler.decompileFunction(function, DEFAULT_TIMEOUT, monitor);
        if (results == null || !results.decompileCompleted()) {
            return "// DECOMPILE FAILED: " + (results == null ? "null result" : results.getErrorMessage());
        }
        return results.getDecompiledFunction().getC();
    }

    private String readStringAt(Memory memory, Address address) {
        try {
            MemoryBlock block = memory.getBlock(address);
            if (block == null || !block.isInitialized()) {
                return null;
            }

            byte[] buffer = new byte[1024];
            int bytesRead = memory.getBytes(address, buffer);
            if (bytesRead <= 0) {
                return null;
            }

            int length = 0;
            while (length < bytesRead && buffer[length] != 0
                    && (buffer[length] >= 0x20 || buffer[length] == '\n' || buffer[length] == '\r' || buffer[length] == '\t')) {
                length++;
            }
            if (length >= 3) {
                return new String(buffer, 0, length, "UTF-8");
            }

            int wideLength = 0;
            while (wideLength + 1 < bytesRead) {
                char character = (char) ((buffer[wideLength] & 0xff) | ((buffer[wideLength + 1] & 0xff) << 8));
                if (character == 0 || (character < 0x20 && character != '\n' && character != '\r' && character != '\t')) {
                    break;
                }
                wideLength += 2;
            }
            if (wideLength >= 6) {
                StringBuilder builder = new StringBuilder();
                for (int index = 0; index < wideLength; index += 2) {
                    builder.append((char) ((buffer[index] & 0xff) | ((buffer[index + 1] & 0xff) << 8)));
                }
                return builder.toString();
            }
        } catch (Exception ignored) {
            // A failed speculative string read is not an export failure.
        }
        return null;
    }
}
