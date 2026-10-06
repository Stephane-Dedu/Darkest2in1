// Moves virtual functions under their class, from the RTTI "vftable" labels of Ghidra's auto-analysis.
// A default-named function found only in the vftables of one class becomes <Class>::vf<slot> (vf<slot>_<n> for the
// class's n-th extra vftable). Functions shared by several classes keep their names. std/exprtk/Concurrency
// templates are skipped: identical-code folding merges their vtables. Replaces RecoverClassesFromRTTIScript,
// which stops with "More than one Base Class Array" on this exe. Arg: output directory for the decision table.
//@category DD1

import java.io.BufferedWriter;
import java.nio.file.*;
import java.util.*;
import java.util.regex.Pattern;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import ghidra.util.exception.DuplicateNameException;

public class Dd1NameVirtuals extends GhidraScript {

	private static final Pattern SKIP = Pattern.compile("^(std|exprtk|Concurrency|`)");

	@Override
	protected void run() throws Exception {
		Path out = Paths.get(getScriptArgs()[0]);
		Files.createDirectories(out);
		FunctionManager functions = currentProgram.getFunctionManager();
		int pointerSize = currentProgram.getDefaultPointerSize();

		// class namespace -> its vftables (by address); function -> classes whose vftables hold it
		Map<Namespace, List<Address>> vftables = new LinkedHashMap<>();
		Map<Function, Set<Namespace>> owners = new HashMap<>();
		Map<Address, List<Function>> slots = new HashMap<>();
		SymbolIterator it = currentProgram.getSymbolTable().getSymbols("vftable");
		while (it.hasNext()) {
			Symbol v = it.next();
			List<Function> list = new ArrayList<>();
			Address slot = v.getAddress();
			for (int i = 0; i < 1000; i++) {
				if (i > 0 && hasLabel(slot)) {
					break;
				}
				Function f;
				try {
					f = functions.getFunctionAt(toAddr(currentProgram.getMemory().getLong(slot)));
				}
				catch (Exception e) {
					break;
				}
				if (f == null) {
					break;
				}
				list.add(f);
				owners.computeIfAbsent(f, k -> new HashSet<>()).add(v.getParentNamespace());
				slot = slot.add(pointerSize);
			}
			slots.put(v.getAddress(), list);
			vftables.computeIfAbsent(v.getParentNamespace(), k -> new ArrayList<>()).add(v.getAddress());
		}

		int renamed = 0;
		try (BufferedWriter w = Files.newBufferedWriter(out.resolve("names_virtuals.tsv"))) {
			w.write("decision\tclass\tvftable\tslot\tfunction\n");
			for (var e : vftables.entrySet()) {
				Namespace cls = e.getKey();
				String clsName = cls.getName(true);
				if (SKIP.matcher(clsName).find()) {
					continue;
				}
				List<Address> tables = e.getValue();
				tables.sort(null);
				for (int t = 0; t < tables.size(); t++) {
					List<Function> list = slots.get(tables.get(t));
					for (int i = 0; i < list.size(); i++) {
						Function f = list.get(i);
						String name = "vf" + i + (t == 0 ? "" : "_" + t);
						String decision;
						if (f.isThunk() || f.isExternal()) {
							decision = "thunk-or-external";
						}
						else if (owners.get(f).size() != 1) {
							decision = "shared-by-" + owners.get(f).size() + "-classes";
						}
						else if (cls.equals(f.getParentNamespace()) && f.getName().startsWith("vf")) {
							decision = "renamed";
						}
						else if (!isDefaultName(f)) {
							decision = "already-named";
						}
						else {
							decision = rename(f, cls, name) ? "renamed" : "rename-failed";
						}
						if (decision.equals("renamed")) {
							renamed++;
						}
						w.write(decision + "\t" + clsName + "\t" + tables.get(t) + "\t" + i + "\t" +
							f.getEntryPoint() + " " + f.getSymbol().getName(true) + "\n");
					}
				}
			}
		}
		println("Dd1NameVirtuals: " + vftables.size() + " classes with vftables, " + renamed + " virtual functions named");
	}

	private boolean hasLabel(Address a) {
		for (Symbol s : currentProgram.getSymbolTable().getSymbols(a)) {
			if (s.getSource() != SourceType.DEFAULT) {
				return true;
			}
		}
		return false;
	}

	private static boolean isDefaultName(Function f) {
		return f.getSymbol().getSource() == SourceType.DEFAULT || f.getName().startsWith("FUN_");
	}

	private boolean rename(Function f, Namespace cls, String name) {
		try {
			f.setParentNamespace(cls);
			try {
				f.setName(name, SourceType.ANALYSIS);
			}
			catch (DuplicateNameException e) {
				f.setName(name + "_" + f.getEntryPoint(), SourceType.ANALYSIS);
			}
			return true;
		}
		catch (Exception e) {
			printerr("rename " + f.getEntryPoint() + " -> " + cls.getName(true) + "::" + name + ": " + e.getMessage());
			return false;
		}
	}
}
