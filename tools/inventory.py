#!/usr/bin/env python3
"""Read archive indexes only; never extract or copy game content."""
# SPDX-License-Identifier: Apache-2.0
import argparse
import json
import struct
from pathlib import Path


def archive_index(path):
    with path.open("rb") as stream:
        header = stream.read(16)
        if len(header) < 16 or header[:4] != b"KCAP":
            raise ValueError(f"Not a KCAP archive: {path}")
        count = struct.unpack_from("<I", header, 12)[0]
        length = path.stat().st_size
        if 16 + count * 44 > length:
            raise ValueError(f"Truncated archive index: {path}")
        entries = []
        for _ in range(count):
            record = stream.read(44)
            offset, size = struct.unpack_from("<II", record, 36)
            if offset + size > length:
                raise ValueError(f"Archive entry exceeds file: {path}")
            entries.append(record[4:28].split(b"\0", 1)[0].decode("cp932"))
        return entries


def inventory(directory):
    result = {"directory": str(directory.resolve()), "archives": [], "movies": []}
    for path in sorted(directory.rglob("*")):
        if not path.is_file() or path.suffix.lower() != ".pak":
            continue
        with path.open("rb") as stream:
            magic = stream.read(4)
        if magic != b"KCAP":
            result["movies"].append(str(path.relative_to(directory)))
            continue
        names = archive_index(path)
        result["archives"].append({"path": str(path.relative_to(directory)),
            "entries": len(names), "scripts": sorted(n for n in names if n.endswith(".bnr")),
            "text_files": sum(n.endswith(".txt") for n in names)})
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directories", nargs="+", type=Path)
    args = parser.parse_args()
    print(json.dumps([inventory(p) for p in args.directories], ensure_ascii=False, indent=2))
