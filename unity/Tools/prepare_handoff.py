#!/usr/bin/env python3
"""Read-only Git provenance + explicit-freeze local team ZIP packaging.

This tool never commits, pushes, changes Git identity, publishes or deletes
project files. Run --check freely; --package requires a matching freeze file.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime, timezone
import fnmatch
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
import tempfile
import zipfile

UNITY_ROOT = Path(__file__).resolve().parents[1]
OWNERSHIP_PATH = UNITY_ROOT / 'docs' / 'TEAM_OWNERSHIP.json'
ARCHIVE_ROOT = 'LUMEN'

# Besides Git ignore rules, never redistribute paid source assets, deployment
# identity, credentials, personal state or compiled players in a source handoff.
COMMON_DENY = (
    '.git', '.git/**', '.env', '.env.*', '**/.env', '**/.env.*',
    '.openai', '.openai/**', '.sites-runtime', '.sites-runtime/**',
    'node_modules', 'node_modules/**', '**/node_modules/**',
    '*.unitypackage', '*.zip', '*.7z', '*.tar', '*.tar.gz',
    '*.pfx', '*.p12', '*.pem', '*.key', '*.keystore',
    '*credentials.json', '*service-account*.json', '*.sqlite', '*.sqlite-*', '*.db',
    '*.log', '*.pidb', '*.pdb', '*.userprefs', '*.csproj.user',
    '.vs/**', '.idea/**', '.DS_Store',
)
UNITY_DENY = (
    'Library/**', 'Temp/**', 'Obj/**', 'Logs/**', 'UserSettings/**', 'Builds/**', 'Artifacts/**',
    'Tests/**/bin/**', 'Tests/**/obj/**', 'Tools/**/bin/**', 'Tools/**/obj/**',
    'Assets/Plugins/Sirenix', 'Assets/Plugins/Sirenix/**', 'Assets/Plugins/Sirenix.meta',
    'Assets/AssetStoreTools', 'Assets/AssetStoreTools/**', 'Assets/AssetStoreTools.meta',
    'Assets/Synty*', 'Assets/Synty*/**', 'Assets/Polygon*', 'Assets/Polygon*/**',
    'Assets/Lumen/Resources/Generated/Synty*', 'Assets/Lumen/Resources/Generated/Synty*/**',
    'Assets/Resources/PerformanceTestRun*', 'Tools/GraphicsDraft/**', 'Tools/VRDraft/**',
    'ProjectSettings/EditorUserBuildSettings.asset', 'ProjectSettings/MemorySettings.asset',
)
WEB_DENY = ('dist/**', '.next/**', '.wrangler/**', 'work/**', 'public/unity/Build/**')


class HandoffError(RuntimeError):
    pass


@dataclass(frozen=True)
class SourceFile:
    path: str
    data: bytes
    mode: str = '100644'

    @property
    def sha256(self) -> str:
        return hashlib.sha256(self.data).hexdigest()


def safe_path(value: str) -> str:
    path = PurePosixPath(value)
    if not value or path.is_absolute() or any(p in ('..', '') for p in path.parts) or '\\' in value or ':' in value:
        raise HandoffError('Unsafe archive path: ' + repr(value))
    return path.as_posix()


def allowed(repo: str, relative: str) -> bool:
    safe_path(relative)
    patterns = COMMON_DENY + (UNITY_DENY if repo == 'unity' else WEB_DENY)
    return not any(fnmatch.fnmatchcase(relative.lower(), p.lower()) for p in patterns)


def json_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')


def git(root: Path, *args: str) -> bytes:
    result = subprocess.run(['git', '-C', str(root), *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise HandoffError('Git ' + args[0] + ' failed in ' + root.name + ': ' + result.stderr.decode('utf-8', 'replace')[:2000])
    return result.stdout


def commit_id(root: Path, ref: str) -> str:
    value = git(root, 'rev-parse', '--verify', ref + '^{commit}').decode().strip()
    if not re.fullmatch(r'[0-9a-f]{40,64}', value):
        raise HandoffError('Git did not return a commit ID.')
    ancestry = subprocess.run(['git', '-C', str(root), 'merge-base', '--is-ancestor', value, 'HEAD'], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if ancestry.returncode:
        raise HandoffError('The selected baseline is not an ancestor of HEAD in ' + root.name + '.')
    return value


def baseline_files(root: Path, repo: str, commit: str) -> dict[str, SourceFile]:
    objects = []
    for entry in git(root, 'ls-tree', '-r', '-z', '--full-tree', commit).split(b'\0'):
        if not entry:
            continue
        header, name = entry.split(b'\t', 1)
        mode, kind, oid = header.decode('ascii').split(' ')
        relative = name.decode('utf-8')
        if not allowed(repo, relative):
            continue
        if kind != 'blob' or mode not in ('100644', '100755'):
            raise HandoffError('A submodule or symbolic link needs manual review: ' + repo + '/' + relative)
        objects.append((relative, mode, oid))
    result = {}
    process = subprocess.Popen(['git', '-C', str(root), 'cat-file', '--batch'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    try:
        for relative, mode, oid in objects:
            process.stdin.write((oid + '\n').encode('ascii')); process.stdin.flush()
            answer = process.stdout.readline().decode('ascii').strip().split(' ')
            if len(answer) != 3 or answer[1] != 'blob':
                raise HandoffError('Cannot read baseline blob for ' + relative)
            size = int(answer[2]); data = process.stdout.read(size)
            if len(data) != size or process.stdout.read(1) != b'\n':
                raise HandoffError('Incomplete Git blob for ' + relative)
            path = safe_path(repo + '/' + relative)
            result[path] = SourceFile(path, data, mode)
        process.stdin.close()
        if process.wait(timeout=30):
            raise HandoffError('Git blob reader did not finish successfully.')
    finally:
        if process.poll() is None:
            process.kill(); process.wait()
    return result


def working_files(root: Path, repo: str) -> dict[str, SourceFile]:
    names = {n.decode('utf-8') for n in git(root, 'ls-files', '-c', '-o', '--exclude-standard', '-z').split(b'\0') if n}
    modes = {}
    for item in git(root, 'ls-files', '--stage', '-z').split(b'\0'):
        if not item:
            continue
        meta, name = item.split(b'\t', 1)
        mode, _, stage = meta.decode('ascii').split(' ')
        if stage != '0':
            raise HandoffError('Resolve Git merge conflicts before capturing a handoff.')
        modes[name.decode('utf-8')] = mode
    result = {}
    for relative in sorted(names):
        if not allowed(repo, relative):
            continue
        file = root / relative
        if not file.exists():
            continue  # A tracked deletion is represented by the baseline/current diff.
        if file.is_symlink() or not file.is_file() or not file.resolve().is_relative_to(root.resolve()):
            raise HandoffError('A non-regular source file needs manual review: ' + repo + '/' + relative)
        mode = modes.get(relative, '100755' if relative.endswith('.sh') else '100644')
        if mode not in ('100644', '100755'):
            raise HandoffError('Unsupported source mode: ' + repo + '/' + relative)
        before = file.stat(); data = file.read_bytes(); after = file.stat()
        if before.st_size != after.st_size or before.st_mtime_ns != after.st_mtime_ns or len(data) != after.st_size:
            raise HandoffError('A source file changed while being read. Wait for a stable state: ' + relative)
        path = safe_path(repo + '/' + relative)
        result[path] = SourceFile(path, data, mode)
    return result


def load_ownership() -> dict:
    config = json.loads(OWNERSHIP_PATH.read_text(encoding='utf-8'))
    roles = config['roles']
    if len(roles) != 4 or sum(r['responsibility_percent'] for r in roles) != 100 or any(r['responsibility_percent'] != 25 for r in roles):
        raise HandoffError('Expected four explicitly described 25% responsibility areas.')
    if {w['number'] for w in config['weeks']} != set(range(1, 11)):
        raise HandoffError('The delivery schedule must contain weeks 01 through 10.')
    ids = {r['id'] for r in roles}
    for rule in config['assignment_rules']:
        if rule['owner'] not in ids or not 1 <= rule['week'] <= 10:
            raise HandoffError('Invalid role/week assignment rule.')
    return config


def assignment(path: str, config: dict) -> tuple[str, int]:
    # Unity's sidecar belongs to its asset; it cannot drift into another person's ZIP.
    canonical = path[:-5] if path.endswith('.meta') else path
    for rule in config['assignment_rules']:
        if any(fnmatch.fnmatchcase(canonical, pattern) for pattern in rule['patterns']):
            return rule['owner'], rule['week']
    raise HandoffError('No owner assigned to ' + path)


def fingerprint(files: dict[str, SourceFile]) -> str:
    digest = hashlib.sha256()
    casefolded = set()
    for path, item in sorted(files.items()):
        folded = path.casefold()
        if folded in casefolded:
            raise HandoffError('Paths collide on Windows: ' + path)
        casefolded.add(folded)
        digest.update((path + '\0' + item.mode + '\0' + item.sha256 + '\0' + str(len(item.data)) + '\n').encode('utf-8'))
    return digest.hexdigest()


def index_files(files: dict[str, SourceFile], config: dict) -> list[dict]:
    return [dict(path=p, sha256=f.sha256, size_bytes=len(f.data), mode=f.mode,
                 owner=assignment(p, config)[0], week=assignment(p, config)[1]) for p, f in sorted(files.items())]


def changes_between(baseline: dict[str, SourceFile], current: dict[str, SourceFile], config: dict) -> list[dict]:
    result = []
    for path in sorted(baseline.keys() | current.keys()):
        before, after = baseline.get(path), current.get(path)
        if before and after and before.sha256 == after.sha256 and before.mode == after.mode:
            continue
        owner, week = assignment(path, config)
        result.append(dict(path=path, action='deleted' if after is None else 'added' if before is None else 'modified',
                           base_sha256=before.sha256 if before else None,
                           sha256=after.sha256 if after else None,
                           size_bytes=len(after.data) if after else 0, mode=after.mode if after else None,
                           owner=owner, week=week))
    return result


def portable_files(files: dict[str, SourceFile]) -> tuple[dict[str, SourceFile], list[dict]]:
    """Drop only optional vendor defines whose paid SDK is deliberately excluded.

    This changes copied bytes, never the live project or its Git objects. Both
    before/after hashes are disclosed so the portable snapshot is not confused
    with the original Git snapshot.
    """
    path = 'unity/ProjectSettings/ProjectSettings.asset'
    original = files.get(path)
    if original is None: return files, []
    lines = original.data.decode('utf-8').splitlines(keepends=True)
    define_indent = None; removed = {}; output = []
    for line in lines:
        stripped = line.strip()
        indent = len(line) - len(line.lstrip(' '))
        if stripped == 'scriptingDefineSymbols:':
            define_indent = indent
        elif define_indent is not None and stripped:
            if indent <= define_indent:
                define_indent = None
            else:
                match = re.fullmatch(r'(\s*([^:#\r\n]+):[ \t]*)([^\r\n]*)(\r?\n)?', line)
                if match:
                    prefix, platform, value, ending = match.groups()
                    quote = value[0] if len(value) >= 2 and value[0] in ('\"', "'") and value[-1] == value[0] else ''
                    tokens = (value[1:-1] if quote else value).split(';')
                    dropped = [token for token in tokens if re.fullmatch(r'ODIN_(?:INSPECTOR|VALIDATOR)(?:_[A-Z0-9]+)*', token.strip())]
                    if dropped:
                        kept = [token for token in tokens if token not in dropped]
                        replacement = ';'.join(kept)
                        if quote: replacement = quote + replacement + quote
                        elif not replacement.strip(): replacement = "''"
                        line = prefix + replacement + (ending or '')
                        removed[platform.strip()] = [token.strip() for token in dropped]
        output.append(line)
    data = ''.join(output).encode('utf-8')
    if not removed: return files, []
    transformed = SourceFile(path, data, original.mode)
    result = dict(files); result[path] = transformed
    return result, [dict(path=path, rule='remove-optional-odin-sdk-defines-v1',
                         reason='Paid Sirenix/Odin source is excluded; the optional editor dashboard must stay disabled until a licensed SDK is imported.',
                         removed_symbols=removed, original_sha256=original.sha256, portable_sha256=transformed.sha256)]


def verify_schedule(baseline: dict[str, SourceFile], current: dict[str, SourceFile], changes: list[dict]) -> list[dict]:
    state = dict(baseline); delivered = set(); milestones = []
    for week in range(1, 11):
        week_changes = [c for c in changes if c['week'] == week]
        for change in week_changes:
            path = change['path']
            if path in delivered:
                raise HandoffError('Source path appears in more than one weekly payload: ' + path)
            delivered.add(path)
            if change['action'] == 'deleted': state.pop(path, None)
            else: state[path] = current[path]
        milestones.append(dict(week=week, changed_paths=len(week_changes), source_sha256=fingerprint(state),
                               unity_build_verified=False,
                               note='File-level reconstruction verified; this is not a Unity build/test result.'))
    if fingerprint(state) != fingerprint(current) or len(delivered) != len(changes):
        raise HandoffError('Weekly source payloads do not reconstruct the exact frozen source.')
    return milestones


def capture(unity_root: Path, web_root: Path, config: dict, unity_base: str | None, web_base: str | None) -> tuple[dict, dict, dict]:
    baseline = {}; current = {}; repos = {}
    for repo, root, override in (('unity', unity_root, unity_base), ('web', web_root, web_base)):
        if not root.is_dir(): raise HandoffError('Source project folder not found: ' + str(root))
        resolved = commit_id(root, override or config['git_baselines'][repo])
        raw_baseline = baseline_files(root, repo, resolved); raw_current = working_files(root, repo)
        portable_baseline, baseline_transforms = portable_files(raw_baseline)
        files, current_transforms = portable_files(raw_current)
        baseline.update(portable_baseline); current.update(files)
        repos[repo] = dict(baseline_commit=resolved, head_commit=git(root, 'rev-parse', 'HEAD').decode().strip(),
                           working_tree_has_changes=bool(git(root, 'status', '--porcelain')), portable_source_files=len(files),
                           original_filtered_baseline_sha256=fingerprint(raw_baseline), original_filtered_source_sha256=fingerprint(raw_current),
                           portable_transformations=dict(baseline=baseline_transforms, current=current_transforms))
    if not baseline or not current: raise HandoffError('An empty source handoff is not supported.')
    return baseline, current, repos


def outside_sources(path: Path, roots: tuple[Path, Path]) -> Path:
    resolved = path.expanduser().resolve()
    if any(resolved.is_relative_to(root.resolve()) for root in roots):
        raise HandoffError('Freeze files and package output must be outside both source repositories.')
    return resolved


def write_zip(path: Path, files: dict[str, SourceFile], metadata: dict[str, bytes]) -> str:
    if path.exists(): raise HandoffError('Refusing to overwrite an existing archive: ' + str(path))
    with zipfile.ZipFile(path, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as archive:
        entries = {safe_path(ARCHIVE_ROOT + '/' + p): (f.data, f.mode) for p, f in files.items()}
        for p, data in metadata.items():
            key = safe_path(ARCHIVE_ROOT + '/HANDOFF/' + p)
            if key in entries: raise HandoffError('Archive metadata collides with a source path.')
            entries[key] = (data, '100644')
        for name, (data, mode) in sorted(entries.items()):
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED; info.create_system = 3
            info.external_attr = int(mode, 8) << 16
            archive.writestr(info, data)
    return hashlib.sha256(path.read_bytes()).hexdigest()


def instructions(role: dict, week: dict | None, selected: list[dict], snapshot: str) -> bytes:
    title = 'Všetky priradené zmeny' if week is None else 'Týždeň %02d · %s' % (week['number'], week['title'])
    payload = sum(c['action'] != 'deleted' for c in selected); removed = sum(c['action'] == 'deleted' for c in selected)
    text = [f'LUMEN · TÍM 3 · {title}', f'{role["name"]} · {role["role"]} · cieľ zodpovednosti 25 %', '',
            'Toto je plánované odovzdávanie aktuálneho AI-asistovaného zdrojového stavu.',
            'Balík netvrdí, kto existujúci kód napísal ani v ktorom minulom týždni vznikol.',
            f'Súbory na skopírovanie: {payload}; odstránenia na ručnú kontrolu: {removed}.',
            'SHA-256 celého cieľového zdrojového stavu: ' + snapshot, '',
            '1. Začni spoločným základom. Pri týždennej ceste najprv aplikuj všetky predošlé týždne.',
            '2. Prečítaj manifest. Skopíruj priečinky unity/ a web/ na rovnaké miesta v LUMEN/.',
            '3. Zoznam delete_paths sa sám nevykonáva. Pred odstránením si porovnaj base_sha256.',
            '4. Skontroluj skutočné zmeny. Nahraj ich sám pod svojím účtom a pravdivo opíš svoju prácu.',
            '5. Neuvádzaj percentá riadkov či falošné historické autorstvo. Nemeň spätne dátumy commitov.', '',
            'Zoznam kontrol z týždenného plánu je úloha pre človeka; tento balík nepotvrdzuje ich vykonanie.',
            'Štyri kumulatívne rolové ZIPy a štyridsať týždenných ZIPov sú alternatívy, nie dve sady rôznych zmien.']
    if week:
        text += ['', 'Tvoja úloha: ' + week['role_tasks'][role['id']], 'Spoločná podmienka: ' + week['gate']]
    if not payload and not removed:
        text += ['', 'V tomto balíku nie je nový zdrojový rozdiel. Obsahuje konkrétny plán a kontrolný podklad, nie predstieranú implementáciu.']
    return ('\n'.join(text) + '\n').encode('utf-8')


def validate_freeze(freeze: dict, baseline: dict, current: dict, repos: dict, config: dict) -> None:
    if freeze.get('schema_version') != 1 or not isinstance(freeze.get('captured_utc'), str):
        raise HandoffError('The freeze file has no supported schema or capture date.')
    if freeze.get('source_sha256') != fingerprint(current) or freeze.get('baseline_sha256') != fingerprint(baseline):
        raise HandoffError('Sources changed after freeze. Review them and explicitly capture a new freeze before packaging.')
    if freeze.get('ownership_sha256') != hashlib.sha256(json_bytes(config)).hexdigest():
        raise HandoffError('Role/week assignments changed after freeze.')
    if freeze.get('repositories') != repos:
        raise HandoffError('Git provenance changed after freeze. Review the commits and capture a new freeze.')


def package(output: Path, version: str, freeze: dict, baseline: dict, current: dict, repos: dict, config: dict) -> dict:
    if not re.fullmatch(r'[0-9A-Za-z][0-9A-Za-z._-]{0,40}', version): raise HandoffError('Use a simple version such as 0.7.0.')
    validate_freeze(freeze, baseline, current, repos, config)
    current_hash = fingerprint(current); baseline_hash = fingerprint(baseline)
    changes = changes_between(baseline, current, config); milestones = verify_schedule(baseline, current, changes)
    target = output / ('v' + version)
    if target.exists(): raise HandoffError('Version output already exists; use a new version or review it manually: ' + str(target))
    target.mkdir(parents=True)
    summary = dict(schema_version=1, version=version, created_utc=freeze['captured_utc'], responsibility_note=config['responsibility_note'],
                   source_sha256=current_hash, baseline_sha256=baseline_hash, repositories=repos,
                   roles=config['roles'], weeks=config['weeks'], milestones=milestones, changes=changes,
                   source_files=index_files(current, config), baseline_files=index_files(baseline, config), archives=[])
    readme_source = current.get('unity/docs/TEAM_HANDOFF.md')
    if readme_source is None: raise HandoffError('The frozen source must include docs/TEAM_HANDOFF.md.')
    readme = readme_source.data

    def emit(name: str, files: dict, metadata: dict, kind: str, **details):
        path = target / name
        digest = write_zip(path, files, metadata)
        summary['archives'].append(dict(file=name, sha256=digest, size_bytes=path.stat().st_size,
                                         kind=kind, source_payload_files=len(files), **details))

    provenance = dict(schema_version=1, repositories=repos, baseline_sha256=baseline_hash,
                      note='Based on actual Git snapshots, filtered for portable source. Disclosed optional-vendor define transformations may change copied settings bytes. Git history, deployment identity, caches, built players and paid source assets are excluded.',
                      files=index_files(baseline, config))
    emit(f'LUMEN-{version}-00-spolocny-zaklad.zip', baseline,
         {'BASELINE.json':json_bytes(provenance), 'PRECITAJ-MA.md':readme}, 'baseline')
    emit(f'LUMEN-{version}-99-uplny-zdroj.zip', current,
         {'FULL-SNAPSHOT.json':json_bytes(dict(source_sha256=current_hash, repositories=repos, files=index_files(current, config))),
          'PRECITAJ-MA.md':readme}, 'full-source')

    for role in config['roles']:
        selected = [c for c in changes if c['owner'] == role['id']]
        files = {c['path']:current[c['path']] for c in selected if c['action'] != 'deleted'}
        info = dict(role=role, source_sha256=current_hash, baseline_sha256=baseline_hash, repositories=repos, changes=selected,
                    delete_paths=[c['path'] for c in selected if c['action'] == 'deleted'])
        prefix = 'roles/' + role['slug'] + '/'
        emit(f'LUMEN-{version}-{role["slug"]}.zip', files,
             {prefix+'manifest.json':json_bytes(info), prefix+'ODOVZDANIE.txt':instructions(role,None,selected,current_hash)}, 'role', owner=role['id'])
        for week in config['weeks']:
            batch = [c for c in selected if c['week'] == week['number']]
            weekly_files = {c['path']:current[c['path']] for c in batch if c['action'] != 'deleted'}
            weekly_info = dict(role=role, week=week, source_sha256=current_hash, baseline_sha256=baseline_hash, repositories=repos,
                               changes=batch, delete_paths=[c['path'] for c in batch if c['action'] == 'deleted'],
                               required_previous_weeks=list(range(1,week['number'])),
                               after_week_source_sha256=milestones[week['number']-1]['source_sha256'],
                               source_payload_present=bool(batch), unity_build_verified=False)
            prefix = f'weeks/week-{week["number"]:02d}/{role["slug"]}/'
            emit(f'LUMEN-{version}-W{week["number"]:02d}-{role["slug"]}.zip', weekly_files,
                 {prefix+'manifest.json':json_bytes(weekly_info),prefix+'ODOVZDANIE.txt':instructions(role,week,batch,current_hash)},
                 'week-role', owner=role['id'], week=week['number'])

    (target/'MANIFEST.json').write_bytes(json_bytes(summary))
    hashes = ''.join(f'{a["sha256"]}  {a["file"]}\n' for a in summary['archives'])
    hashes += hashlib.sha256((target/'MANIFEST.json').read_bytes()).hexdigest() + '  MANIFEST.json\n'
    (target/'SHA256SUMS.txt').write_text(hashes,encoding='utf-8',newline='\n')
    (target/'PRECITAJ-MA.md').write_bytes(readme)
    (target/'freeze.json').write_bytes(json_bytes(freeze))
    return dict(output=str(target), archive_count=len(summary['archives']), weekly_role_archives=40,
                source_sha256=current_hash, changed_paths=len(changes), status='LOCAL_ONLY_NOT_UPLOADED')


def self_test(config: dict) -> dict:
    checks=0
    def check(value, message):
        nonlocal checks
        checks+=1
        if not value: raise AssertionError(message)
    for path in ('../escape','/absolute','C:/bad','bad\\name'):
        try: safe_path(path)
        except HandoffError: check(True,'unsafe path rejected')
        else: check(False,'unsafe path accepted')
    for path in ('Assets/Plugins/Sirenix/Editor.dll','Assets/Synty/Models/model.fbx','Assets/Lumen/Resources/Generated/SyntyDecor.prefab','Library/state','Assets/Test.unitypackage'):
        check(not allowed('unity',path),'paid/generated state excluded')
    check(not allowed('web','.openai/hosting.json') and not allowed('web','.env.local'),'deployment identity/secrets excluded')
    check(allowed('web','build/sites-worker.ts'),'retained build source is not mistaken for compiled output')
    for path in ('unity/Assets/Lumen/Core/PetBattles.cs','unity/Assets/Lumen/World/CompanionModels.cs','unity/Assets/Lumen/Runtime/LumenHud.Study.cs'):
        check(assignment(path,config)==assignment(path+'.meta',config),'Unity meta follows its source owner/week')
    check(assignment('unity/Assets/Lumen/Core/Domain.cs',config)==('programmer-1',1),'grade scaffolding week one')
    check(assignment('unity/Assets/Lumen/Runtime/LumenHud.Battle.cs',config)==('artist-2',9),'coupled HUD integrates week nine')
    check(assignment('unity/Assets/Lumen/Runtime/LumenHud.Portraits.cs',config)==('artist-2',9),'pet portrait UI owner and integration week')
    settings_path='unity/ProjectSettings/ProjectSettings.asset'
    settings=("PlayerSettings:\r\n  scriptingDefineSymbols:\r\n"
              "    Standalone: ODIN_INSPECTOR;KEEP_ME;ODIN_VALIDATOR_3_3\r\n"
              "    WebGL: 'ODIN_INSPECTOR_3;WEB_KEEP'\r\n"
              "    Android: ODIN_INSPECTOR\r\n  otherSetting: ODIN_INSPECTOR\r\n").encode('utf-8')
    source_settings={settings_path:SourceFile(settings_path,settings)}
    copied,transforms=portable_files(source_settings);sanitized=copied[settings_path].data
    check(source_settings[settings_path].data==settings,'live and original settings bytes untouched')
    check(b'Standalone: KEEP_ME\r\n' in sanitized and b"WebGL: 'WEB_KEEP'\r\n" in sanitized,'vendor defines removed and other platform defines retained')
    check(b"Android: ''\r\n" in sanitized and b'otherSetting: ODIN_INSPECTOR\r\n' in sanitized,'empty YAML string and unrelated settings preserved')
    check(transforms[0]['original_sha256']==hashlib.sha256(settings).hexdigest() and transforms[0]['portable_sha256']==hashlib.sha256(sanitized).hexdigest(),'vendor transform has both original and portable hashes')
    check(portable_files(copied)[1]==[],'portable settings transform is idempotent')
    base={'unity/Assets/Lumen/Core/Domain.cs':SourceFile('unity/Assets/Lumen/Core/Domain.cs',b'base'),
          'web/obsolete.txt':SourceFile('web/obsolete.txt',b'remove')}
    current={'unity/Assets/Lumen/Core/Domain.cs':SourceFile('unity/Assets/Lumen/Core/Domain.cs',b'updated'),
             'unity/Assets/Lumen/World/CompanionModels.cs':SourceFile('unity/Assets/Lumen/World/CompanionModels.cs',b'new')}
    changes=changes_between(base,current,config); milestones=verify_schedule(base,current,changes)
    check(len(changes)==3 and milestones[-1]['source_sha256']==fingerprint(current),'ten-week reconstruction includes add/modify/delete')
    check(len({c['path'] for c in changes})==len(changes),'disjoint source paths')
    with tempfile.TemporaryDirectory(prefix='lumen-handoff-fixture-') as directory:
        path=Path(directory)/'fixture.zip'
        digest=write_zip(path,current,{'fixture.json':b'{"fixture":true}\n'})
        with zipfile.ZipFile(path) as archive:
            check(archive.testzip() is None and all(n.startswith('LUMEN/') for n in archive.namelist()),'fixture ZIP integrity and safe root')
            check(archive.read('LUMEN/unity/Assets/Lumen/Core/Domain.cs')==b'updated','fixture bytes preserved')
        check(digest==hashlib.sha256(path.read_bytes()).hexdigest(),'archive checksum verified')
        try: write_zip(path,{}, {})
        except HandoffError: check(True,'existing ZIP not overwritten')
        else: check(False,'ZIP overwrite allowed')
        # Exercise the real packaging route using disposable synthetic sources.
        # No project source ZIPs or delivery freeze are produced by this fixture.
        current['unity/docs/TEAM_HANDOFF.md']=SourceFile('unity/docs/TEAM_HANDOFF.md',b'Fixture handoff, not project sources.\n')
        repos={'unity':{'baseline_commit':'1'*40,'head_commit':'2'*40,'working_tree_has_changes':True},
               'web':{'baseline_commit':'3'*40,'head_commit':'4'*40,'working_tree_has_changes':False}}
        frozen=dict(schema_version=1,captured_utc='2026-01-01T00:00:00+00:00',source_sha256=fingerprint(current),
                    baseline_sha256=fingerprint(base),ownership_sha256=hashlib.sha256(json_bytes(config)).hexdigest(),repositories=repos)
        validate_freeze(frozen,base,current,repos,config)
        for field, replacement in (('source_sha256','changed'),('ownership_sha256','changed'),('repositories',{})):
            changed=dict(frozen);changed[field]=replacement
            try: validate_freeze(changed,base,current,repos,config)
            except HandoffError: check(True,'changed '+field+' rejected')
            else: check(False,'changed '+field+' accepted')
        result=package(Path(directory),'fixture',frozen,base,current,repos,config)
        target=Path(result['output']);manifest=json.loads((target/'MANIFEST.json').read_text(encoding='utf-8'))
        check(result['archive_count']==46 and len(list(target.glob('*-W??-*.zip')))==40,'all four role packs for every week exist')
        restored=dict(base);seen=set()
        weekly=sorted((a for a in manifest['archives'] if a['kind']=='week-role'),key=lambda a:(a['week'],a['owner']))
        for item in manifest['archives']:
            check(hashlib.sha256((target/item['file']).read_bytes()).hexdigest()==item['sha256'],'packaged ZIP hash verified')
        for item in weekly:
            with zipfile.ZipFile(target/item['file']) as archive:
                check(archive.testzip() is None,'weekly ZIP integrity')
                names=archive.namelist();meta=next(n for n in names if n.endswith('/manifest.json'))
                info=json.loads(archive.read(meta))
                check(info['role']['id']==item['owner'] and info['week']['number']==item['week'],'weekly manifest role and order')
                for change in info['changes']:
                    name=change['path'];check(name not in seen,'source path delivered only once');seen.add(name)
                    if change['action']=='deleted': restored.pop(name,None)
                    else: restored[name]=SourceFile(name,archive.read('LUMEN/'+name),change['mode'])
        check(fingerprint(restored)==fingerprint(current),'actual forty ZIP payloads restore exact frozen source including deletion')
        check('Peter Baláž' in (target/'MANIFEST.json').read_text(encoding='utf-8'),'Slovak names preserved as UTF-8')
    return dict(status='HANDOFF_SELF_TEST_PASS',checks=checks,final_archives_created=False)


def main() -> int:
    parser=argparse.ArgumentParser(description=__doc__)
    action=parser.add_mutually_exclusive_group()
    action.add_argument('--check',action='store_true',help='Validate and print the plan, without ZIP files (default).')
    action.add_argument('--self-test',action='store_true',help='Run temporary fixture checks only.')
    action.add_argument('--capture-freeze',type=Path,metavar='FILE',help='Explicitly record the stable source fingerprints outside source repos.')
    action.add_argument('--package',action='store_true',help='Create local ZIPs only if the explicit freeze still matches.')
    parser.add_argument('--freeze',type=Path)
    parser.add_argument('--output',type=Path,default=UNITY_ROOT.parent/'LUMEN-TEAM-HANDOFF')
    parser.add_argument('--version',default='0.7.0')
    parser.add_argument('--unity-root',type=Path,default=UNITY_ROOT)
    sibling=UNITY_ROOT.parent/('web' if (UNITY_ROOT.parent/'web').is_dir() else 'lumen')
    parser.add_argument('--web-root',type=Path,default=sibling)
    parser.add_argument('--unity-base');parser.add_argument('--web-base')
    args=parser.parse_args(); config=load_ownership()
    if args.self_test:
        print(json.dumps(self_test(config),ensure_ascii=False,indent=2));return 0
    roots=(args.unity_root.resolve(),args.web_root.resolve())
    baseline,current,repos=capture(*roots,config,args.unity_base,args.web_base)
    changes=changes_between(baseline,current,config); milestones=verify_schedule(baseline,current,changes)
    state=dict(schema_version=1,captured_utc=datetime.now(timezone.utc).isoformat(),
               source_sha256=fingerprint(current),baseline_sha256=fingerprint(baseline),
               ownership_sha256=hashlib.sha256(json_bytes(config)).hexdigest(),repositories=repos,
               source_file_count=len(current),planned_archives=46,planned_weekly_archives=40)
    if args.capture_freeze:
        target=outside_sources(args.capture_freeze,roots)
        if target.exists(): raise HandoffError('Freeze already exists. Review it manually rather than silently replacing it.')
        target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(json_bytes(state))
        print(json.dumps(dict(status='FREEZE_CAPTURED_NO_ARCHIVES',file=str(target),**state),ensure_ascii=False,indent=2));return 0
    if args.package:
        if not args.freeze: raise HandoffError('--package requires --freeze FILE captured after source changes stop.')
        freeze_path=outside_sources(args.freeze,roots);output=outside_sources(args.output,roots)
        frozen=json.loads(freeze_path.read_text(encoding='utf-8'))
        print(json.dumps(package(output,args.version,frozen,baseline,current,repos,config),ensure_ascii=False,indent=2));return 0
    role_counts={r['name']:sum(c['owner']==r['id'] for c in changes) for r in config['roles']}
    weekly={f'{w:02d}':{r['name']:sum(c['owner']==r['id'] and c['week']==w for c in changes) for r in config['roles']} for w in range(1,11)}
    print(json.dumps(dict(status='PLAN_CHECKED_NO_FREEZE_NO_ARCHIVES',**state,changed_paths=len(changes),role_changed_paths=role_counts,
                          weekly_changed_paths=weekly,milestones=milestones,
                          paid_sources_and_deployment_identity_excluded=True),ensure_ascii=False,indent=2));return 0


if __name__=='__main__':
    try: raise SystemExit(main())
    except (HandoffError,OSError,ValueError,subprocess.TimeoutExpired) as error:
        print('HANDOFF_ERROR: '+str(error),file=sys.stderr);raise SystemExit(1)
