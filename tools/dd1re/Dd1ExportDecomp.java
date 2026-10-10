// Decompiles every non-thunk function of Darkest.exe into one C file per function:
//   <out>/<Namespace>/<Class>/<name>@<entry>.c    for named classes/namespaces
//   <out>/_global/<first 5 hex digits>/<name>@<entry>.c    for global functions (64 KB ranges)
// Each file starts with a header: qualified name, entry, size, callers and the DD1 source files
// named by its assert strings. The output is decompiled game code: keep it private, never commit it.
// Existing files are kept, so an interrupted run resumes (clear the directory after renaming functions).
// Args: output directory, then optionally the number of decompiler threads (default 4: each thread runs
// its own native decompiler process, and all of them at once starve a 16 GB machine). Read-only.
//@category DD1

import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.regex.*;

import generic.concurrent.GThreadPool;
import ghidra.app.decompiler.*;
import ghidra.app.decompiler.parallel.*;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import ghidra.util.task.TaskMonitor;

public class Dd1ExportDecomp extends GhidraScript {

	private static final Pattern SOURCE = Pattern.compile("source_code\\\\(.+\\.(?:cpp|h|hpp|inl))$");
	private static final int TIMEOUT_SECONDS = 90;
	private static final int MAX_CALLERS = 15;

	@Override
	protected void run() throws Exception {
		Path out = Paths.get(getScriptArgs()[0]);
		Files.createDirectories(out);
		Program program = currentProgram;
		String header = "// Darkest.exe " + program.getExecutableSHA256() +
			", Ghidra decompilation. Private: never commit or share.\n";
		AtomicInteger ok = new AtomicInteger();
		AtomicInteger failed = new AtomicInteger();

		DecompilerCallback<Void> callback = new DecompilerCallback<>(program, d -> {
			d.toggleCCode(true);
			d.toggleSyntaxTree(true);
			d.setSimplificationStyle("decompile");
			DecompileOptions options = new DecompileOptions();
			options.grabFromProgram(program);
			d.setOptions(options);
		}) {
			@Override
			public Void process(DecompileResults results, TaskMonitor m) throws Exception {
				Function f = results.getFunction();
				StringBuilder text = new StringBuilder(header);
				text.append("// ").append(f.getSymbol().getName(true)).append(" @ ")
						.append(f.getEntryPoint()).append("  size=").append(f.getBody().getNumAddresses())
						.append('\n');
				appendCallers(text, f, m);
				appendSources(text, f);
				DecompiledFunction c = results.getDecompiledFunction();
				if (results.decompileCompleted() && c != null) {
					text.append('\n').append(c.getC());
					ok.incrementAndGet();
				}
				else {
					text.append("\n// decompile failed: ").append(results.getErrorMessage()).append('\n');
					failed.incrementAndGet();
				}
				Path file = out.resolve(relativePath(f));
				Files.createDirectories(file.getParent());
				Path partial = file.resolveSibling(file.getFileName() + ".partial");
				Files.writeString(partial, text, StandardCharsets.UTF_8);
				Files.move(partial, file, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
				return null;
			}
		};
		callback.setTimeout(TIMEOUT_SECONDS);

		List<Function> todo = new ArrayList<>();
		int kept = 0;
		for (Function f : program.getFunctionManager().getFunctions(true)) {
			if (f.isThunk() || f.isExternal()) {
				continue;
			}
			if (Files.exists(out.resolve(relativePath(f)))) {
				kept++;
			}
			else {
				todo.add(f);
			}
		}
		int threads = getScriptArgs().length > 1 ? Integer.parseInt(getScriptArgs()[1]) : 4;
		GThreadPool.getSharedThreadPool("Parallel Decompiler").setMaxThreadCount(threads);
		println("Dd1ExportDecomp: decompiling " + todo.size() + " functions on " + threads + " threads, " +
			kept + " already written");
		try {
			ParallelDecompiler.decompileFunctions(callback, program, todo.iterator(), r -> {}, monitor);
		}
		finally {
			callback.dispose();
		}
		println("Dd1ExportDecomp: " + ok.get() + " decompiled, " + failed.get() + " failed");
	}

	private static void appendCallers(StringBuilder text, Function f, TaskMonitor m) {
		Set<Function> callers = f.getCallingFunctions(m);
		text.append("// callers (").append(callers.size()).append("):");
		int shown = 0;
		for (Function c : callers) {
			if (shown++ == MAX_CALLERS) {
				text.append(" ...");
				break;
			}
			text.append(' ').append(c.getSymbol().getName(true)).append('@').append(c.getEntryPoint());
		}
		text.append('\n');
	}

	private void appendSources(StringBuilder text, Function f) {
		Set<String> sources = new TreeSet<>();
		Listing listing = currentProgram.getListing();
		for (Instruction ins : listing.getInstructions(f.getBody(), true)) {
			for (Reference r : ins.getReferencesFrom()) {
				Data d = listing.getDataAt(r.getToAddress());
				if (d != null && d.hasStringValue() && d.getValue() instanceof String s) {
					Matcher m = SOURCE.matcher(s);
					if (m.find()) {
						sources.add(m.group(1).replace('\\', '/'));
					}
				}
			}
		}
		if (!sources.isEmpty()) {
			text.append("// source files (assert strings): ").append(String.join(", ", sources)).append('\n');
		}
	}

	/** Short, Windows-safe relative path; stays well under MAX_PATH. */
	private static Path relativePath(Function f) {
		List<String> dirs = new ArrayList<>();
		Namespace ns = f.getParentNamespace();
		while (ns != null && !ns.isGlobal()) {
			dirs.add(0, safe(ns.getName(), 40));
			ns = ns.getParentNamespace();
		}
		if (dirs.isEmpty()) {
			String hex = f.getEntryPoint().toString();
			dirs.add("_global");
			dirs.add(hex.substring(0, Math.min(5, hex.length())));
		}
		while (dirs.size() > 3) {
			dirs.remove(1);
		}
		Path p = Paths.get(dirs.get(0), dirs.subList(1, dirs.size()).toArray(new String[0]));
		return p.resolve(safe(f.getName(), 80) + "@" + f.getEntryPoint() + ".c");
	}

	private static String safe(String s, int max) {
		String t = s.replaceAll("[^A-Za-z0-9_.~-]", "_");
		return t.length() > max ? t.substring(0, max) : t;
	}
}
