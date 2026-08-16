import tempfile
import unittest
from pathlib import Path

from validate_godot_incidents import validate_incident


VALID_INCIDENT = """---
id: sample-incident
status: verified
signature: \"sample\"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: csharp
last_verified: 2026-08-09
---

# Sample

## Observed
x
## Reproduction
x
## Cause and resolution
x
## Evidence
`verified_local`
## Scope and invalidation
x
"""


class ValidateGodotIncidentTests(unittest.TestCase):
    def test_valid_incident_passes(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = Path(temporary_directory) / "sample-incident.md"
            path.write_text(VALID_INCIDENT, encoding="utf-8")
            self.assertEqual(validate_incident(path), [])

    def test_verified_incident_requires_local_evidence(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = Path(temporary_directory) / "sample-incident.md"
            path.write_text(VALID_INCIDENT.replace("`verified_local`", "`official_docs`"), encoding="utf-8")
            self.assertTrue(any("verified_local" in error for error in validate_incident(path)))


if __name__ == "__main__":
    unittest.main()
