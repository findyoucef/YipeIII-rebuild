# Earned campaign prefix — 2026-10-04

**26,804 C++ state/RNG observations match C#**, starting with the same fresh
character and earned opening. Six retained later native saves also match the
complete converted persistent state. This prefix earns seven rescues, two stamps,
the Rainbow Key and strength 40. Two ordinary deaths are retained. It ends in
Hell; it does **not** establish campaign completion.

`commands.txt` is the literal input stream after the shared 42-checkpoint opening.
`states.jsonl.gz` contains all native observations in order; checkpoints.jsonl
indexes six native production saves. No observation snapshot is loaded into play.
The source C++ is unchanged; test inputs purchase actual gear/potions, defeat
monsters and regenerate via elapsed exploration ticks. The native driver rejects
EOF before completion by design; these incomplete prefixes never get an exit-0
completion claim. The authoring helper's repeated fresh input replays compared
every prior observation before extending the route.

```sh
dotnet run --project monogame/Yipe.Tests -c Release -- --campaign-prefix monogame/evidence/2026-10-04-campaign-prefix
python3 monogame/tools/campaign_driver.py --build-only
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy build/monogame-reference/campaign-reference --full-game < monogame/evidence/2026-10-04-campaign-prefix/commands.txt > build/campaign-prefix-native.log 2>&1
```

The last command intentionally exits 1 because this is a prefix. Each `STATE`
line can be compared to the decompressed retained JSONL. `--campaign-route`
requires explicit quit plus every progression prerequisite and the live finale;
it must reject this prefix. New authoring iterations in `build/campaign-author-*`
are disposable; retain the final winning inputs/evidence in the repository.
