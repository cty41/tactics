# Sprite Preview

A small desktop tool for previewing sprite frame sequences.

## Features

- Loads frames from a selected folder using natural filename ordering
- Supports both `FPS` mode and `Duration` mode
- Supports play, pause, previous frame, next frame, and reset
- Runs as a local desktop preview tool without Unity

## Install

```powershell
python -m pip install -r Tools/sprite_preview/requirements.txt
```

## Run

```powershell
python Tools/sprite_preview/main.py --source "D:\path\to\idle"
```

## Common Arguments

- `--mode fps`: play using a fixed frame rate
- `--mode duration`: spread frames across a total duration
- `--fps 30`: set playback FPS
- `--duration 1.0`: set total playback duration
- `--no-loop`: disable looping
- `--no-autoplay`: do not start playback automatically after loading

## Notes

- By default, only direct child files of the selected folder are scanned
- A single image file is supported as a one-frame preview
- Common static image formats are supported by default, including PNG, JPG, BMP, TGA, and WEBP
