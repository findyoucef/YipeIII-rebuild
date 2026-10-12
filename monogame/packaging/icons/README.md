# Yipe! III app icon

Original pixel-art knight and castle artwork generated with the built-in
image_gen tool. The silver helmet, red plume, blue cape and castle roofs follow
the game's existing hero and town sprites. The exact prompt is in [PROMPT.txt](PROMPT.txt).

- `YipeIII.png`: original transparent master (1254 × 1254).
- `YipeIII.icns`: macOS Finder/bundle icon with ten standard 1×/2× representations
  from 16 to 1024 pixels.
- `../../Yipe.Desktop/Icon.bmp`: 256 × 256, 32-bit BMP with alpha. MonoGame
  DesktopGL reads the embedded resource named `Icon.bmp` and sets the running
  window/Dock icon, including ordinary local builds.

The macOS packager copies the ICNS into `Contents/Resources` and declares it in
`CFBundleIconFile`. These assets are checked in; ordinary builds require no image
generation or conversion tools. The package verifier decodes all ten ICNS sizes
and checks their dimensions and alpha support.

To regenerate the platform formats after replacing the master, run from the
repository root on macOS (only resize/format conversion; preserve transparency):

```python
from pathlib import Path
import subprocess

icons = Path("monogame/packaging/icons")
iconset = Path("build/app-icon/YipeIII.iconset")
iconset.mkdir(parents=True, exist_ok=True)
for size in [16, 32, 128, 256, 512]:
    for scale in [1, 2]:
        pixels = str(size * scale)
        name = f"icon_{size}x{size}" + ("@2x" if scale == 2 else "") + ".png"
        subprocess.run(["sips", "-z", pixels, pixels, str(icons / "YipeIII.png"),
                        "--out", str(iconset / name)], check=True)
subprocess.run(["iconutil", "-c", "icns", str(iconset), "-o", str(icons / "YipeIII.icns")], check=True)
subprocess.run(["sips", "-s", "format", "bmp", str(iconset / "icon_256x256.png"),
                "--out", "monogame/Yipe.Desktop/Icon.bmp"], check=True)
```

MonoGame's resource contract is defined in
[SDLGameWindow.cs (3.8.5.1)](https://github.com/MonoGame/MonoGame/blob/v3.8.5.1/MonoGame.Framework/Platform/SDL/SDLGameWindow.cs).
