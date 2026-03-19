# ZSlayer SPT Zombies

![Version](https://img.shields.io/badge/Version-v1.1.0-c8aa6e?style=flat-square)
![SPT](https://img.shields.io/badge/SPT-~4.0.x-blue?style=flat-square)
![BigBrain](https://img.shields.io/badge/BigBrain-Required-red?style=flat-square)
![SAIN](https://img.shields.io/badge/SAIN-Optional-green?style=flat-square)
![Command Center](https://img.shields.io/badge/Command_Center-Optional-green?style=flat-square)

A comprehensive zombie overhaul for SPT 4.0 / FIKA. Replaces vanilla zombie behavior with rich, varied archetypes — shamblers that stumble through doorways, runners that sprint in terrifying bursts, crawlers that hug walls, stalkers that flank from behind, and berserkers that never stop coming.

**Two components work together:**
- **Server mod** — controls infection rates, spawn weights, bot caps, AI difficulty, health pools, boss injection, and wave escalation
- **Client plugin** — replaces the vanilla zombie brain with BigBrain-powered behavior archetypes, horde coordination, custom audio, and optional SAIN integration

---

## How It Works

### The Infection System

Every map has an **infection percentage** (0-100%) that controls how many normal bot spawns are replaced with zombies. Labs and Factory default to 100% — pure zombie apocalypse. Customs sits at 75%, creating a tense mix of PMCs, scavs, and the undead. Woods and Reserve at 60% keep you guessing what's around the next corner.

The server mod intercepts SPT's bot spawning pipeline and converts the configured percentage of normal spawns into infected types. It handles the math of translating "75% infection on Customs" into actual spawn weight adjustments across easy/normal/hard difficulty tiers.

### The Brain Replacement

Vanilla EFT zombies are boring. They have exactly three modes:
- **Slow** — shamble toward you in a zigzag
- **Fast** — sprint toward you in a zigzag
- **Shooting** — stand there and shoot (yes, zombies with guns)

All three modes do the same thing: walk at you, melee at 4 meters. No variety, no surprise, no fear.

ZSlayer SPT Zombies replaces the vanilla zombie brain entirely using **BigBrain**. When an infected bot spawns, the plugin:
1. Detects it via WildSpawnType (infectedAssault, infectedPmc, infectedCivil, infectedLaborant, infectedTagilla)
2. Assigns a **behavior archetype** based on weighted random selection (deterministic per ProfileId for FIKA sync)
3. Registers custom BigBrain layers that override vanilla decision-making
4. Controls movement, aggression, and vocalizations through archetype-specific logic

---

## Zombie Archetypes

Each zombie is assigned one archetype at spawn. The archetype determines how it moves, how it hunts, and how dangerous it is.

### Shambler (40% default weight)

The classic undead. Shamblers are slow, erratic, and individually manageable — but they're everywhere.

**Movement:** Speed varies between 0.3-0.5 (walking pace). They don't move in straight lines — a random lateral offset is applied to their pathing every 0.5 seconds, creating the distinctive shambling gait. They stumble periodically (every 3-8 seconds), nearly stopping for 0.5-1.5 seconds before resuming their approach.

**Combat:** Melee only. They force-equip melee weapons on engagement. At distances over 4 meters they approach with erratic offset. Under 4 meters they speed up slightly and move directly toward the target for a melee strike.

**The danger:** One shambler is nothing. Ten shamblers coming from three directions while you're reloading is a death sentence. Their stumble pauses create false confidence — you think they're stuck, you turn your back, and suddenly one is right behind you.

**Vocalization:** Low-frequency groaning during pursuit. Ambient moaning while idle.

---

### Runner (25% default weight)

The nightmare. Runners use a sprint-burst / recovery cycle that makes them unpredictable and terrifying.

**Movement:** During sprint phase (3.5-6 seconds), they move at maximum speed with sprint animation. During recovery phase (2.5-4 seconds), they slow to 0.6 speed and catch their breath. The cycle repeats indefinitely. Path updates happen faster during sprints (every 0.3s vs 0.5s for other archetypes) for tighter tracking.

**Combat:** Melee focused. They stop sprinting at close range (<3m) and switch to direct melee approach at 0.8 speed. The sudden deceleration from sprint to melee strike is jarring and hard to react to.

**The danger:** You hear them before you see them — heavy breathing, then footsteps getting louder, then a screaming sprint around the corner. The recovery pause gives you a window to shoot, but if you miss, the next burst closes the gap before you can reload. In groups, their sprint/recovery cycles desynchronize, creating waves of pressure.

**Vocalization:** Triple the normal rate during sprints. Screaming, snarling pursuit sounds.

---

### Crawler (10% default weight)

Low-profile ambush predators. Crawlers are slow but hard to spot and harder to headshot.

**Movement:** Speed 0.3-0.4, crouched/prone pose. They approach close to walls and cover, staying low. Their reduced profile makes them easy to miss in dark environments or when you're focused on standing threats.

**Combat:** Extremely close melee range (2m). They need to be practically on top of you to attack, but by then it's often too late.

**The danger:** You're scanning a hallway for standing zombies, clear it, step forward — and one grabs your ankle from behind a doorframe. Labs' dark corridors and Factory's tight spaces are their playground. They're the reason you always check your feet.

**Special spawn rules:** infectedLaborant (lab zombies) have a 60% chance to spawn as Crawlers instead of the normal weighted pool, reflecting scientists who were caught low and never got back up.

---

### Stalker (15% default weight)

The intelligent hunter. Stalkers don't charge — they follow, wait, and strike when you're vulnerable.

**Movement:** Maintains 15-25 meters distance from the target. Approaches from flanking angles rather than directly. Speed 0.5-0.7, rising to 1.0 during the final rush.

**Combat:** Waits for the target to look away or engage another threat, then rushes from behind. The rush is fast (1.0 speed) and committed — once they start, they don't stop.

**The danger:** You just killed three shamblers and you're looting. You didn't notice the stalker that's been pacing you for 30 seconds, staying just outside your peripheral vision. It rushes while you're in your inventory. Stalkers punish tunnel vision and reward situational awareness.

**Vocalization:** Near-silent during stalking phase. Sudden vocalization burst on rush — by the time you hear it, it's too late to turn.

---

### Berserker (10% default weight)

The juggernaut. Berserkers are pure aggression — maximum speed, direct path, no pauses, no recovery.

**Movement:** Constant 0.9-1.0 speed. No stumble mechanic. No sprint/recovery cycle. Just relentless forward momentum. Direct pathing to the target with no erratic offset.

**Combat:** Close melee range (3m). They arrive fast and hit hard. No hesitation, no pause between approaches.

**The danger:** A berserker in the open is a target practice problem — one headshot and it's done. A berserker around a corner is 200 pounds of rage closing a 5-meter gap before your barrel comes up. They're the reason you don't sprint through doorways on infected maps.

**Special spawn rules:** infectedTagilla is always a Berserker — the zombie boss charges with the same relentless fury as his living counterpart, but with zombie-tier health pools (130 head, 450 chest).

**Dynamic triggering:** Any zombie that loses more than 50% HP in 3 seconds temporarily enters Berserker mode regardless of original archetype. Wound a shambler without killing it, and it stops shambling.

---

## Horde System

Zombies don't just wander independently. The horde system creates emergent group behavior through three mechanisms:

### Alert Propagation

Zombies exist in one of three alert states:

```
Unaware  -->  Alerted  -->  Aggressive
  (idle)    (investigate)    (full pursuit)
```

- **Unaware:** Idle wandering. The zombie shuffles aimlessly within a 15-meter patrol radius, occasionally groaning. NavMesh-sampled waypoints ensure they stay on walkable surfaces. Random pauses at destinations (3-8 seconds) create natural idle behavior.

- **Alerted:** A nearby zombie detected a threat (saw a player, heard gunfire, or received an alert call from another zombie). The alerted zombie moves toward the sound source with scanning head movement. Alert radius expands over time — one gunshot can ripple through an entire building.

- **Aggressive:** Direct pursuit. The zombie has line-of-sight on a target or received an aggressive call from a group member. Full combat behavior based on archetype.

### Alpha Zombie

Every zombie group (spatial cluster within detection radius) elects an **alpha** — the zombie with the highest current HP, or the first to reach Aggressive state. The alpha:

- Broadcasts target position to all group members
- Assigns offset positions for coordinated approach: 2-3 from the front, 1-2 from the sides, 1 from behind (groups of 6+)
- Creates flanking pressure even with "simple" shamblers

When the alpha dies, the nearest zombie inherits the role. Kill the leader, and the group briefly disorganizes before a new alpha coordinates the next push.

### Horde Rush

**Trigger:** Alpha is Aggressive + 4 or more zombies in the group + target within 30 meters.

All zombies in the group simultaneously switch to maximum speed and direct approach for 10 seconds. Group vocalization intensifies — the audio feedback tells you something just changed before you see the wave hit.

A horde rush is survivable with preparation (chokepoint, full magazine, grenades). Without preparation, it's a sprint-or-die moment.

---

## Audio Architecture

The plugin includes a framework for custom zombie audio that replaces or supplements vanilla voicelines.

### How It Works

A Harmony patch intercepts `BotTalk.Say()` for all infected bot types. When a zombie vocalizes:
1. Check if custom audio clips exist for the situation
2. If yes: play custom clip via 3D spatial AudioSource (proper distance falloff, directional audio)
3. If no: pass through to vanilla voiceline system

### Situational Audio

| Trigger | Category | When |
|---------|----------|------|
| OnEnemyConversation | idle / chase | Depends on alert state |
| OnFight | attack | Melee initiated |
| OnDeath | death | Zombie killed |
| OnLostVisual | alert | Lost sight of target |
| OnBreath / OnMutter | idle | Ambient |

### Custom Clip Drop-In

Place `.ogg` or `.wav` files in the audio folder structure:

```
BepInEx/plugins/ZSlayerZombieClient/audio/
  idle/       -- ambient moans, groans, breathing
  alert/      -- alert growls, sniffing, scanning sounds
  chase/      -- pursuit screams, snarling, heavy breathing
  attack/     -- melee roars, impact grunts
  death/      -- death gurgles, final gasps
  horde/      -- group rush overlay sounds
```

Multiple clips per category are supported — random selection per event. Empty folders gracefully fall back to vanilla audio.

### Group Vocalization

Horde audio scales with group size and distance. A single zombie groaning is atmosphere. Six zombies groaning in chorus from different directions is "time to leave."

---

## SAIN Integration

When SAIN is installed, the plugin adds a **fear/morale system** for non-zombie bots near zombie groups.

### Fear Calculation

```
Fear = (nearby zombie count within 50m) * personality multiplier
```

| SAIN Personality | Fear Multiplier | Behavior |
|-----------------|----------------|----------|
| GigaChad | 0.2x | Barely notices zombies |
| Chad | 0.5x | Mild concern |
| Normal | 1.0x | Standard fear response |
| Rat | 1.5x | Elevated anxiety |
| Timmy | 2.0x | Panic-prone |
| Coward | 2.5x | Terror at the slightest groan |

### Fear Effects

- **Low fear:** Normal SAIN behavior — bots fight zombies calmly
- **Medium fear:** More frequent grenade usage, tendency to retreat, less accurate fire
- **High fear:** Panic fire, strong retreat behavior, may flee rather than fight
- **Killing zombies reduces fear** — combat confidence builds with successful engagements

### Isolation Pattern

All SAIN types are confined to the `Sain/` directory and loaded via lazy initialization. This prevents `TypeLoadException` crashes when SAIN is not installed. The client plugin works identically with or without SAIN — SAIN just adds the extra fear dimension for non-zombie bots.

---

## FIKA Compatibility

All archetype assignments are **deterministic per ProfileId**. The same zombie will behave the same way on every connected client because the archetype selection is seeded from a hash of the bot's unique profile identifier. No network sync required — identical inputs produce identical outputs.

---

## Server Configuration

The server mod exposes a full JSON config at `user/mods/ZSlayerZombies/config/config.json`:

### Map Infection Rates
Per-map infection percentage (0-100). Higher = more zombie spawns replacing normal bots.

### Spawn Weights
Control the ratio of zombie types (infectedAssault, infectedPmc, infectedCivil, infectedLaborant) per difficulty tier. The server translates infection percentages into these weights.

### Zombie AI
Per-difficulty tuning: sight range, field of view, hearing sensitivity, aggression chance, reaction time, rotation speed, and shot scattering. These server-side values shape the zombie's sensory capabilities — the client-side archetypes control what they do with that information.

### Zombie Health
Per-type body part HP. Standard zombies get 10 HP head / 180 chest. infectedTagilla gets 130 head / 450 chest. One-tappable heads on standards, tanky bodies that force sustained fire.

### Spawn Control
Bot cap overrides, zone limits, boss zombie injection into crowd attack params, independent zombie wave spawning. ABPS (acidphantasm-botplacementsystem) conflict detection — automatically skips bot cap overrides when ABPS is detected.

### Boss Zombies
infectedTagilla and cursedAssault injection into configured maps with spawn chance, max per raid, and map-specific targeting.

### Advanced Features
Night mode (infection multiplier + forced weather), wave escalation (infection ramps up during raid), difficulty scaling (infection scales with player count or level), raid time extension, loot modifiers.

### HTTP API
All settings are live-adjustable via REST API at `/zslayer/zombies/`:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/config` | GET | Get current config |
| `/config` | POST | Update and apply config |
| `/status` | GET | Get active status |
| `/apply` | POST | Re-apply current config |
| `/reset` | POST | Reset to defaults |

---

## Command Center Integration

When [ZSlayer Command Center](https://github.com/ZSlayerHQ/ZSlayerCommandCenter) is installed on the same server, it **auto-detects** ZSlayer Zombies and adds a dedicated **Zombies** tab to the admin panel. This gives you full browser-based control over every zombie setting without touching config files:

- Adjust per-map infection rates with sliders
- Tune zombie AI parameters (sight, hearing, aggression, reaction time)
- Configure spawn weights, boss zombies, and health pools
- Enable/disable infection effects, night mode, wave escalation
- Adjust loot modifiers and XP rewards
- All changes apply live — no server restart needed

Command Center proxies requests through its own API (`/zslayer/cc/zombies/*`) to the zombie mod's HTTP listener (`/zslayer/zombies/*`), so everything is managed from a single browser tab alongside all your other server settings.

> Command Center is optional — ZSlayer Zombies works perfectly fine standalone with its JSON config file.

---

## Installation

### Requirements
- SPT ~4.0.x
- [BigBrain](https://hub.sp-tarkov.com/files/file/104-bigbrain/) (required)
- [SAIN](https://hub.sp-tarkov.com/files/file/1062-sain-solarint-s-ai-modifications/) (optional, enables fear system)
- [ZSlayer Command Center](https://github.com/ZSlayerHQ/ZSlayerCommandCenter) (optional, enables browser-based config UI)

### Server Mod
Copy `ZSlayerZombies/` folder to `SPT/user/mods/ZSlayerZombies/`

### Client Plugin
Copy `ZSlayerZombieClient/` folder to `BepInEx/plugins/ZSlayerZombieClient/`

### Client Configuration
BepInEx config file generated at first launch: `BepInEx/config/com.zslayerhq.zombieclient.cfg`

Key settings:
- **Archetype weights** — adjust the probability of each zombie type
- **Movement speeds** — fine-tune shambler/runner movement parameters
- **Brain names** — override if obfuscated class names change between SPT versions
- **Debug logging** — enable verbose logging for troubleshooting

---

## Project Structure

```
ZSlayerZombieClient/          -- mono-repo root
|
|-- Server/                   -- SPT server mod (.NET 9.0)
|   |-- ZSlayerZombies.csproj
|   |-- ModMetadata.cs
|   |-- ZSlayerZombiesMod.cs  -- entry point, config I/O
|   |-- ZombieConfig.cs       -- full config model
|   |-- ZombieService.cs      -- core logic (spawn, health, AI, waves)
|   |-- ZombieHttpListener.cs -- REST API
|   +-- config/config.json    -- default config
|
|-- Plugin.cs                 -- client entry point (BepInEx)
|-- ZSlayerZombieClient.csproj
|
|-- Core/
|   |-- ZombieConstants.cs    -- WildSpawnType values, brain names, timing
|   |-- ZombieIdentifier.cs   -- infected bot detection
|   +-- ZombieRegistry.cs     -- runtime bot registry
|
|-- Config/
|   +-- ZombieClientConfig.cs -- BepInEx config entries
|
|-- Archetypes/
|   |-- ZombieArchetype.cs    -- archetype enum + data
|   +-- ArchetypeAssigner.cs  -- weighted random assignment
|
|-- Layers/
|   |-- ZombieMainLayer.cs    -- combat (priority 95)
|   +-- ZombieIdleLayer.cs    -- idle fallback (priority 75)
|
|-- Logic/
|   |-- ShamblerLogic.cs      -- slow, erratic, stumble
|   |-- RunnerLogic.cs        -- sprint burst / recovery
|   +-- IdleWanderLogic.cs    -- ambient wander
|
|-- Patches/
|   +-- BotSpawnPatch.cs      -- Harmony postfix for registration
|
+-- audio/                    -- custom audio clips (future)
    |-- idle/
    |-- alert/
    |-- chase/
    |-- attack/
    |-- death/
    +-- horde/
```

---

## Credits

- **Author:** ZSlayerHQ / Ben Cole
- **BigBrain:** DrakiaXYZ — the custom brain layer framework that makes this possible
- **SAIN:** Solarint — personality system integration for fear mechanics
- **SPT Team** — for the modding platform

---

## License

[CC BY-NC-SA 4.0](LICENSE) — Built by [ZSlayerHQ / Ben Cole](https://github.com/ZSlayerHQ)

---

*"They're not fast. They're not smart. But they don't stop, they don't sleep, and there are always more of them than you have bullets."*
