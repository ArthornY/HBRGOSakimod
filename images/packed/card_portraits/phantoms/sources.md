# Kaiser claw phantom portraits

`rocket.png` is the right claw (Rocket); `crusher.png` is the left claw (Crusher).
Both use the game's original Kaiser Crab atlas texels, skeleton, skin, mesh weights,
and three idle animation tracks at 0.1 seconds. They contain the complete visible
arm and claw, on transparency. The existing portrait compositor supplies the white
card background and framing at runtime.

Source files: `sts/animations/monsters/kaiser_crab/kaiser_crab.skel`,
`kaiser_crab.atlas`, and the four corresponding PNG atlas pages in the development
workspace. The skeleton format is 4.2.43. The left/right slot whitelists are in
`Core/Helpers/PhantomPortraitSpineSlots.cs`.

Generation record (2026-09-29):
`output/phantom-portrait-crash-20260929/export-claws.mjs` exports deformed triangles
with the official `@esotericsoftware/spine-core` 4.2.120 tool;
`rasterize-claws.py` samples the original texels and rasterizes them on the CPU.
The output directory contains source hashes, geometry, and verification results.
No Godot process or GPU rendering is involved in this asset generation.

The Spine runtime is an offline development tool only, retained with its original
license in the output directory. It is not a game dependency or bundled mod file.

Static portraits deliberately avoid instantiating the shared Kaiser Crab Spine
scene inside a portrait SubViewport: the 1.2.0.29 logs repeatedly terminate during
that path's frame capture for Rocket or Crusher.
