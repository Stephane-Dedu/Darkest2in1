# Using REA for Darkest2in1

Use REA when an investigation needs shipped-binary evidence, CIL inspection,
native DD1 rules unavailable in data, or comparison across DD2 builds.
Use the existing source, DD1 data and Unity port for questions they already answer.
The owner's full quest-lifecycle request supersedes waiting for R1-R10 selections.
Use [the quest map](quest_parity_map.md), latest MODLOG Status and PARITY.md for current priorities;
[the R1-R10 backlog](rea_investigation_backlog.md) remains a supporting investigation list.

## Load the skill and select the target

Read the installed upstream skill before the first REA investigation in a session:

```powershell
$reaWorkbench = Join-Path $env:USERPROFILE 'rea-workbench'
$reaPackage = Join-Path $reaWorkbench 'node_modules/rea-agents'
$reaCli = Join-Path $reaPackage 'scripts/rea.mjs'
Get-Content -LiteralPath (Join-Path $reaPackage 'skills/reverse-engineer-anything/SKILL.md')
```

Read its `references/native-and-artifacts.md` for DD1 native or DD2 managed work,
and `references/evidence-workflows.md` when comparing or verifying findings.
These are supplied by the REA package; this repository keeps the project-specific
recipe rather than a second copy of the upstream skill.

| Target | CLI entry | MCP entry, if registered |
| --- | --- | --- |
| DD2 managed `IronCrown.dll` | `inspect-managed-artifact` | `inspect_managed_artifact` |
| DD2 types, methods and CIL | `inspect-managed-members` | `inspect_managed_members` |
| DD1 native win64 `Darkest.exe` | `inspect --provider ghidra` | `open_binary` with Ghidra, then focused queries |
| Managed build comparison | `compare-managed-members` | `compare_managed_members` |

The DD2 assembly path on this machine is
`C:/Users/Piral/darkestwithdlc/game/Darkest Dungeon II_Data/Managed/IronCrown.dll`.
The native DD1 target is
`C:/Program Files (x86)/Steam/steamapps/common/DarkestDungeon/_windows/win64/Darkest.exe`.
Use the current install's exact bytes and hash. The compile reference is under
`C:/Users/Piral/dd2-decomp/refs`, and readable DD2 decompilation is under
`C:/Users/Piral/dd2-decomp/IronCrown`.

## Local CLI installation

Verified on 2026-10-06: Node 24.19.0 and REA 4.0.1. The CLI is already installed
at `C:/Users/Piral/rea-workbench`; it is independent of the mod build/runtime.
For a fresh machine, install that reproducible baseline outside the repository:

```powershell
node --version
npm install --prefix $reaWorkbench --ignore-scripts --no-audit --no-fund rea-agents@4.0.1
if ($LASTEXITCODE -ne 0) { throw 'REA installation failed' }
node $reaCli --version
```

Check the installed command's `--help` or `--schema` when its parameters are
uncertain. Record the exact version if deliberately changing the baseline.
Lifecycle scripts are disabled in this local installation. No agent registration
or global skill installation is required for direct CLI use.

## DD2 managed inspection

First check whether matching private evidence already exists. The backlog records
the successful trial's paths, artifact digest, MVID and Evidence IDs. Reuse it when
the current artifact hash and operation match. A full member inventory can exceed
200 MB; save it privately and read only relevant types/methods.

For a new artifact, these PowerShell commands inspect identity without loading
or executing the assembly:

```powershell
$dd2Assembly = 'C:/Users/Piral/darkestwithdlc/game/Darkest Dungeon II_Data/Managed/IronCrown.dll'
$dd2Digest = (Get-FileHash -LiteralPath $dd2Assembly -Algorithm SHA256).Hash.ToLowerInvariant()
$reaEvidence = Join-Path $reaWorkbench "evidence/$dd2Digest"
New-Item -ItemType Directory -Path $reaEvidence -Force | Out-Null
$identityFile = Join-Path $reaEvidence 'managed-artifact.json'
if (-not (Test-Path -LiteralPath $identityFile)) {
    node $reaCli inspect-managed-artifact $dd2Assembly --format json > $identityFile
    if ($LASTEXITCODE -ne 0) { throw 'REA identity inspection failed; inspect the private error result' }
}
$identity = Get-Content -LiteralPath $identityFile -Raw | ConvertFrom-Json
if ($identity.subject.digest.sha256 -ne $dd2Digest) { throw 'Evidence identity mismatch' }
$identity.normalized_result.classification
```

After successful identity inspection, obtain members once if needed:

