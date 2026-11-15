"""Offline QA and manual acceptance ledger. Never launches, deploys, or opens saves."""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
HOME = ROOT / '.qa'
MANIFEST = Path(__file__).with_name('steps.json')


def now():
    return datetime.now(timezone.utc).isoformat()


def read_json(path, default=None):
    return json.loads(path.read_text(encoding='utf-8')) if path.exists() else default


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix('.new')
    temp.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    os.replace(temp, path)


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args], text=True, encoding='utf-8').strip()


def source_id(ref):
    # Documentation updates do not invalidate executable test evidence.
    entries = git('ls-tree', '-r', ref).splitlines()
    inputs = [line for line in entries if not line.split('\t')[-1].lower().endswith('.md')]
    return hashlib.sha256('\n'.join(inputs).encode()).hexdigest()


def empty_state():
    return {'version': 1, 'runs': [], 'builds': [], 'manual': []}


@contextmanager
def ledger_lock():
    HOME.mkdir(parents=True, exist_ok=True)
    lock = HOME / 'running.lock'
    # Exclusive creation prevents two agents from overwriting progress.
    with lock.open('x', encoding='utf-8') as handle:
        handle.write(json.dumps({'pid': os.getpid(), 'started': now()}))
    try:
        yield
    finally:
        lock.unlink()


def parse_trx(path):
    root = ET.parse(path).getroot()
    counters = root.find('.//{*}Counters')
    if counters is None:
        raise ValueError('TRX has no counters')
    counts = {k: int(counters.get(k, '0')) for k in ('total', 'executed', 'passed', 'failed', 'notExecuted')}
    results = root.findall('.//{*}UnitTestResult')
    counts['bad_results'] = [r.get('testName') for r in results if r.get('outcome') != 'Passed']
    counts['ok'] = (counts['total'] > 0 and counts['total'] == counts['passed']
                    and counts['executed'] == counts['total'] and len(results) == counts['total']
                    and not counts['bad_results'])
    return counts


def parse_unittest(output):
    matches = re.findall(r'^Ran (\d+) tests? in ', output, re.MULTILINE)
    count = int(matches[-1]) if matches else 0
    # Skipped tests are not accepted as complete coverage.
    return {'total': count, 'ok': count > 0 and bool(re.search(r'^OK\s*$', output, re.MULTILINE))}


def covered(state, suite, current):
    candidates = [r for r in state['runs'] if r['source_id'] == current
                  and (r['suite'] == suite or r['suite'] == 'full')]
    return bool(candidates and candidates[-1]['status'] == 'pass')


def next_action(state, manifest, current):
    if not covered(state, 'smoke', current):
        return 'Run offline tests: python tools/qa/run.py run --suite full --dd1 <DD1-data-folder>'
    if not state['builds']:
        return 'Capture the installed build: python tools/qa/run.py baseline --game-dir <DD2-folder>. Then prepare step 01. Game launch still needs current permission.'
    build = state['builds'][-1]['id']
    manual = {r['step']: r for r in state['manual'] if r['build'] == build}
    failed = [s for s in manifest['steps'] if manual.get(s['id'], {}).get('outcome') == 'fail']
    pending = [s for s in manifest['steps'] if manual.get(s['id'], {}).get('outcome') != 'pass']
    todo = failed or pending
    if not todo:
        return 'This installed build passed the manual cycle. Review the balance notes, then start the next build/region iteration.'
    step = todo[0]
    if not covered(state, step['suite'], current):
        return f"Run offline suite {step['suite']} before step {step['id']}: {step['title']}"
    outcome = manual.get(step['id'], {}).get('outcome', 'pending')
    return f"Step {step['id']} [{outcome}]: {step['title']}. Follow PLAYTEST_RUNBOOK.md#{step['anchor']}; record the user's observed result."


def freeze(ref, destination):
    archive = destination.parent / 'source.zip'
    subprocess.run(['git', '-C', str(ROOT), 'archive', '--format=zip', '-o', str(archive), ref], check=True)
    destination.mkdir()
    with zipfile.ZipFile(archive) as z:
        for member in z.infolist():
            target = (destination / member.filename).resolve()
            if not target.is_relative_to(destination.resolve()):
                raise ValueError('Archive path escapes frozen source')
        z.extractall(destination)
    archive.unlink()


