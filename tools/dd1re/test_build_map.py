"""Exercise the map builder and queries with synthetic input, without Ghidra or game files."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


TOOLS = Path(__file__).resolve().parent


class BuildMapTests(unittest.TestCase):
    def test_loaded_rule_keeps_source_references_and_query_indexes(self):
        with tempfile.TemporaryDirectory(prefix="dd1re-test-") as temp:
            root = Path(temp)
            (root / "raw").mkdir()
            (root / "decomp").mkdir()
            (root / "data/shared").mkdir(parents=True)
            (root / "data/shared/rules.json").write_text(
                json.dumps({"synthetic_rule": 0.25}), encoding="utf-8")
            functions = [
                {"entry": "1000", "name": "SyntheticLoader", "size": 20,
                 "signature": "void SyntheticLoader(void)",
                 "callers": [], "callees": [],
                 "strings": ["synthetic_rule", "source_code\\game\\synthetic.cpp"]},
                {"entry": "2000", "name": "SyntheticReader", "size": 20,
                 "signature": "float SyntheticReader(void)",
                 "callers": [], "callees": [], "strings": []},
            ]
            (root / "raw/functions.jsonl").write_text(
                "".join(json.dumps(fn) + "\n" for fn in functions), encoding="utf-8")
            for name in ("strings", "vtables"):
                (root / f"raw/{name}.jsonl").write_text("", encoding="utf-8")
            (root / "decomp/SyntheticLoader@1000.c").write_text(
                'if (memcmp(name, "synthetic_rule", 14) == 0) {\n'
                '  DAT_12345678 = value;\n}\n', encoding="utf-8")
            (root / "decomp/SyntheticReader@2000.c").write_text(
                "return DAT_12345678;\n", encoding="utf-8")

            env = dict(os.environ, DD1RE_DIR=str(root), PYTHONUTF8="1")
            build = subprocess.run(
                [sys.executable, str(TOOLS / "build_map.py"), "--root", str(root),
                 "--dd1", str(root / "data")],
                capture_output=True, text=True, encoding="utf-8", env=env)
            self.assertEqual(build.returncode, 0, build.stderr)
            manifest = json.loads(build.stdout)
            self.assertEqual(manifest["source_files"], 1)
            self.assertEqual(manifest["data_keys_loaded_into_globals"], 1)
            self.assertIn("game/synthetic.cpp", (root / "map/source_files.md").read_text())

            for command, argument, expected in (
                ("key", "synthetic_rule", "SyntheticReader@2000"),
                ("global", "DAT_12345678", "SyntheticReader@2000"),
                ("fn", "SyntheticLoader", "source files: game/synthetic.cpp"),
            ):
                with self.subTest(command=command):
                    query = subprocess.run(
                        [sys.executable, str(TOOLS / "dd1q.py"), command, argument],
                        capture_output=True, text=True, encoding="utf-8", env=env)
                    self.assertEqual(query.returncode, 0, query.stderr)
                    self.assertIn(expected, query.stdout)


if __name__ == "__main__":
    unittest.main()
