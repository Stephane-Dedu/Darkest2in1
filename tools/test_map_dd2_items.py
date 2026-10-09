"""Synthetic fixtures only; no game files or saves required."""
import csv
import json
import tempfile
import unittest
from pathlib import Path

from map_dd2_items import catalogue, private_output, read_tables, write_catalogue


def block(item_id, kind, *rows):
    return [['element_start', item_id, kind], *rows, ['element_end']]


class ItemMapTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.excel = self.root / 'excel'
        self.excel.mkdir()

    def write(self, name, rows):
        path = self.excel / name
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open('w', encoding='utf-8-sig', newline='') as stream:
            csv.writer(stream).writerows(rows)
        return path

    def data(self):
        return catalogue(*read_tables(self.excel))

    def test_variants_do_not_overwrite_each_other(self):
        self.write('main.csv', block('tool', 'Item', ['m_type', 'rest'], ['m_useLimit', '2']))
        self.write('kingdom/override.csv', block('tool', 'Item', ['m_type', 'rest'], ['m_useLimit', '1']))
        data = self.data()
        self.assertEqual(2, data['summary']['item_records'])
        self.assertEqual(1, data['summary']['unique_item_ids'])
        self.assertEqual(2, len(data['duplicate_variants']['tool']))
        self.assertEqual({'1', '2'}, {i['native']['m_useLimit'][0][0] for i in data['items']})

    def test_raw_rows_keep_empty_columns_repetitions_and_unicode(self):
        rows = [['m_chances', '2', '', '0', ''], ['note', 'a,b\nété'],
                ['note', 'second'], ['m_effectIds', 'good', 'good', 'bad', '']]
        path = self.write('main.csv', block('tool', 'Item', ['m_type', 'rest'], *rows))
        records, sources = read_tables(self.excel)
        self.assertEqual(rows, records[0]['rows'][1:])
        self.assertEqual(len(path.read_text(encoding='utf-8-sig').splitlines()), records[0]['end_line'])
        self.assertEqual(64, len(sources[0]['sha256']))

    def test_matching_id_and_explicit_effect_lists_preserve_multiplicity(self):
        self.write('main.csv', block('tool', 'Item', ['m_type', 'rest'],
                   ['m_combinationApplyLimitEffectIds', 'good', 'good', 'bad'])
                   + block('tool', 'Effect', ['m_Chance', '1'])
                   + block('good', 'Effect', ['m_Chance', '1'])
                   + block('bad', 'Effect', ['m_Chance', '1']))
        item = self.data()['items'][0]
        self.assertEqual(['good', 'good', 'bad'], [b['id'] for b in item['bindings']
                         if b['field'] == 'm_combinationApplyLimitEffectIds'])
        self.assertTrue(any(b['field'] == 'm_effectIds' and b['lookup'] == 'matching_id'
                            for b in item['bindings']))
        self.assertFalse(any(b['field'] == 'm_partyEffectIds' for b in item['bindings']))

    def test_skill_precedence_and_explicit_different_skill_payload(self):
        self.write('main.csv', block('tool', 'Item', ['m_type', 'combat'],
                   ['m_actorDataSkillId', 'action'], ['m_actorDataSkillEffectsId', 'missing_inactive'])
                   + block('action', 'ActorDataSkill', ['m_ActorDataEffectsId', 'payload'])
                   + block('payload', 'ActorDataEffects', ['target_effects', 'heal'])
                   + block('heal', 'Effect', ['m_Chance', '1']))
        data = self.data()
        self.assertEqual([], data['unresolved_bindings'])
        inactive = next(b for b in data['items'][0]['bindings'] if b['id'] == 'missing_inactive')
        self.assertFalse(inactive['active'])
        self.assertTrue(any(r['id'] == 'payload' for r in data['records'].values()))
        self.assertTrue(any(r['id'] == 'heal' for r in data['records'].values()))

    def test_unresolved_explicit_binding_is_visible_optional_miss_is_not_an_error(self):
        self.write('main.csv', block('tool', 'Item', ['m_type', 'rest'], ['m_buyCostId', 'missing']))
        data = self.data()
        self.assertEqual(['missing'], [r['id'] for r in data['unresolved_bindings']])
        self.assertNotIn('m_usedInInn', data['items'][0]['native'])

    def test_loot_zero_weights_conditions_alignment_cycles_and_variants(self):
        self.write('main.csv', block('tool', 'Item', ['m_type', 'combat'])
                   + block('pool', 'LootTable', ['m_ids', 'other', 'tool'],
                           ['m_types', 'item', 'item'], ['m_chances', '1', '0'],
                           ['m_conditions', '', 'needs_unlock'])
                   + block('shop', 'LootTable', ['m_ids', 'pool'], ['m_types', 'sub_table'])
                   + block('pool', 'LootTable', ['m_ids', 'shop'], ['m_types', 'all_sub_table']))
        item = self.data()['items'][0]
        self.assertEqual('0', item['loot_entries'][0]['m_chances'])
        self.assertEqual('needs_unlock', item['loot_entries'][0]['m_conditions'])
        self.assertEqual(2, len(item['loot_ancestors']))

    def test_nested_or_unclosed_blocks_fail_without_partial_catalogue(self):
        for rows in ([['element_start', 'x', 'Item']],
                     [['element_start', 'x', 'Item'], ['element_start', 'y', 'Item']]):
            self.write('main.csv', rows)
            with self.assertRaises(ValueError):
                read_tables(self.excel)

    def test_empty_scan_and_no_items_are_errors(self):
        with self.assertRaises(ValueError):
            read_tables(self.excel)
        self.write('main.csv', block('other', 'Effect'))
        with self.assertRaises(ValueError):
            self.data()

    def test_private_output_rejects_repos_and_existing_evidence(self):
        repo = self.root / 'repo'
        repo.mkdir()
        (repo / '.git').write_text('gitdir: elsewhere')
        for output in (repo / 'private', repo / '..' / 'repo' / 'private'):
            with self.assertRaises(ValueError):
                private_output(output, repo)
        with self.assertRaises(ValueError):
            private_output(repo / 'private', self.root / 'different-repo')
        self.write('existing.csv', block('owned_evidence', 'Item'))
        with self.assertRaises(ValueError):
            private_output(self.excel, repo)
        output = self.root / 'new-private'
        self.assertEqual(output, private_output(output, repo))

    def test_outputs_round_trip_and_html_cannot_be_closed_by_native_text(self):
        self.write('main.csv', block('tool', 'Item', ['m_type', 'rest'],
                                    ['note', '</script><script>alert(1)</script>']))
        data = self.data()
        output = self.root / 'private'
        write_catalogue(data, output)
        self.assertEqual(data, json.loads((output / 'catalogue.json').read_text(encoding='utf-8')))
        html = (output / 'index.html').read_text(encoding='utf-8')
        self.assertNotIn('</script><script>alert(1)', html)
        self.assertNotIn('/*CATALOGUE_JSON*/', html)
        with (output / 'items.csv').open(encoding='utf-8-sig', newline='') as stream:
            self.assertEqual('tool', list(csv.DictReader(stream))[0]['id'])


if __name__ == '__main__':
    unittest.main()
