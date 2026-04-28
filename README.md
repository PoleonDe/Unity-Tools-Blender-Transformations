# Control Tools - Blender Transformations

Unity editor tools for Blender-style transformation workflows in the Scene View, including shortcut-driven move, rotate, scale, reset, visibility, and selection behavior.

## Requirements

Unity 6 / 6000.0 or newer.

## Installation

Install the package directly from GitHub with Unity Package Manager:

1. Open `Window > Package Manager` in Unity.
2. Select `+ > Install package from git URL...`.
3. Enter this URL:

```text
https://github.com/PoleonDe/Unity-Tools-Blender-Transformations.git
```

You can also add the package to `Packages/manifest.json`:

```json
"com.control-tools.blender-transformations": "https://github.com/PoleonDe/Unity-Tools-Blender-Transformations.git"
```

To install a specific version later, create and push a Git tag, then append it to the URL, for example `#0.1.0`.

## Basic Usage

After installation, use the Scene View shortcuts provided by the package. Settings are available in `Project Settings > Control Tools > Blender Transformations`.

## Folder Structure

- `Runtime/`: runtime assembly placeholder for future shared APIs.
- `Editor/`: Unity Editor transform tools, shortcuts, settings, and icons.
- `Tests/`: package test folders.
- `Samples~/Basic Usage/`: importable sample content.
- `Documentation~/`: setup and usage notes.

## Known Limitations

- The transform workflow is editor-only.
- Shortcut installation can affect matching Scene View shortcuts in the current Unity editor profile.
- Git URL installation requires Git to be installed and available on the system path used by Unity.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
