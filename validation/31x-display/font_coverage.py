"""Offline SFNT cmap coverage audit; no font engine or external package needed."""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read_font(path):
    data = path.read_bytes()
    u16 = lambda offset: struct.unpack_from(">H", data, offset)[0]
    u32 = lambda offset: struct.unpack_from(">I", data, offset)[0]
    tables = {}
    for index in range(u16(4)):
        tag, _, offset, size = struct.unpack_from(">4sIII", data, 12 + index * 16)
        tables[tag.decode("ascii")] = (offset, size)
    start, _ = tables["cmap"]
    codepoints = set()
    formats = set()
    for index in range(u16(start + 2)):
        platform, encoding, relative = struct.unpack_from(">HHI", data, start + 4 + index * 8)
        if platform != 0 and not (platform == 3 and encoding in (1, 10)):
            continue
        sub = start + relative
        kind = u16(sub)
        formats.add(kind)
        if kind == 12:
            for group in range(u32(sub + 12)):
                first, last, glyph = struct.unpack_from(">III", data, sub + 16 + group * 12)
                codepoints.update(range(first + (1 if glyph == 0 else 0), last + 1))
        elif kind == 4:
            count = u16(sub + 6) // 2
            end = sub + 14
            begin = end + count * 2 + 2
            delta = begin + count * 2
            ranges = delta + count * 2
            for segment in range(count):
                first, last = u16(begin + segment * 2), u16(end + segment * 2)
                offset, shift = u16(ranges + segment * 2), u16(delta + segment * 2)
                for point in range(first, last + 1):
                    if offset:
                        glyph = u16(ranges + segment * 2 + offset + (point - first) * 2)
                        glyph = (glyph + shift) & 0xffff if glyph else 0
                    else:
                        glyph = (point + shift) & 0xffff
                    if glyph:
                        codepoints.add(point)
    return codepoints, formats, tables, hashlib.sha256(data).hexdigest()


def main():
    words = json.loads((ROOT / "data/thirty_one_x_character_glossary.json").read_text(encoding="utf-8"))["entries"]
    provenance = json.loads((ROOT / "fonts/thirty_one_x_fallback.sources.json").read_text(encoding="utf-8"))
    rows = []
    for filename, language_indexes in (("noto_sans.ttf", (0, 1, 2)), ("noto_sans_devanagari.ttf", (3,))):
        path = ROOT / "fonts" / filename
        points, formats, tables, sha = read_font(path)
        wanted = {ord(char) for row in words.values() for language in language_indexes for char in row[language] if not char.isspace()}
        missing = sorted(wanted - points)
        source = next(row for row in provenance if row["file"] == "fonts/" + filename)
        assert sha == source["sha256"] and path.stat().st_size == source["bytes"]
        assert not missing, (filename, [(hex(point), chr(point)) for point in missing])
        assert "GSUB" in tables and "GPOS" in tables
        rows.append(dict(file="fonts/" + filename, sha256=sha, cmapFormats=sorted(formats), totalMapped=len(points), required=len(wanted), missing=[], hasGSUB=True, hasGPOS=True))
    for source in provenance:
        path = ROOT / source["file"]
        assert hashlib.sha256(path.read_bytes()).hexdigest() == source["sha256"]
    report = dict(ok=True, dictionaryEntries=len(words), fonts=rows, provenanceFiles=len(provenance), boundary="Static Unicode cmap and OpenType GSUB/GPOS presence only; runtime Godot fallback application, shaping and visual clipping require in-game rendering. No font rendering, engine invocation, downloads or external services.")
    target = ROOT / "outputs/31f-and-31x-83-88-20261115/31x-font-coverage.json"
    target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
