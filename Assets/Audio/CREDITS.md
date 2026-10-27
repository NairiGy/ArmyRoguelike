# Sound credits

Every clip in `Resources/Sfx` is cut from these sources by `tools/make_sfx.sh`. The raw downloads live
outside the repo, in `Desktop/sound sources/`. The recordings below are from [Freesound](https://freesound.org) under
**CC0 1.0** (public domain: commercial use allowed, no credit required; credited anyway).

| Clip | Source | Author | Licence |
|---|---|---|---|
| `moon_cast` | [Magic - Cymbal Swell 2](https://freesound.org/people/Vrymaa/sounds/826131/) | Vrymaa | CC0 |
| `sun_charge_1` | [Reverse Cymbal: Reverb + long delay](https://freesound.org/people/mycoolclue/sounds/130387/) | mycoolclue | CC0 |
| `sun_impact` | [Powerful impact](https://freesound.org/people/nicorico_120/sounds/855910/) | nicorico_120 | CC0 |
| `sun_impact` | [Subterranean Deep Sub-Bass Rumble Drop](https://freesound.org/people/brktkrgll/sounds/856174/) | brktkrgll | CC0 |
| `sun_impact` | [Flamethrower](https://freesound.org/people/SamsterBirdies/sounds/490166/) | SamsterBirdies | CC0 |
| `sun_sizzle` | [Flame Steaming Denoised 01](https://freesound.org/people/loganzsound/sounds/872946/) | loganzsound | CC0 |
| `moon_absorb_1`, `moon_shatter` | [ANI - Wine Glass - Hard](https://freesound.org/people/ani_music/sounds/198401/) | ani_music | CC0 |
| `moon_absorb_2` | [OWI_Crystal class ping](https://freesound.org/people/matthewHoldenSound/sounds/542561/) | matthewHoldenSound | CC0 |
| `moon_shatter` | [Glass Break](https://freesound.org/people/unfa/sounds/221528/) | unfa | CC0 |
| `moon_fade` | [Magic Whoosh](https://freesound.org/people/DustyWind/sounds/715784/) | DustyWind | CC0 |
| `lightning_strike` | [Thunderstorm lightning strike](https://freesound.org/people/foad/sounds/243614/) | foad | CC0 |
| `lightning_rumble` | [210914 Thunder, crack, pretty close](https://freesound.org/people/TRP/sounds/616992/) | TRP | CC0 |

## Generated with ElevenLabs Sound Effects

Generated on the free plan on 2026-10-27 (model v3, prompt influence 30%; 2.0 s unless noted). **Before release:** the free plan requires
crediting elevenlabs.io for commercial use; either keep the credit below in the game's credits or generate on a paid
plan (no attribution needed). Check ElevenLabs' terms again at that point.

| Clip | Prompt | Variation |
|---|---|---|
| `sun_mark_1` | "An ancient stone sigil igniting on the ground: a warm whoosh of fire catching, with a low resonant hum underneath. Heavy and mystical, not sci-fi." | #2 |
| `sun_mark_2` | "A warm golden glow blooming on an old stone floor: a soft breath of rising heat, crackling embers and a deep warm drone. Ancient sun magic, slow and heavy." | #2 |
| `sun_charge_2`-`_5` | "A short surge of fire and heat gathering power, rising quickly to its loudest point at the very end, like a sun beam about to strike. Warm, heavy, ancient magic, not sci-fi." (0.7 s) | E1-E4 |
| `sun_charge_6`-`_9` | "A quick rising whoosh of flame being drawn in and building intensity, ending abruptly at its peak. Warm and powerful, no electronic sounds." (0.7 s) | F1-F4 |
| `sun_charge_10`-`_12` | "Ancient sun magic charging up: a warm resonant hum swelling fast with crackling heat, cut off at its loudest moment. Heavy and mystical." (0.7 s) | G1, G3, G4 (G2 dropped) |

Sound effects generated with ElevenLabs (elevenlabs.io).

### Units (stone statues, every unit)

Generated on 2026-10-27, 0.7 s (dying: 2.0 s). Which generations are used, and in what order, is listed per sound in
`tools/make_sfx.sh` (near-silent generations are skipped). After the first audition: attack uses A2 A3 A4 B3 C2, die A3 B3 C1 C2, steps A4 C1 D1-D4 E1 E2 E4, hurt replaced by the third round below (G-I).

| Sound | Prompt A | Prompt B | Prompt C |
|---|---|---|---|
| `unit_attack_*` | "A heavy stone fist striking a stone statue: a deep, solid thud of rock on rock with a short crack. Weighty, no metal, no whoosh." | "A stone club smashing into a stone figure: a heavy, dull impact with a gravel crunch. Close, punchy, ancient." | "Two granite blocks slamming together hard: one sharp, heavy hit with a short rocky tail." (only C2 usable) |
| `unit_hurt_*` | "Stone cracking under a blow: a sharp crack and small stone chips scattering on a stone floor." | "A stone statue being damaged: brittle rock fracturing with a gritty crunch and pebbles falling." | "Marble chipping: a quick, hard crack followed by tiny fragments skittering away." (only C1 usable) |
| `unit_die_*` | "A stone statue crumbling and collapsing into rubble: cracking, then heavy chunks of rock falling and settling in dust." | "A large stone figure breaking apart: a deep crack, then a rumble of falling stones and gravel." | "An ancient statue shattering and sinking: grinding stone, a collapse of rocks, a soft settling of dust." |
| `unit_step_*` | "A single heavy stone footstep on a stone floor: a dull, weighty thud with a little grit." | "A stone statue taking one step on stone tiles: a heavy, low clunk with a scrape of gravel." | "One heavy rock being set down on a stone slab: a short, solid thump, close and dry." (C3 near-silent) |

Second round, after the first listen (steps: "a heavy stone object with no holes moving on a stone surface";
hurt: "more like stone crumbling"):

| Sound | Prompt D | Prompt E | Prompt F |
|---|---|---|---|
| `unit_step_*` | "A solid, heavy block of stone shifted one step across a stone floor: a dense, low, dry thud with a brief grinding scrape. No hollow ring, not a person's footstep." | "A massive solid granite statue lifted slightly and set down on a stone floor: one deep, dull, dead thump. No resonance, no echo." | "A heavy stone pedestal sliding a short distance on stone and settling: a short grinding scrape ending in a low, solid knock." |
| `unit_hurt_*` | "A chunk of stone breaking off a statue: crumbling rock, gritty and dry, small pieces falling onto a stone floor. Short." | "Stone crumbling under an impact: a gravelly crunch and a trickle of grit and pebbles. No sharp crack, no metal." | "Sandstone crumbling: dry crumbling and grinding of stone, a few small fragments falling. Close and short." |

Third round for hurt (the user: a knock, not stone coming off; shorter and clearly smaller than dying; fast and simple),
0.5 s. H2, I2 and I4 came out near-silent; G1 and H3 were dropped in the audition. Kept: G2-G4, H1, H4, I1, I3.

| Sound | Prompt G | Prompt H | Prompt I |
|---|---|---|---|
| `unit_hurt_*` | "A single short knock on a solid stone statue: one dull, dry thud of stone being struck. Fast and simple, no echo, no crumbling." | "A quick hard tap of rock against a solid stone block: one short, dense knock. Clean, no tail, no debris." | "A heavy stone statue being struck once: a brief, low, solid knock. Simple, close, dry." |
