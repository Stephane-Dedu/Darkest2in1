"""The QA ledger must never turn absent or stale evidence into acceptance."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('qa_run', ROOT / 'tools/qa/run.py')
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)


class QaRunnerTest(unittest.TestCase):
    def setUp(self):
        self.manifest = json.loads((ROOT / 'tools/qa/steps.json').read_text())
        self.state = qa.empty_state()

    def run_record(self, suite='full', status='pass', source='current'):
        self.state['runs'].append({'suite': suite, 'status': status, 'source_id': source})

    def test_previous_source_pass_cannot_unlock_manual_acceptance(self):
        self.run_record(source='old')
        self.assertIn('Run offline', qa.next_action(self.state, self.manifest, 'current'))

    def test_failed_rerun_overrides_earlier_success(self):
        self.run_record()
        self.run_record(status='fail')
        self.assertFalse(qa.covered(self.state, 'smoke', 'current'))

    def test_targeted_failure_overrides_full_pass_for_that_suite(self):
        self.run_record()
        self.run_record('travel', 'fail')
        self.assertFalse(qa.covered(self.state, 'travel', 'current'))
        self.assertTrue(qa.covered(self.state, 'town', 'current'))

    def test_automated_pass_does_not_mark_any_manual_step_done(self):
        self.run_record()
        self.state['builds'].append({'id': 'build-a'})
        self.assertIn('Step 01 [pending]', qa.next_action(self.state, self.manifest, 'current'))
        self.assertEqual([], self.state['manual'])

    def test_failed_later_step_is_revisited_before_untested_steps(self):
        self.run_record()
        self.state['builds'].append({'id': 'build-a'})
        self.state['manual'].append({'step': '05', 'outcome': 'fail', 'build': 'build-a'})
        self.assertIn('Step 05 [fail]', qa.next_action(self.state, self.manifest, 'current'))

    def test_new_installed_build_keeps_history_but_does_not_inherit_manual_passes(self):
        self.run_record()
        self.state['builds'] = [{'id': 'old'}, {'id': 'new'}]
        self.state['manual'] = [{'step': s['id'], 'outcome': 'pass', 'build': 'old'} for s in self.manifest['steps']]
        self.assertIn('Step 01 [pending]', qa.next_action(self.state, self.manifest, 'current'))
        self.assertEqual(10, len(self.state['manual']))

    def test_python_zero_skipped_or_failed_tests_are_not_a_pass(self):
        for text in ('Ran 0 tests in 0.00s\nOK\n', 'Ran 2 tests in 0.01s\nOK (skipped=1)\n',
                     'Ran 2 tests in 0.01s\nFAILED (failures=1)\n', 'missing dependencies'):
            self.assertFalse(qa.parse_unittest(text)['ok'])
        self.assertTrue(qa.parse_unittest('Ran 2 tests in 0.01s\nOK\n')['ok'])

    def test_trx_requires_nonzero_complete_results_and_no_skips(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'result.trx'
            for total, passed, executed, outcome, expected in [(0,0,0,None,False), (1,0,0,'NotExecuted',False),
                                                               (1,0,1,'Failed',False), (1,1,1,None,False), (1,1,1,'Passed',True)]:
                result = f'<UnitTestResult testName="example" outcome="{outcome}"/>' if outcome else ''
                path.write_text(f'<TestRun xmlns="urn:trx"><Results>{result}</Results><ResultSummary><Counters total="{total}" executed="{executed}" passed="{passed}"/></ResultSummary></TestRun>')
                self.assertEqual(expected, qa.parse_trx(path)['ok'])

    def test_every_step_has_a_real_test_suite_and_runbook_anchor(self):
        text = (ROOT / 'PLAYTEST_RUNBOOK.md').read_text(encoding='utf-8')
        self.assertEqual(len(self.manifest['steps']), len({s['id'] for s in self.manifest['steps']}))
        for step in self.manifest['steps']:
            self.assertIn(step['suite'], self.manifest['suites'])
            self.assertIn('id="' + step['anchor'] + '"', text)
        for suite in self.manifest['suites'].values():
            for kind in ('core', 'ui'):
                for name in suite.get(kind, []):
                    if name != '*':
                        project = 'Core' if kind == 'core' else 'Ui'
                        self.assertTrue((ROOT / f'tests/DarkestDungeon3.{project}.Tests/{name}.cs').exists(), name)


if __name__ == '__main__':
    unittest.main()