def execute(command, folder, log, env, timeout):
    print('Running:', ' '.join(map(str, command)), flush=True)
    with log.open('w', encoding='utf-8') as handle:
        try:
            result = subprocess.run(command, cwd=folder, env=env, stdout=handle,
                                    stderr=subprocess.STDOUT, timeout=timeout)
            return result.returncode
        except subprocess.TimeoutExpired:
            handle.write(f'\nQA TIMEOUT after {timeout} seconds\n')
            return 124


def run_tests(args, state, manifest):
    dd1 = Path(args.dd1 or os.environ.get('DD3_TEST_DD1', '') or state.get('dd1', '')).resolve()
    if not (dd1 / 'scripts/map_generator.darkest').is_file() or not (dd1 / 'dungeons').is_dir():
        raise ValueError('Supply --dd1 with a valid DD1 data directory; automatic Steam discovery is disabled for this runner.')
    revision = git('rev-parse', '--verify', args.ref + '^{tree}')
    identity = source_id(revision)
    run_id = datetime.now().strftime('%Y%m%d-%H%M%S-') + uuid.uuid4().hex[:6]
    folder = HOME / 'runs' / run_id
    folder.mkdir(parents=True)
    source = folder / 'source'
    record = {'id': run_id, 'started': now(), 'suite': args.suite, 'ref': args.ref,
              'tree': revision, 'source_id': identity, 'dd1': str(dd1), 'status': 'running',
              'working_tree_status': git('status', '--short'), 'checks': []}
    write_json(folder / 'result.json', record)
    env = dict(os.environ, DD3_TEST_DD1=str(dd1), DOTNET_CLI_TELEMETRY_OPTOUT='1', PYTHONUTF8='1')
    try:
        freeze(revision, source)
        # Run only test projects. Every invocation also suppresses all deploy targets.
        for kind, selection in manifest['suites'][args.suite].items():
            log = folder / f'{kind}.log'
            check = {'kind': kind, 'log': str(log), 'ok': False}
            try:
                if kind in ('core', 'ui'):
                    project = 'Core' if kind == 'core' else 'Ui'
                    trx = folder / f'{kind}.trx'
                    command = ['dotnet', 'test', f'tests/DarkestDungeon3.{project}.Tests', '-c', 'Release',
                               '-p:Deploy=false', '--logger', f'trx;LogFileName={trx.name}',
                               '--results-directory', str(folder), '--nologo']
                    if selection != ['*']:
                        command += ['--filter', '|'.join('FullyQualifiedName~.' + name + '.' for name in selection)]
                    check['command'] = command
                    check['exit_code'] = execute(command, source, log, env, args.timeout)
                    check.update(parse_trx(trx))
                else:
                    # Each Python module gets its own discover run and count.
                    check['modules'] = []
                    for module in selection:
                        module_log = folder / f'{module}.log'
                        command = [sys.executable, '-m', 'unittest', 'discover', '-s', 'tests', '-p', module, '-v']
                        code = execute(command, source, module_log, env, args.timeout)
                        counts = parse_unittest(module_log.read_text(encoding='utf-8', errors='replace'))
                        check['modules'].append(dict(counts, exit_code=code, log=str(module_log)))
                    check['ok'] = all(m['ok'] and m['exit_code'] == 0 for m in check['modules'])
                    check['exit_code'] = 0 if check['ok'] else 1
                    check['total'] = sum(m['total'] for m in check['modules'])
                check['ok'] = check['ok'] and check['exit_code'] == 0
            except (OSError, ValueError, ET.ParseError) as error:
                check['error'] = str(error)
            record['checks'].append(check)
            write_json(folder / 'result.json', record)
            print(f"{kind}: {'PASS' if check['ok'] else 'FAIL'} ({check.get('total', 0)} tests). Evidence: {folder}", flush=True)
        record['status'] = 'pass' if record['checks'] and all(c['ok'] for c in record['checks']) else 'fail'
    except (Exception, KeyboardInterrupt) as error:
        record['status'] = 'interrupted' if isinstance(error, KeyboardInterrupt) else 'fail'
        record['error'] = str(error)
    record['finished'] = now()
    write_json(folder / 'result.json', record)
    state['dd1'] = str(dd1)
    state['runs'].append({k: record[k] for k in ('id', 'suite', 'source_id', 'status', 'finished')})
    print(f"{record['status'].upper()}: {folder / 'result.json'}", flush=True)
    return 0 if record['status'] == 'pass' else 1


