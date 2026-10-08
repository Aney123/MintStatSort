from pathlib import Path
import base64
import hashlib
import re
import textwrap
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT.parent / "MintStatSort-0.3-SelectorVisual.zip"
FALLBACK = ROOT.parent / "CREAR-ZIP-MintStatSort-0.3-SelectorVisual.txt"
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
assembly = ROOT / "Mod" / "Assemblies" / "MintStatSort.dll"
if not assembly.exists() or assembly.read_bytes()[:2] != b"MZ":
    raise SystemExit("Compila primero la DLL.")
with zipfile.ZipFile(OUTPUT, "w", zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(ROOT.rglob("*")):
        relative = path.relative_to(ROOT)
        if not path.is_file() or any(part in {".git", "obj", "bin", "__pycache__", "Dependencies", "dist"} for part in relative.parts):
            continue
        if path.name == ".gitignore":
            continue
        data = path.read_bytes()
        if path.suffix in {".cmd", ".ps1"}:
            data = data.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
        item = zipfile.ZipInfo("MintStatSort/" + relative.as_posix())
        item.external_attr = 0o644 << 16
        item.compress_type = zipfile.ZIP_DEFLATED
        archive.writestr(item, data)
with zipfile.ZipFile(OUTPUT) as archive:
    assert archive.testzip() is None
    assert "MintStatSort/Mod/Assemblies/MintStatSort.dll" in archive.namelist()
    assert not any(name.endswith(("DubsMintMenus.dll", "NiceBillTab.dll", "Assembly-CSharp.dll", "0Harmony.dll")) for name in archive.namelist())
    for name in archive.namelist():
        if name.endswith(".xml"):
            ET.fromstring(archive.read(name))
    translations = []
    for language in ("English", "Spanish"):
        xml = ET.fromstring(archive.read(f"MintStatSort/Mod/Languages/{language}/Keyed/MintStatSort.xml"))
        translations.append({child.tag: sorted(re.findall(r"\{\d+\}", child.text or "")) for child in xml})
    assert translations[0] == translations[1], "Las traducciones deben tener las mismas claves y argumentos."

data = OUTPUT.read_bytes()
checksum = hashlib.sha256(data).hexdigest()
payload = textwrap.fill(base64.b64encode(data).decode("ascii"), width=96)
script = """# COPIA TODO ESTE TEXTO Y PEGALO EN POWERSHELL DE WINDOWS.
# Crea el ZIP actualizado en tu Escritorio. No necesita Internet.
& {
    $ErrorActionPreference = 'Stop'
    $mssPayload = @'
""" + payload + """
'@
    $mssBytes = [Convert]::FromBase64String($mssPayload)
    $mssHasher = [Security.Cryptography.SHA256]::Create()
    try {
        $mssHash = [BitConverter]::ToString($mssHasher.ComputeHash($mssBytes)).Replace('-', '').ToLowerInvariant()
    } finally {
        $mssHasher.Dispose()
    }
    if ($mssHash -ne '""" + checksum + """') {
        throw 'El texto se copio incompleto. Vuelve a copiar todo el contenido.'
    }
    $mssDesktop = [Environment]::GetFolderPath('DesktopDirectory')
    if (-not $mssDesktop -or -not (Test-Path -LiteralPath $mssDesktop)) {
        $mssDesktop = [Environment]::GetFolderPath('MyDocuments')
    }
    $mssFileName = 'MintStatSort-0.3-SelectorVisual-' + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.zip'
    $mssZipPath = Join-Path $mssDesktop $mssFileName
    [IO.File]::WriteAllBytes($mssZipPath, $mssBytes)
    Write-Host ('ZIP creado correctamente: ' + $mssZipPath) -ForegroundColor Green
    Write-Host 'Extrae el ZIP y ejecuta Instalar-mod.cmd. Actualiza Mint Stat Sort guardando una copia de la version anterior.'
    Write-Host 'Activa Harmony, tu menu (Nice Bill Tab o Dubs Mint Menus) y Mint Stat Sort, en ese orden, y reinicia RimWorld.'
}
"""
FALLBACK.write_text(script, encoding="utf-8")
assert base64.b64decode(script.split("$mssPayload = @'\n", 1)[1].split("\n'@", 1)[0]) == data
OUTPUT.chmod(0o644)
FALLBACK.chmod(0o644)
print(f"{OUTPUT} ({OUTPUT.stat().st_size:,} bytes)")
print(f"{FALLBACK} ({FALLBACK.stat().st_size:,} bytes)")
print(f"SHA-256: {checksum}")
