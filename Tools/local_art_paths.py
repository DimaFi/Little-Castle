"""Resolve canonical sibling-workspace art paths to independent project copies."""
from pathlib import Path, PurePosixPath
import hashlib

GAME = Path(__file__).resolve().parents[1]
ROOT = GAME.parent
ARCHIVE = GAME / 'SourceAssets/Dependencies'


def canonical_path(relative):
    value = PurePosixPath(str(relative).replace('\\', '/'))
    if value.is_absolute() or '..' in value.parts or ':' in str(value):
        raise ValueError('Expected a canonical path relative to the workspace parent: ' + str(relative))
    if not value.parts or value.parts[0] not in ('CozySettlement', 'Little-Castle_Assets'):
        raise ValueError('Unknown canonical art workspace: ' + str(relative))
    return value.as_posix()


def resolve_source(relative, local_only=False):
    relative = canonical_path(relative)
    local = ARCHIVE / relative
    if local.is_file():
        return local
    legacy = ROOT / relative
    if not local_only and legacy.is_file():
        return legacy
    raise FileNotFoundError('Missing archived source: ' + str(local) +
                            ('' if local_only else '; legacy fallback: ' + str(legacy)))


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()
