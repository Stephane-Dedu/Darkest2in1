"""Read installed DD2 tables into a private, provenance-preserving item catalogue.

Standard library only. Never imports the game, opens saves or changes installed data.
The output contains native data and must stay outside repositories and releases.
"""
import argparse
import csv
import hashlib
import json
from collections import Counter, defaultdict, deque
from datetime import datetime, timezone
from pathlib import Path


def read_tables(root):
    records, sources = [], []
    for path in sorted(root.rglob('*.csv')):
        source = path.relative_to(root).as_posix()
        sources.append({'path': source, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        current = None
        with path.open(encoding='utf-8-sig', newline='') as stream:
            reader = csv.reader(stream)
            for row in reader:
                if not row or not row[0]:
                    continue
                if row[0] == 'element_start':
                    if current or len(row) < 3 or not row[1] or not row[2]:
                        raise ValueError(f'Invalid block start: {source}:{reader.line_num}')
                    current = {'key': f'{source}:{reader.line_num}', 'id': row[1],
                               'kind': row[2], 'source': source, 'line': reader.line_num,
                               'rows': []}
                elif row[0] == 'element_end':
                    if current is None:
                        raise ValueError(f'Unexpected block end: {source}:{reader.line_num}')
                    current['end_line'] = reader.line_num
                    records.append(current)
                    current = None
                elif current is not None:
                    # Preserve order, repeated fields, duplicate values and empty columns.
                    current['rows'].append(row)
        if current:
            raise ValueError(f'Unterminated block: {current["key"]}')
    if not sources:
        raise ValueError(f'No CSV tables in {root}')
    return records, sources


def values(record, field):
    return [v for row in record['rows'] if row[0] == field for v in row[1:] if v]


def scalar(record, field):
    cells = values(record, field)
    return cells[0] if cells else None


def source_scope(source):
    parts = Path(source).parts
    return {'mode_hint': 'kingdom' if 'kingdom' in source.lower() else 'unspecified',
            'dlc_hint': next((p for p in parts if p.startswith('dlc_')), None),
            'editor_hint': 'editor' in source.lower()}


def columns(record, field):
    """Positional arrays, including empty cells, for parallel native loot columns."""
    rows = [row[1:] for row in record['rows'] if row[0] == field]
    return rows[0] if rows else []


def loot_routes(records):
    direct, parents = defaultdict(list), defaultdict(list)
    for record in records:
        if record['kind'] != 'LootTable':
            continue
        arrays = {f: columns(record, f) for f in
                  ('m_ids', 'm_types', 'm_chances', 'm_qtys', 'm_conditions', 'm_tags')}
        for i, target in enumerate(arrays['m_ids']):
            if not target:
                continue
            entry = {'record': record['key'], 'index': i,
                     **{f: a[i] if i < len(a) else None for f, a in arrays.items()}}
            kind = entry['m_types'] or ''
            if kind == 'item':
                direct[target].append(entry)
            elif 'sub_table' in kind:
                parents[target].append(entry)
    return direct, parents


# ItemDefinition constructor bindings. None means optional matching-ID lookup.
ROOT_BINDINGS = {
    'm_buyCostId': ('Cost', False), 'm_sellCostId': ('Cost', False),
    'm_conditionIds': ('Condition', True), 'm_effectIds': ('Effect', True),
    'm_applyLimitEffectIds': ('Effect', True),
    'm_combinationEffectIds': ('Effect', False),
    'm_combinationApplyLimitEffectIds': ('Effect', False),
    'm_partyEffectIds': ('Effect', False), 'm_partyApplyLimitEffectIds': ('Effect', False),
    'm_actorDataSkillId': ('ActorDataSkill', True),
    'm_actorDataSkillEffectsId': ('ActorDataEffects', True),
    'm_dataExternalBuffsId': ('ActorDataExternalBuffs', True),
    'm_runDataStatsId': ('RunDataStats', True),
    'm_DataNodeReplacementsId': ('DataNodeReplacements', True),
    'm_TorchLevelGroupId': ('TorchLevelGroup', True),
    'm_InnUpgradeIds': ('InnUpgrade', False),
}
RELATED_KINDS = {
    'Effect', 'Buff', 'Condition', 'Token', 'Quirk', 'Dot', 'Resist', 'Cost',
    'ActorDataSkill', 'ActorDataEffects', 'ActorDataStats', 'ActorDataExternalBuffs',
    'RunDataStats', 'StoryDataEffects', 'DataNodeReplacements', 'TorchLevelGroup',
    'SkillReplacement', 'ItemBlock', 'SkillBlock', 'InnUpgrade', 'Unlock',
}


def catalogue(records, sources):
    by_kind_id, by_id = defaultdict(list), defaultdict(list)
    for record in records:
        by_kind_id[record['kind'], record['id']].append(record['key'])
        by_id[record['id']].append(record)
    items = [r for r in records if r['kind'] == 'Item']
    if not items:
        raise ValueError('No Item records found')
    item_ids = {r['id'] for r in items}
    mentions = defaultdict(list)
    for record in records:
        for n, row in enumerate(record['rows']):
            for item_id in sorted(set(row[1:]) & item_ids):
                mentions[item_id].append({'record': record['key'], 'row_index': n,
                                          'field': row[0]})
    by_key = {r['key']: r for r in records}
    direct_loot, parent_loot = loot_routes(records)
    result, roots, unresolved = [], set(), []
    for item in items:
        bindings = []
        skill_id = scalar(item, 'm_actorDataSkillId') or item['id']
        has_skill = bool(by_kind_id['ActorDataSkill', skill_id])
        for field, (kind, fallback) in ROOT_BINDINGS.items():
            explicit = values(item, field)
            ids = explicit or ([item['id']] if fallback else [])
            for ref_id in ids:
                found = by_kind_id[kind, ref_id]
                suppressed = has_skill and field in ('m_actorDataSkillEffectsId', 'm_dataExternalBuffsId')
                binding = {'field': field, 'kind': kind, 'id': ref_id, 'records': found,
                           'lookup': 'explicit' if explicit else 'matching_id',
                           'active': not suppressed}
                # Optional same-ID misses are expected, not errors.
                if found or explicit:
                    bindings.append(binding)
                    roots.update(found)
                if explicit and not found and not suppressed:
                    unresolved.append({'item': item['key'], **binding})
        # Resource/unlock loading has separate rules. Show candidates without claiming activation.
        related = [r['key'] for r in by_id[item['id']]
                   if r['kind'] in RELATED_KINDS]
        unlock_id = scalar(item, 'm_UnlockId')
        if unlock_id:
            related.extend(by_kind_id['Unlock', unlock_id])
        refs = mentions[item['id']]
        acquisition = [m for m in refs if any(word in (
            by_key[m['record']]['kind'] + ' ' + by_key[m['record']]['source']).lower()
            for word in ('loot', 'shop', 'store', 'stock', 'trader'))]
        loot_entries = direct_loot[item['id']]
        loot_ancestors, seen = [], set()
        queue = deque(by_key[e['record']]['id'] for e in loot_entries)
        while queue:
            table_id = queue.popleft()
            if table_id in seen:
                continue
            seen.add(table_id)
            for entry in parent_loot[table_id]:
                loot_ancestors.append(entry)
                queue.append(by_key[entry['record']]['id'])
        roots.update(e['record'] for e in loot_entries + loot_ancestors)
        roots.update(related)
        roots.update(m['record'] for m in refs)
        roots.add(item['key'])
        result.append({
            'key': item['key'], 'id': item['id'], 'type': scalar(item, 'm_type'),
            'scope': source_scope(item['source']),
            'native': {row[0]: [r[1:] for r in item['rows'] if r[0] == row[0]]
                       for row in item['rows']},
            'bindings': bindings, 'same_id_candidates': list(dict.fromkeys(related)),
            'exact_id_mentions': refs, 'acquisition_candidates': acquisition,
            'loot_entries': loot_entries, 'loot_ancestors': loot_ancestors,
            'review': 'table-mapped; individual runtime behaviour and obtainability unverified',
            'proposed_curio_use': None, 'proposed_shop_stock': None,
        })
    # Resolve exact ID cells into related definitions for drill-down, with explicit uncertainty.
    # This is a navigation aid, NOT an interpreter of filters, load order or effect execution.
    queue, visited, links, skill_bindings = deque(sorted(roots)), set(), {}, {}
    while queue:
        key = queue.popleft()
        if key in visited:
            continue
        visited.add(key)
        record = by_key[key]
        if record['kind'] == 'ActorDataSkill':
            skill_bindings[key] = []
            for field, kind in (('m_ActorDataEffectsId', 'ActorDataEffects'),
                                ('m_ActorDataStatsId', 'ActorDataStats')):
                ref_id = scalar(record, field) or record['id']
                targets = by_kind_id[kind, ref_id]
                if targets or scalar(record, field):
                    skill_bindings[key].append({'field': field, 'kind': kind,
                                                'id': ref_id, 'records': targets})
                    queue.extend(targets)
        candidates = []
        for n, row in enumerate(record['rows']):
            for c, value in enumerate(row[1:], 1):
                if not value or value.lower() in ('true', 'false'):
                    continue
                try:
                    float(value)
                    continue
                except ValueError:
                    pass
                targets = [r['key'] for r in by_id[value]
                           if r['kind'] in RELATED_KINDS and r['key'] != key]
                if targets:
                    candidates.append({'row_index': n, 'column': c, 'value': value,
                                       'candidate_records': targets})
                    queue.extend(t for t in targets if t not in visited)
        if candidates:
            links[key] = candidates
    duplicates = {k: [r['key'] for r in v if r['kind'] == 'Item'] for k, v in by_id.items()
                  if sum(r['kind'] == 'Item' for r in v) > 1}
    return {
        'schema_version': 1,
        'scope': 'Installed Item records including DLC, mode overrides and editor entries; not a live inventory.',
        'limitations': [
            'No runtime load precedence, DLC ownership, unlock state, modifiers or shop stock evaluated.',
            'Missing native fields remain unspecified; zero/false defaults are not invented.',
            'Same-ID and exact-cell candidates need caller review; they are not confirmed execution edges.',
            'Costs are base definitions in DD2 currency, not a DD1 gold conversion or guaranteed sale.',
            'Loot ancestry retains variants, weights and conditions; it does not prove an enabled shop path.',
            'Scene-bound store roots and tag-filtered stock need additional runtime/addressable review.',
            'Effect groups and duplicate values are preserved; random weighting is not simulated.',
            'No localized display names, scene props or addressable art are extracted.',
        ],
        'summary': {'item_records': len(items), 'unique_item_ids': len(item_ids),
                    'by_type_records': dict(sorted(Counter(i['type'] for i in result).items())),
                    'by_type_unique_ids': {t: len({i['id'] for i in result if i['type'] == t})
                                           for t in sorted({i['type'] for i in result})},
                    'duplicate_item_ids': len(duplicates), 'csv_files': len(sources),
                    'evidence_records': len(visited), 'unresolved_explicit_bindings': len(unresolved)},
        'sources': sources, 'duplicate_variants': duplicates, 'unresolved_bindings': unresolved,
        'items': result, 'records': {k: by_key[k] for k in sorted(visited)},
        'candidate_links': links, 'skill_bindings': skill_bindings,
    }


CSV_FIELDS = ['m_maxQty', 'm_combinable', 'm_isConsumable', 'm_usedInInn', 'm_usedInCombat',
              'm_usedInDriving', 'm_numberOfTargets', 'm_isRandomTarget', 'm_useLimit',
              'm_useTagLimit', 'm_possessionLimit', 'm_canDiscard', 'm_slot', 'm_tags',
              'm_buyCostId', 'm_sellCostId', 'm_isBuyHidden', 'm_DurationType',
              'm_DurationAmount', 'm_QuestResourceId', 'm_QuestStepId']


def private_output(output, repo):
    output, repo = output.resolve(), repo.resolve()
    if output == repo or output.is_relative_to(repo):
        raise ValueError('Native catalogue output must stay outside the mod repository')
    if any((p / '.git').exists() for p in (output, *output.parents)):
        raise ValueError('Native catalogue output must stay outside all Git repositories')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Use a new or empty private output directory; existing evidence is preserved')
    return output


def write_catalogue(data, output):
    output.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(data, ensure_ascii=False, separators=(',', ':'))
    (output / 'catalogue.json').write_text(payload, encoding='utf-8')
    (output / 'summary.json').write_text(json.dumps(data['summary'], indent=2), encoding='utf-8')
    with (output / 'items.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['id', 'type', 'source', 'line', *CSV_FIELDS, 'review'])
        for item in data['items']:
            record = data['records'][item['key']]
            writer.writerow([item['id'], item['type'], record['source'], record['line'],
                             *[' | '.join(values(record, f)) for f in CSV_FIELDS], item['review']])
    template = Path(__file__).with_name('dd2_item_catalogue.html').read_text(encoding='utf-8')
    # Prevent a native string from terminating the inert JSON script block.
    (output / 'index.html').write_text(template.replace('/*CATALOGUE_JSON*/',
                                       payload.replace('<', '\\u003c')), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--excel', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    try:
        output = private_output(args.output, Path(__file__).resolve().parents[1])
        if not args.excel.is_dir():
            raise ValueError('Excel must be an installed table directory')
        if output == args.excel.resolve() or output.is_relative_to(args.excel.resolve()):
            raise ValueError('Output cannot be inside the installed tables')
        records, sources = read_tables(args.excel)
        data = catalogue(records, sources)
        data['created_utc'] = datetime.now(timezone.utc).isoformat()
        data['excel_root'] = str(args.excel.resolve())
        write_catalogue(data, output)
    except (OSError, ValueError, csv.Error) as error:
        parser.exit(1, f'{error}\n')
    print(json.dumps(data['summary'], indent=2))
    print(f'Private catalogue: {output / "index.html"}')


if __name__ == '__main__':
    main()
