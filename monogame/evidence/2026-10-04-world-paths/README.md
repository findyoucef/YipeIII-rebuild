# World path polish — 2026-10-04

The user requested the same consistency improvement for walking paths as for
world water. World roads now reuse the approved village paving: native stone
texture, four texture orientations, narrow irregular earth edges and rounded
bends/junctions. Authored road tile directions preserve straight routes and all
four corner orientations, including continuations at map borders. Buildings and
cave entrances receive the village's existing paved-doorstep treatment.

The existing atlas compiler moved unchanged from TownScenery to RetroArt; both
renderers now use it and a small shared tile-to-direction mapping. The old flat
world-road rectangles and inferred neighbor connections were removed. Each
renderer owns/disposes its compiled texture. This is a rendering-only change;
no new image assets, collision, timers, RNG, movement or save behavior changed.

Validation:

- Checked Release build: zero warnings/errors; all **633 graphics checks pass**.
- Visually inspected road-bearing surface maps 10, 11, 12, 16, 17, 22, 23, 27
  and 28, plus the sandstone cave approach. All-map rendering remains covered.
- The approved village capture and native terrain capture are byte-identical
  to the preceding build; hashes are in `unchanged-village.json`.
- Core and input source fingerprints match the campaign-completion checkpoint.
  Their rules/replay suites were not repeated for this rendering-only change.
- Final simplicity review confirms one shared existing atlas compiler, a direct
  mapping/draw call, correct texture disposal, and no duplicate road generator.

The selected `*-before.png` / `*-after.png` pairs are native diagnostic full-map
captures. Logs and source/art fingerprints are retained in this folder.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```
