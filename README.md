# Control Tools - Blender Transformations

Unity editor tools for Blender-style transformation workflows in the Scene View, including shortcut-driven move, rotate, scale, reset, visibility, and selection behavior.

## Requirements

Unity 6 / 6000.0 or newer.

## Installation

Install from a Git URL:

```text
https://github.com/OWNER/com.control-tools.blender-transformations.git#0.1.0
```

Install from a local file path during development:

```json
"com.control-tools.blender-transformations": "file:../../com.control-tools.blender-transformations"
```

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

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
