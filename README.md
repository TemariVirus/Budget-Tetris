# Budget-Tetris

Check the Release branch for more info.

## Current Plans

- [x] Build a backend engine mostly following the [2009 guidelines](https://tetris.fandom.com/wiki/Tetris_Guideline), but adding some flexibility like multiple rotation systems
- [ ] Compile client to wasm and run on the web?

## Building

```bash
git clone --single-branch -b main https://github.com/TemariVirus/Budget-Tetris.git # Only clone the main branch
cd Budget-Tetris
git submodule update --init --recursive
zig build run
```

## Child Projects (added as submodules)

- [bot](https://github.com/TemariVirus/Budget-Tetris-Bot)
- [engine](https://github.com/TemariVirus/Budget-Tetris-Engine)
