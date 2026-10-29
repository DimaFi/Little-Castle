"""Prepare explicitly selected code on a public parent using an isolated Git index.

No checkout, Unity copy, network operation or push. The source commit may have
private ancestors; its history is never used as the publication parent.
"""
from pathlib import Path, PurePosixPath
import argparse, hashlib, json, os, subprocess, tempfile

ROOT = Path(__file__).resolve().parents[1]
PRIVATE = 'c143b4a'
SUFFIXES = {'.cs', '.shader', '.hlsl', '.cginc', '.compute', '.asmdef', '.asmref', '.py', '.ps1'}
PACKAGE_CONFIG = {'Packages/manifest.json', 'Packages/packages-lock.json'}

def git(*args, env=None, input=None, check=True):
    result = subprocess.run(['git', '-C', str(ROOT), *args], env=env, input=input,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if check and result.returncode:
        raise SystemExit('Git '+args[0]+' failed: '+result.stderr.decode('utf-8',errors='replace'))
    return result

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--public-base', default='origin/main')
    parser.add_argument('--source', default='HEAD')
    parser.add_argument('--paths-file', type=Path, required=True, help='JSON array of exact reviewed code paths')
    parser.add_argument('--output-dir', type=Path, required=True)
    parser.add_argument('--create-ref', help='Optional new refs/heads/...; never overwrites an existing ref')
    parser.add_argument('--allow-package-config', action='store_true', help='Also permit the two exact Unity package configuration files; review dependency changes separately')
    parser.add_argument('--message', default='Prepare reviewed code-only publication', help='Commit description for the new public-parent ref')
    args = parser.parse_args()
    base = git('rev-parse', '--verify', args.public_base+'^{commit}').stdout.decode().strip()
    source = git('rev-parse', '--verify', args.source+'^{commit}').stdout.decode().strip()
    private = git('rev-parse', '--verify', PRIVATE+'^{commit}', check=False)
    if private.returncode == 0 and git('merge-base', '--is-ancestor', PRIVATE, base, check=False).returncode == 0:
        raise SystemExit('Refusing a public base containing the private map snapshot')
    paths = json.loads(args.paths_file.read_text(encoding='utf-8-sig'))
    if not paths or not isinstance(paths,list) or not all(isinstance(p,str) for p in paths):
        raise SystemExit('Provide a nonempty exact code-path allowlist')
    for p in paths:
        rel = PurePosixPath(p)
        sibling = p[:-5] if p.endswith('.meta') else p
        if ('\\' in p or rel.is_absolute() or '..' in rel.parts or ':' in p
            or not ((p.startswith('Assets/_Game/') or p.startswith('Tools/'))
                    and PurePosixPath(sibling).suffix in SUFFIXES
                    or args.allow_package_config and p in PACKAGE_CONFIG)
            or (p.endswith('.meta') and sibling not in paths)):
            raise SystemExit('Not a reviewed code path: '+p)
    output = args.output_dir.resolve()
    # Prevent publication artifacts from becoming part of the Unity source tree.
    if output == ROOT or ROOT in output.parents:
        raise SystemExit('Output must be outside the Unity project')
    output.mkdir(parents=True, exist_ok=True)
    patch = git('diff','--binary','--no-ext-diff',base,source,'--',*paths).stdout
    (output/'code.patch').write_bytes(patch)
    with tempfile.TemporaryDirectory(prefix='lc-code-index-',dir=output) as folder:
        env = dict(os.environ, GIT_INDEX_FILE=str(Path(folder)/'index'))
        git('read-tree',base,env=env)
        if patch: git('apply','--cached','--binary','--whitespace=nowarn','-',env=env,input=patch)
        changed = git('diff','--cached','--name-only',base,env=env).stdout.decode().splitlines()
        assert set(changed) <= set(paths), changed
        tree = git('write-tree',env=env).stdout.decode().strip()
    commit = None
    if args.create_ref:
        if not args.create_ref.startswith('refs/heads/codex/'):
            raise SystemExit('Use a new refs/heads/codex/... reference')
        git('check-ref-format',args.create_ref)
        if git('show-ref','--verify',args.create_ref,check=False).returncode == 0:
            raise SystemExit('Ref already exists; refusing to overwrite')
        commit = git('commit-tree',tree,'-p',base,input=(args.message+'\n').encode('utf-8')).stdout.decode().strip()
        if private.returncode == 0:
            assert git('merge-base','--is-ancestor',PRIVATE,commit,check=False).returncode != 0
        assert git('rev-list','--parents','-n','1',commit).stdout.decode().split()[1:] == [base]
        git('update-ref',args.create_ref,commit,'0'*40)
    manifest = {'publicBase':base,'source':source,'parentPolicy':'Only publicBase; no source ancestry',
                'changedPaths':changed,'reviewedPaths':paths,'tree':tree,'commit':commit,'createdRef':args.create_ref,
                'privateAncestorExcluded':True,'patchSha256':hashlib.sha256(patch).hexdigest(),
                'network':'NOT USED','push':'NOT PERFORMED','requiresReview':'Review code.patch and manifest before publication'}
    (output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'changedCodePaths':len(changed),'commit':commit,'manifest':str(output/'manifest.json')}))

if __name__ == '__main__': main()
