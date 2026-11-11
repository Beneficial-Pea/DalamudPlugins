#!/usr/bin/env python3
"""Fix a whitespace-only anchor mismatch in the Oracle v1.3.4 patch kit.

This is applied AFTER extracting LogMimic_SourceKit.zip and BEFORE running
prepare_standalone.py. It does not change functionality or upstream licenses.
"""
from pathlib import Path

p = Path("patch/patch_oracle.py")
if not p.is_file():
    raise SystemExit("Missing patch/patch_oracle.py (extract source ZIP first).")
text = p.read_text(encoding="utf-8")
bad = "'                 allowed,\\n                 code));'"
good = "'                allowed,\\n                code));'"
if text.count(bad) == 1:
    p.write_text(text.replace(bad, good), encoding="utf-8")
    print("Corrected FFLogsImportPanel source anchor spacing.")
elif text.count(good) == 1:
    print("FFLogsImportPanel source anchor already corrected.")
else:
    raise SystemExit("Unexpected patch script version; refusing a blind edit.")
