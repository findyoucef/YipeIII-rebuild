---
name: remaster-yipe-caves
description: Remaster Yipe III cave environments using the approved breeding-ground and Fire Cave pixel-art techniques for connected stone, material-specific floors, cohesive creatures, contour lighting, and restrained atmosphere. Use for cave visual upgrades or corrections; adapt the treatment to the target cave rather than copying its layout or theme.
---

# Remaster Yipe Caves

Bring a requested cave up to the approved remaster's visual quality while keeping
its identity and gameplay. Work in the user's YipeIII-rebuild checkout; source
and evidence paths in this skill's reference are relative to that repository.

## Establish the target and reference

- Resolve region, layer, map IDs and landmarks with the repository's
  `.agents/skills/locate-map-region/SKILL.md` before diagnosing a named location.
  Inspect its actual map render. Region labels repeat across layers; an
  environment number alone does not define a cave's scope.
- Compare the target with the approved breeding-ground capture identified in
  [the technique reference](references/yipe-cave-techniques.md#approved-reference).
  For floor work, also inspect the accepted
  [Fire Cave floor](references/yipe-cave-techniques.md#accepted-fire-cave-floor).
  Use current Hell/Earth renders when they are relevant stylistic references.
  Old captures may depict superseded renderers.
- Preserve map topology, collision, doors, secrets, stairs, encounters and rescue
  positions unless the user has requested gameplay changes. Keep visual changes
  scoped to the resolved rooms; do not add another cave to `IsBreeding` merely
  to inherit its art, authored props or light positions.
- Name the visual problem concretely: slab scale, silhouette, floor contrast,
  creature integration, rectangular shadows, sparse atmosphere, or room identity.
  A narrow correction should reuse the relevant part of this workflow.

## Build a coherent pixel-art treatment

1. **Silhouette and stone.** Keep native pixels and point sampling. Connected,
   chipped rock, narrow joints, small facets and shaded front faces worked better
   than broad rounded slabs, patchy colour blobs or continuous bright rims.
   Match the reference's detail density before changing render resolution.
   Start with [walls and continuity](references/yipe-cave-techniques.md#walls-and-continuity).
2. **Floor and props.** Define what the ground is made of and what shaped it:
   cooled lava, compacted dirt, eroded limestone, ice or deposited sediment.
   Build readable large forms, shallow surface marks and sparse fine detail in
   a coherent palette. Keep floor contrast quieter than the walls, with contact
   darkness following actual rock contours. Tie debris and foot dust to that
   material, and keep routes, hazards and creatures readable. Use
   [material-specific floors](references/yipe-cave-techniques.md#material-specific-floors)
   to adapt the successful layering; glowing cracks and crust plates belong
   to the volcanic example, not every cave.
3. **Creature integration.** If monsters appear pasted on, first try a restrained
   world-sprite ambient tint and stronger existing contact shadows. Preserve
   identity, animation, hover height, scale and battle artwork. See
   [floor and creatures](references/yipe-cave-techniques.md#floor-and-creatures).
4. **Lighting.** Give each light a visible source. Ground-contact shadows and
   light occlusion should use the same visible rock silhouette. Diagnose light
   and terrain separately using lighting on/off and isolated-mask captures.
   Follow [contour lighting](references/yipe-cave-techniques.md#contour-lighting)
   when square cutoffs or ground borders are involved.
5. **Atmosphere.** Add sparse native-pixel movement appropriate to the room, such
   as drips, small ripples, spores or falling grit. Use presentation time and
   deterministic seeds, not gameplay RNG. Optional ambience should support the
   space without masking actions; reuse existing audio lifecycle and credits.

Inspect a representative room at native resolution and actual gameplay scale
before propagating the treatment through the authorized cave. Continue refining
and completing that scope without inventing intermediate approval gates.

## Verify and preserve the result

- Cache baked terrain and light masks; invalidate on map replacement and relevant
  tile changes. Respect matching region overlaps, fog and secret-tile aliases.
- Use isolated sessions for visual checks. Do not navigate or replace the live
  adventure to stage screenshots. For a requested launch, read the current save
  from `build/local-game-launch.json`; do not hard-code an old save or substitute
  a demo. Inspect running processes before relaunching.
- Build locally. Package only when requested. Use the commands and scope guidance
  in [verification](references/yipe-cave-techniques.md#verification); a breeding-only
  run does not verify a different cave.
- Review before/after native and gameplay captures, the changed cave's linked
  rooms, and unaffected reference rooms. Test meaningful risks introduced by
  the change: doors, fog, secrets, cache invalidation, creature presentation and
  full gameplay/RNG preservation. Code checks do not establish visual approval.
- Retain relevant captures and verification under `monogame/evidence/`, and
  update `monogame/PORTING_PLAN.md` with the result and any open issues. Record
  actual user approval separately from implementation and test completion.

The detailed numbers in the reference document the successful breeding-ground
and Fire Cave implementations. They are starting points, not mandatory art
settings for every cave. Preserve an existing approved treatment when the
request only concerns one part of it.
