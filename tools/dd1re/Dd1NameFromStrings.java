// Names DD1 functions from the qualified names Darkest.exe keeps in its own log and assert
// strings ("Darkest::RaidFinish", "Estate::...: message"). Only renames functions that still have
// a default name, when exactly one function uses the string and that function uses no other such
// name; everything else is reported, not applied. Overrides vf<slot> names from Dd1NameVirtuals.
// Arg: output directory for the decision table.
//@category DD1

import java.io.BufferedWriter;
import java.nio.file.*;
import java.util.*;
import java.util.regex.*;

import ghidra.app.script.GhidraScript;
import ghidra.app.util.NamespaceUtils;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;

public class Dd1NameFromStrings extends GhidraScript {

	// A qualified name at the start of the string. Group 2 is what follows it.
	private static final Pattern QUALIFIED =
		Pattern.compile("^((?:[A-Za-z_][A-Za-z0-9_]*::)+~?[A-Za-z_][A-Za-z0-9_]*)(.?)");

	@Override
	protected void run() throws Exception {
		Path out = Paths.get(getScriptArgs()[0]);
		Files.createDirectories(out);

		// qualified name -> functions referencing a string that starts with it
		Map<String, Set<Function>> users = new TreeMap<>();
		// qualified names that appear as a whole string or before ':' / '(' (function-name style)
		Set<String> functionStyle = new HashSet<>();
		Map<String, String> example = new HashMap<>();

		DataIterator it = currentProgram.getListing().getDefinedData(true);
		while (it.hasNext() && !monitor.isCancelled()) {
			Data d = it.next();
			if (!d.hasStringValue() || !(d.getValue() instanceof String s)) {
				continue;
			}
			Matcher m = QUALIFIED.matcher(s);
			if (!m.find() || m.group(1).startsWith("std::")) {
				continue;
			}
			String name = m.group(1);
			String next = m.group(2);
			if (next.isEmpty() || next.equals(":") || next.equals("(")) {
				functionStyle.add(name);
			}
			example.putIfAbsent(name, s);
			for (Reference r : getReferencesTo(d.getAddress())) {
				Function f = getFunctionContaining(r.getFromAddress());
				if (f != null) {
					users.computeIfAbsent(name, k -> new LinkedHashSet<>()).add(f);
				}
			}
		}

		Map<Function, Set<String>> namesUsed = new HashMap<>();
		for (var e : users.entrySet()) {
			for (Function f : e.getValue()) {
				namesUsed.computeIfAbsent(f, k -> new TreeSet<>()).add(e.getKey());
			}
		}

		int renamed = 0;
		try (BufferedWriter w = Files.newBufferedWriter(out.resolve("names_from_strings.tsv"))) {
			w.write("decision\tqualified_name\tfunctions\texample_string\n");
			for (var e : users.entrySet()) {
				String name = e.getKey();
				Set<Function> fs = e.getValue();
				String decision;
				if (!functionStyle.contains(name)) {
					decision = "label-only";
				}
				else if (fs.size() != 1) {
					decision = "several-functions";
				}
				else if (namesUsed.get(fs.iterator().next()).size() != 1) {
					decision = "function-uses-several-names";
				}
				else if (fs.iterator().next().getSymbol().getName(true).equals(name)) {
					decision = "renamed"; // applied by an earlier run
					renamed++;
				}
				else if (!isDefaultName(fs.iterator().next())) {
					decision = "already-named";
				}
				else {
					decision = rename(fs.iterator().next(), name) ? "renamed" : "rename-failed";
					if (decision.equals("renamed")) {
						renamed++;
					}
				}
				StringBuilder where = new StringBuilder();
				for (Function f : fs) {
					where.append(where.length() == 0 ? "" : ",").append(f.getEntryPoint());
				}
				w.write(decision + "\t" + name + "\t" + where + "\t" +
					example.get(name).replace('\t', ' ').replace('\n', ' ') + "\n");
			}
		}
		println("Dd1NameFromStrings: " + users.size() + " qualified names, " + renamed + " renamed");
	}

	private static boolean isDefaultName(Function f) {
		String n = f.getName();
		// vf<slot> names come from Dd1NameVirtuals; DD1's own string names are more specific
		return f.getSymbol().getSource() == SourceType.DEFAULT || n.startsWith("FUN_") ||
			n.matches("vf\\d+(_\\d+)?(_[0-9a-f]+)?");
	}

	private boolean rename(Function f, String qualified) {
		int cut = qualified.lastIndexOf("::");
		try {
			Namespace ns = NamespaceUtils.createNamespaceHierarchy(qualified.substring(0, cut), null,
				currentProgram, SourceType.ANALYSIS);
			f.setParentNamespace(ns);
			f.setName(qualified.substring(cut + 2), SourceType.ANALYSIS);
			f.setComment(appendLine(f.getComment(),
				"dd1re: named from a log/assert string; could be an inlined callee's name"));
			return true;
		}
		catch (Exception e) {
			printerr("rename " + f.getEntryPoint() + " -> " + qualified + ": " + e.getMessage());
			return false;
		}
	}

	private static String appendLine(String existing, String line) {
		return existing == null || existing.isEmpty() ? line : existing + "\n" + line;
	}
}