def installed_build(game):
    game = Path(game).resolve()
    plugin = game / 'BepInEx/plugins/DarkestDungeon3'
    if not (game / 'Darkest Dungeon II.exe').is_file() or not (plugin / 'DarkestDungeon3.dll').is_file():
        raise ValueError('This is not the modded DD2 install')
    files = [p for p in plugin.rglob('*') if p.is_file() and p.suffix.lower() != '.pdb']
    files += list((game / 'BepInEx/config').glob('*DarkestDungeon3*.cfg'))
    files += list((game / 'BepInEx/config').glob('*darkestdungeon3*.cfg'))
    # Include private art bytes, not just their manifests. Never traverse the save directory.
    for name in ('PrivateScenery', 'PrivateCurios'):
        if (game / name).exists():
            files += [p for p in (game / name).rglob('*') if p.is_file()]
    hashes = {}
    for path in sorted(set(files)):
        with path.open('rb') as handle:
            hashes[path.relative_to(game).as_posix()] = hashlib.file_digest(handle, 'sha256').hexdigest()
    identity = hashlib.sha256(json.dumps(hashes, sort_keys=True).encode()).hexdigest()
    return {'id': identity, 'time': now(), 'game_dir': str(game), 'hashes': hashes,
            'scope': 'mod plugin/data, mod config, PrivateScenery and PrivateCurios; excludes saves, base game and external assets'}


def main():
    manifest = read_json(MANIFEST)
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    sub.add_parser('status')
    sub.add_parser('next')
    run = sub.add_parser('run')
    run.add_argument('--suite', choices=manifest['suites'], default='full')
    run.add_argument('--dd1')
    run.add_argument('--ref', default='HEAD', help='Commit or reviewed Git tree; working edits are never implicitly included')
    run.add_argument('--timeout', type=int, default=900, help='Seconds per test process')
    baseline = sub.add_parser('baseline')
    baseline.add_argument('--game-dir', required=True)
    record = sub.add_parser('record')
    record.add_argument('step', choices=[s['id'] for s in manifest['steps']])
    record.add_argument('outcome', choices=['pass', 'fail', 'blocked', 'pending'])
    record.add_argument('--note', required=True)
    record.add_argument('--evidence', action='append', default=[])
    record.add_argument('--metrics', help='JSON measurements from this playtest, retained verbatim')
    args = parser.parse_args()
    try:
        if args.action in ('status', 'next'):
            state = read_json(HOME / 'state.json', empty_state())
            current = source_id('HEAD')
            if args.action == 'status':
                print('Offline source:', git('rev-parse', '--short', 'HEAD'))
                for r in state['runs'][-6:]:
                    print(r['id'], r['suite'], r['status'], '(current source)' if r['source_id'] == current else '(older source)')
                build = state['builds'][-1]['id'] if state['builds'] else None
                print('Installed build:', build or 'not recorded')
                records = {r['step']: r for r in state['manual'] if r['build'] == build}
                for step in manifest['steps']:
                    r = records.get(step['id'], {})
                    print(step['id'], r.get('outcome', 'pending'), step['title'], r.get('note', ''))
            print(next_action(state, manifest, current))
            return 0
        with ledger_lock():
            state = read_json(HOME / 'state.json', empty_state())
            code = 0
            if args.action == 'run':
                code = run_tests(args, state, manifest)
            elif args.action == 'baseline':
                build = installed_build(args.game_dir)
                state['builds'].append(build)
                print(f"Installed build {build['id'][:12]}: {len(build['hashes'])} files. No save access or launch.")
            else:
                if not state['builds']:
                    raise ValueError('Record an installed baseline before entering manual results')
                if not args.note.strip():
                    raise ValueError('A manual verdict needs an observed result')
                # Refresh identity so a changed install cannot inherit an old pass.
                build = installed_build(state['builds'][-1]['game_dir'])
                if build['id'] != state['builds'][-1]['id']:
                    raise ValueError('Installed files changed. Capture a new baseline before recording this test.')
                entry = {'step': args.step, 'outcome': args.outcome, 'note': args.note,
                         'evidence': args.evidence, 'time': now(), 'build': build['id']}
                if args.metrics:
                    entry['metrics'] = json.loads(Path(args.metrics).read_text(encoding='utf-8'))
                state['manual'].append(entry)
                print(f"Recorded step {args.step}: {args.outcome}")
            write_json(HOME / 'state.json', state)
            return code
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print(f'QA stopped: {error}', file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
