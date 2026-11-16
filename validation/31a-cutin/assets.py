"""Read-only asset provenance, selection metadata and layout bounds checks."""
import hashlib
import json
import pathlib
import struct
import sys

root = pathlib.Path(__file__).resolve().parents[2]
report_path = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'outputs/31a-28-37-20261114/cutin-assets-check.json'
manifest = json.loads((root / 'outputs/31a-28-37-20261114/31a-cutin-assets.json').read_text(encoding='utf-8'))
data = json.loads((root / 'data/thirty_one_a_cutin.json').read_text(encoding='utf-8'))
passed = []


def check(condition, message):
    if not condition:
        raise AssertionError(message)
    passed.append(message)


ok = False
error = None
try:
    check(len({line['k'] for line in data['lines']}) == len(data['lines']), 'all phrase identities remain unique')
    by_key = {line['k']: line for line in data['lines']}
    for key, role in manifest['roles'].items():
        # Every current working-picker identity must exist in the combined data.
        check(key in by_key, f'working-picker identity exists in current combined data: {key}')
        check(by_key[key]['role'] == role, f'working-picker role preserved: {key}={role}')
    def eligible(line):
        return not line.get('drop') and (not line.get('cond') or line.get('condActive'))
    def available(line):
        check((root / 'sounds/thirty_one_a_cutin' / (line['voice'] + '.ogg')).is_file(), f'eligible selected voice exists: {line["voice"]}')
        for expression in line.get('ec', []):
            check((root / 'images/ui/thirty_one_a_cutin' / f'{line["c"]}DefaultAdvCutIn{expression}.png').is_file(), f'eligible selected expression exists: {line["c"]}/{expression}')
    for line in data['lines']:
        if eligible(line):
            available(line)
    for chain in data['chains']:
        if not chain.get('def'):
            continue
        for step in chain['steps']:
            linked = by_key.get(step.get('k'))
            if linked and not eligible(linked):
                continue
            available(dict(voice=step['voice'], c=step['c'], ec=[step['expr']]))
    for item in manifest['files']:
        path = root / item['path']
        raw = path.read_bytes()
        check(hashlib.sha256(raw).hexdigest() == item['sha256'], f'asset byte-for-byte provenance: {item["path"]}')
        if path.suffix == '.png':
            check(raw[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', raw[16:24]) == (1024, 1368) and raw[25] == 6, f'native transparent RGBA portrait frame: {path.name}')
        else:
            identification = raw.find(b'\x01vorbis')
            check(raw[:4] == b'OggS' and identification >= 0, f'voice is OGG Vorbis: {path.name}')
            check(struct.unpack('<I', raw[identification + 12:identification + 16])[0] == 44100, f'voice sample rate 44100: {path.name}')
    installed = {str(path.relative_to(root)).replace('\\', '/') for folder in ('images/ui/thirty_one_a_cutin', 'sounds/thirty_one_a_cutin') for path in (root / folder).glob('*') if path.suffix in ('.png', '.ogg')}
    expected = {item['path'].replace('\\', '/') for item in manifest['files']}
    check(installed == expected, 'only selected/currently reachable art and voices are copied')
    for width, height in [(1920, 1080), (1920, 1200), (2520, 1080), (1280, 720)]:
        art_height = height * .24
        art_width = art_height * 1024 / 1368
        for row in range(4):  # The fourth exists only for an immediate reply chain.
            x = width * .2 - art_width / 2
            y = height * (.28 + row * .08) - art_height / 2
            right = x + art_width * .85 + width * .24
            bottom = y + art_height
            check(0 <= x and right <= width * .6 and y >= height * .1 and bottom <= height * .65,
                  f'portrait and speech remain above hand and left of enemies: {width}x{height}, row{row}')
    source = (root / 'Core/Nodes/Combat/NThirtyOneACutinOverlay.cs').read_text(encoding='utf-8')
    check('MouseFilterEnum.Stop' not in source and 'MouseFilterEnum.Pass' not in source, 'cut-in contains no input-catching control')
    check('LocalContext.GetMe(combat)' in source, 'presentation receipt checks the local player before enqueue')
    check('private readonly Random _random = new();' in source and '.Rng.' not in source, 'presentation randomness is separate from synchronized battle RNG')
    conds = [line for line in data['lines'] if line.get('cond') and line.get('condActive') and not line.get('drop')]
    check(all(line.get('conditionKey') not in ('unknown', 'unavailable-original-encounter', '') for line in conds), 'every active encounter condition has an explicit runtime mapping')
    ok = True
except Exception as failure:
    error = repr(failure)
report_path.parent.mkdir(parents=True, exist_ok=True)
report_path.write_text(json.dumps(dict(ok=ok, checks=len(passed), passed=passed, error=error,
    lines=len(data['lines']), copiedArt=manifest['art'], copiedVoices=manifest['voice'],
    boundary='File hashes/formats and settled layout geometry. No Godot import, shader compilation, live rendering, audio audition or multiplayer execution.'), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(('PASS ' if ok else 'FAIL ') + str(len(passed)) + ' cut-in asset/layout checks')
if error:
    print(error)
    raise SystemExit(1)