```powershell
$membersFile = Join-Path $reaEvidence 'managed-members.json'
if (-not (Test-Path -LiteralPath $membersFile)) {
    node $reaCli inspect-managed-members $dd2Assembly --format json > $membersFile
    if ($LASTEXITCODE -ne 0) { throw 'REA member inspection failed; inspect the private error result' }
}
if ((Get-FileHash -LiteralPath $dd2Assembly).Hash.ToLowerInvariant() -ne $dd2Digest) {
    throw 'The target changed during inspection'
}
```

Before reusing a members result, validate its `subject.digest.sha256`, provider,
operation and `normalized_result.coverage`; read its limitations. Build-local
tokens are meaningful only with the recorded artifact SHA-256 and module MVID.
Follow the relevant type, method, CIL call edges and field accesses, then compare
with the existing decompilation and installed tables. Static CIL does not measure
startup time or establish live hook timing.

For a DD2 update, keep owned copies of both managed assemblies outside Git and
run the exact comparison command:

```powershell
# Set these to the two explicitly selected, private assembly copies.
node $reaCli compare-managed-members $baselineAssembly $candidateAssembly --format json > $comparisonFile
if ($LASTEXITCODE -ne 0) { throw 'REA managed comparison failed' }
```

## DD1 native inspection

Current readiness, checked 2026-10-06: unavailable. Published REA 4.0.1 omits the
Windows native bundle, and this machine has no configured Ghidra or full JDK.
The trial returned `provider_unavailable/not_configured`. Repository-main support
and published-package support differ. Consult the [official Windows guide](https://raw.githubusercontent.com/morluto/rea/main/docs/windows-ghidra-p0.md)
before preparing a compatible verified bundle, Ghidra 12.1.4 and full x64 JDK 21,
or a separately validated supported host. MCP registration alone cannot supply
the missing native bundle.

Once the prerequisites are actually installed and verified, use their real
absolute paths for `GHIDRA_INSTALL_DIR` and `JAVA_HOME`. Run `doctor --json` when
diagnosing provider unavailability, then `providers --json`; normal working
queries do not need repeated doctor runs. A cold import can take several minutes.
Keep progress updates flowing while the tool session runs.

The following queries are templates for a ready native provider. The address
must come from observed search/xref results:

```powershell
$dd1Binary = 'C:/Program Files (x86)/Steam/steamapps/common/DarkestDungeon/_windows/win64/Darkest.exe'
node $reaCli inspect $dd1Binary --provider ghidra --format json > $nativeOverviewFile
if ($LASTEXITCODE -ne 0) { throw 'REA native inspection failed' }
node $reaCli search $dd1Binary 'dismissed_hero_stress_penalties' --kind strings --mode literal --provider ghidra --format json > $nativeSearchFile
if ($LASTEXITCODE -ne 0) { throw 'REA native search failed' }
node $reaCli decompile $dd1Binary $observedProcedureAddress --provider ghidra --format json > $nativeFunctionFile
if ($LASTEXITCODE -ne 0) { throw 'REA native decompilation failed' }
```

Choose private output paths beforehand; check each command's exit code and result
before continuing. Follow strings to references and the actual rule consumer,
then its callers, arithmetic, state mutations and return order. A string match
alone does not establish a rule. In MCP, reuse one native session for related
queries and close it with `close_binary` when finished. CLI `--snapshot` preserves
matching cached query results locally; it does not prove unseen behavior.

## Record findings and implement one rule

1. Write the exact question and current difference in `PARITY.md` before code.
2. Record artifact/version, Evidence IDs, entry points, coverage and search bounds.
   Label each conclusion observed, inferred or unresolved.
3. Translate confirmed rules into Core using the installed DD1 data; place DD2
   bridge/UI integration in the plugin. Write a regression at the actual seam.
4. Follow the existing [parity loop](parity_loop.md) for tests, build, stopped-game
   deployment, MODLOG status, commit and push. Native behavior remains `[?]`
   until permitted in-game verification succeeds.

Keep binaries, full CIL/native output, snapshots and runtime captures under the
private workbench outside Git. Commit authored findings and instructions only.
The owner's no-launch, protected-estate and abandoned-project rules remain in
`CLAUDE.md`. Optional MCP/agent setup is a separate configuration task; this guide
uses the working direct CLI and changes no global registration.

Upstream references: [REA](https://github.com/morluto/rea),
[managed analysis](https://github.com/morluto/rea/blob/main/docs/managed-code-analysis.md),
[installation](https://github.com/morluto/rea/blob/main/docs/installation.md).
