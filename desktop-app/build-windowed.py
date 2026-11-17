"""Rebuild only ChromiumHost from pinned upstream v1.1.1; retain original release."""
from pathlib import Path
import hashlib
import subprocess
import urllib.request
import zipfile

root = Path(__file__).resolve().parent.parent
work = root / 'work' / 'windowed'
package = 'com/github/serezhka/airplay/app/mse/'
original = root / 'releases' / 'java-airplay-mse-1.1.1.jar'
output = original.with_name('java-airplay-mse-1.1.1-windowed.jar')
work.mkdir(parents=True, exist_ok=True)
source = Path(__file__).parent / 'ChromiumHost.java'
if not source.exists():
    url = 'https://raw.githubusercontent.com/serezhka/java-airplay/v1.1.1/app/mse/src/main/java/' + package + 'ChromiumHost.java'
    code = urllib.request.urlopen(url).read().decode()
    code = code.replace('import lombok.extern.slf4j.Slf4j;', '')
    code = code.replace('@Slf4j\n', '')
    code = code.replace('final class ChromiumHost implements AutoCloseable {', 'final class ChromiumHost implements AutoCloseable {\n    private static final org.slf4j.Logger log = org.slf4j.LoggerFactory.getLogger(ChromiumHost.class);')
    assert code.count('cmd.add("--start-fullscreen");') == 1
    code = code.replace('cmd.add("--start-fullscreen");', 'cmd.add("--window-size=1120,760");')
    code = code.replace('fullscreen', 'normal resizable').replace('full-screen', 'normal resizable')
    source.write_text(code, encoding='utf-8')
with zipfile.ZipFile(original) as z:
    libraries = [n for n in z.namelist() if n.startswith('BOOT-INF/lib/slf4j-api-')]
    assert len(libraries) == 1
    lib = work / Path(libraries[0]).name
    lib.write_bytes(z.read(libraries[0]))
    cls = work / package / 'MseBrowsers.class'
    cls.parent.mkdir(parents=True, exist_ok=True)
    cls.write_bytes(z.read('BOOT-INF/classes/' + package + 'MseBrowsers.class'))
subprocess.run(['javac', '--release', '21', '-encoding', 'UTF-8', '-cp', str(work)+';'+str(lib), '-d', str(work), str(source)], check=True)
test = work / 'WindowCheck.java'
test.write_text('''package com.github.serezhka.airplay.app.mse;
import java.nio.file.Path;
class WindowCheck {
 public static void main(String[] args) {
  var window = ChromiumHost.buildLaunchCommand("edge", Path.of("profile"), "http://localhost", false);
  if (!window.contains("--window-size=1120,760") || window.stream().anyMatch(x -> x.contains("fullscreen") || x.contains("kiosk"))) throw new AssertionError(window);
  if (!window.contains("--app=http://localhost")) throw new AssertionError(window);
  var headless = ChromiumHost.buildLaunchCommand("edge", Path.of("profile"), "http://localhost", true);
  if (!headless.contains("--headless=new") || headless.contains("--window-size=1120,760")) throw new AssertionError(headless);
  System.out.println("PASS: normal resizable app window; no fullscreen/kiosk; headless preserved.");
 }
}''', encoding='utf-8')
subprocess.run(['javac', '--release', '21', '-cp', str(work)+';'+str(lib), '-d', str(work), str(test)], check=True)
subprocess.run(['java', '-cp', str(work)+';'+str(lib), package.replace('/','.')+'WindowCheck'], check=True)
replacement = (work/package/'ChromiumHost.class').read_bytes()
with zipfile.ZipFile(original) as src, zipfile.ZipFile(output, 'w') as dst:
    changed = []
    for info in src.infolist():
        data = src.read(info.filename)
        if info.filename == 'BOOT-INF/classes/'+package+'ChromiumHost.class':
            data = replacement
            changed.append(info.filename)
        dst.writestr(info, data)
assert len(changed) == 1
with zipfile.ZipFile(original) as a, zipfile.ZipFile(output) as b:
    differences = [n for n in a.namelist() if a.read(n) != b.read(n)]
    assert differences == changed, differences
(work/'verification.txt').write_text('PASS: window launch test; only ChromiumHost.class differs from upstream.\nSHA256 '+hashlib.sha256(output.read_bytes()).hexdigest(), encoding='utf-8')
print(output)
