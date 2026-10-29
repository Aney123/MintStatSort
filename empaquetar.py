"""Package reviewed project files without caches, user data or vendor DLLs."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
SKIP = {'.git', 'bin', 'obj', '__pycache__', '.vs', 'dist', 'artifacts', 'Dependencies', 'Revision-privada'}
ALLOWED_DLLS = {'Mod/Assemblies/MintStatSort.dll', 'Experimental/RimWorld17/Snapshot/1.7/Assemblies/MintStatSort.dll'}

def project_files(root=ROOT):
    for p in sorted(root.rglob('*')):
        relative = p.relative_to(root)
        if not p.is_file() or p.is_symlink() or any(part in SKIP for part in relative.parts):
            continue
        if p.name == 'PublishedFileId.txt' or p.name.startswith('.env') or p.suffix.lower() in {'.pem', '.pfx', '.p12', '.key', '.suo', '.user'}:
            raise RuntimeError('Private file is not allowed: ' + relative.as_posix())
        if p.suffix.lower() == '.dll' and relative.as_posix() not in ALLOWED_DLLS:
            raise RuntimeError('Unexpected DLL: ' + relative.as_posix())
        if p.suffix.lower() in {'.exe', '.rws', '.nupkg', '.pdb'}:
            raise RuntimeError('Unexpected binary/user file: ' + relative.as_posix())
        yield p

if __name__ == '__main__':
    version = ET.parse(ROOT / 'Mod/About/About.xml').findtext('modVersion')
    destination = ROOT / 'dist' / ('MintStatSort-source-' + version + '.zip')
    destination.parent.mkdir(exist_ok=True)
    if destination.exists():
        raise SystemExit('Output already exists; choose a new destination before rebuilding.')
    with ZipFile(destination, 'w', ZIP_DEFLATED) as archive:
        for p in project_files():
            archive.write(p, 'MintStatSort/' + p.relative_to(ROOT).as_posix())
    with ZipFile(destination) as archive:
        assert archive.testzip() is None
    print(destination)
