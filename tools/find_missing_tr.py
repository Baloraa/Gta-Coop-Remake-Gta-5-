import re, glob, os

keys = set()
for f in glob.glob('GTACoopRemakeV2/src/GTACoop.Launcher/**/Localization*.cs', recursive=True):
    s = open(f, encoding='utf-8-sig').read()
    for m in re.finditer(r'\["((?:[^"\\]|\\.)*)"\]\s*=', s):
        k = m.group(1).replace('\\r', '\r').replace('\\n', '\n').replace('\\"', '"')
        keys.add(k)

used = {}
pattern = re.compile(r'(?:Loc\.)?\bT\("((?:[^"\\]|\\.)*)"')
for f in glob.glob('GTACoopRemakeV2/src/GTACoop.Launcher*/**/*.cs', recursive=True):
    if 'Localization' in f:
        continue
    s = open(f, encoding='utf-8-sig').read()
    for m in pattern.finditer(s):
        k = m.group(1).replace('\\r', '\r').replace('\\n', '\n').replace('\\"', '"')
        used.setdefault(k, set()).add(os.path.basename(f))

miss = {k: v for k, v in used.items() if k not in keys}
print('USED', len(used), 'MISSING', len(miss))
for k, v in sorted(miss.items()):
    print(repr(k), '<-', ','.join(sorted(v)))
