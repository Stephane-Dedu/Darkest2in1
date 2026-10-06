// Exports raw facts about Darkest.exe for tools/dd1re/build_map.py, as JSON lines:
//   functions.jsonl  every function: entry, qualified name, size, signature, callers, callees and
//                    the C strings its instructions reference (catches short keys Ghidra left undefined)
//   strings.jsonl    every defined string: the functions using it, directly or through one data
//                    table (arrays of const char* such as enum name tables)
//   vtables.jsonl    every RTTI "vftable" label: its class, slot functions and the functions loading its
//                    address (constructors, destructors, or code that inlines them)
// Arg: output directory. Read-only.
//@category DD1

import java.io.BufferedWriter;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

import com.google.gson.*;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.mem.*;
import ghidra.program.model.symbol.*;

public class Dd1ExportFacts extends GhidraScript {

	private static final int MAX_STRING = 400;
	private final Gson gson = new GsonBuilder().disableHtmlEscaping().create();
	private Memory memory;

	@Override
	protected void run() throws Exception {
		Path out = Paths.get(getScriptArgs()[0]);
		Files.createDirectories(out);
		memory = currentProgram.getMemory();
		exportFunctions(out.resolve("functions.jsonl"));
		exportStrings(out.resolve("strings.jsonl"));
		exportVtables(out.resolve("vtables.jsonl"));
	}

	private void exportFunctions(Path file) throws Exception {
		Listing listing = currentProgram.getListing();
		int count = 0;
		try (BufferedWriter w = Files.newBufferedWriter(file)) {
			for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
				monitor.checkCancelled();
				JsonObject o = new JsonObject();
				o.addProperty("entry", f.getEntryPoint().toString());
				o.addProperty("name", f.getSymbol().getName(true));
				o.addProperty("size", f.getBody().getNumAddresses());
				o.addProperty("thunk", f.isThunk());
				o.addProperty("signature", f.getPrototypeString(false, false));
				o.add("callers", entries(f.getCallingFunctions(monitor)));
				o.add("callees", entries(f.getCalledFunctions(monitor)));
				Set<String> strings = new LinkedHashSet<>();
				for (Instruction ins : listing.getInstructions(f.getBody(), true)) {
					for (Reference r : ins.getReferencesFrom()) {
						String s = readCString(r.getToAddress());
						if (s != null) {
							strings.add(s);
						}
					}
				}
				JsonArray sa = new JsonArray();
				strings.forEach(sa::add);
				o.add("strings", sa);
				w.write(gson.toJson(o));
				w.write('\n');
				count++;
			}
		}
		println("Dd1ExportFacts: " + count + " functions");
	}

	private void exportStrings(Path file) throws Exception {
		Listing listing = currentProgram.getListing();
		int count = 0;
		try (BufferedWriter w = Files.newBufferedWriter(file)) {
			DataIterator it = listing.getDefinedData(true);
			while (it.hasNext()) {
				monitor.checkCancelled();
				Data d = it.next();
				if (!d.hasStringValue() || !(d.getValue() instanceof String s)) {
					continue;
				}
				Set<Function> direct = new LinkedHashSet<>();
				Set<Function> viaTable = new LinkedHashSet<>();
				for (Reference r : getReferencesTo(d.getAddress())) {
					Function f = getFunctionContaining(r.getFromAddress());
					if (f != null) {
						direct.add(f);
						continue;
					}
					Data holder = listing.getDataContaining(r.getFromAddress());
					if (holder == null) {
						continue;
					}
					for (Reference tr : getReferencesTo(holder.getAddress())) {
						Function tf = getFunctionContaining(tr.getFromAddress());
						if (tf != null) {
							viaTable.add(tf);
						}
					}
				}
				JsonObject o = new JsonObject();
				o.addProperty("addr", d.getAddress().toString());
				o.addProperty("value", s.length() > MAX_STRING ? s.substring(0, MAX_STRING) : s);
				o.add("functions", entries(direct));
				o.add("via_table", entries(viaTable));
				w.write(gson.toJson(o));
				w.write('\n');
				count++;
			}
		}
		println("Dd1ExportFacts: " + count + " strings");
	}

	private void exportVtables(Path file) throws Exception {
		SymbolTable symbols = currentProgram.getSymbolTable();
		FunctionManager functions = currentProgram.getFunctionManager();
		int pointerSize = currentProgram.getDefaultPointerSize();
		int count = 0;
		try (BufferedWriter w = Files.newBufferedWriter(file)) {
			SymbolIterator it = symbols.getSymbols("vftable");
			while (it.hasNext()) {
				monitor.checkCancelled();
				Symbol v = it.next();
				JsonArray slots = new JsonArray();
				Address slot = v.getAddress();
				for (int i = 0; i < 1000; i++) {
					if (i > 0 && hasLabel(symbols, slot)) {
						break;
					}
					Function target;
					try {
						long p = pointerSize == 8 ? memory.getLong(slot) : memory.getInt(slot) & 0xffffffffL;
						target = functions.getFunctionAt(toAddr(p));
					}
					catch (Exception e) {
						break;
					}
					if (target == null) {
						break;
					}
					slots.add(target.getEntryPoint() + " " + target.getSymbol().getName(true));
					slot = slot.add(pointerSize);
				}
				Set<Function> users = new LinkedHashSet<>();
				for (Reference r : getReferencesTo(v.getAddress())) {
					Function f = getFunctionContaining(r.getFromAddress());
					if (f != null) {
						users.add(f);
					}
				}
				JsonObject o = new JsonObject();
				o.addProperty("addr", v.getAddress().toString());
				o.addProperty("class", v.getParentNamespace().getName(true));
				o.add("slots", slots);
				o.add("referenced_by", entries(users));
				w.write(gson.toJson(o));
				w.write('\n');
				count++;
			}
		}
		println("Dd1ExportFacts: " + count + " vftables");
	}

	private static boolean hasLabel(SymbolTable symbols, Address a) {
		for (Symbol s : symbols.getSymbols(a)) {
			if (s.getSource() != SourceType.DEFAULT) {
				return true;
			}
		}
		return false;
	}

	private static JsonArray entries(Collection<Function> fs) {
		JsonArray a = new JsonArray();
		for (Function f : fs) {
			a.add(f.getEntryPoint().toString());
		}
		return a;
	}

	/**
	 * The string at a non-code address: Ghidra's defined string there, else a printable
	 * NUL-terminated ASCII run of at least 2 characters (short keys stay undefined), else null.
	 */
	private String readCString(Address a) {
		MemoryBlock block = memory.getBlock(a);
		if (block == null || block.isExecute() || !block.isInitialized()) {
			return null;
		}
		Data defined = currentProgram.getListing().getDataAt(a);
		if (defined != null && defined.hasStringValue() && defined.getValue() instanceof String s) {
			return s.length() > MAX_STRING ? s.substring(0, MAX_STRING) : s;
		}
		byte[] buf = new byte[MAX_STRING + 1];
		int n;
		try {
			n = memory.getBytes(a, buf);
		}
		catch (MemoryAccessException e) {
			return null;
		}
		int len = 0;
		while (len < n && buf[len] != 0) {
			int b = buf[len] & 0xff;
			if ((b < 0x20 && b != '\t' && b != '\n' && b != '\r') || b > 0x7e) {
				return null;
			}
			len++;
		}
		if (len < 2) {
			return null;
		}
		return new String(buf, 0, Math.min(len, MAX_STRING), StandardCharsets.US_ASCII);
	}
}
