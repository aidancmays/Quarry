# Quarry

Parts for the things players fight in co-op games on Netcode for GameObjects: health, being knocked about by hits,
a health bar over the head, and senses that sneaking players can get closer to.

## Requirements

- Unity 6.3 (6000.3) or newer
- Netcode for GameObjects 2.7+, uGUI
- **Gameplay Tags** (`com.aidancmays.gameplay-tags`)
- Optional: **Cleave** 0.8+ (`com.aidancmays.cleave`) turns on the `Quarry.Cleave` adapter.

## Pieces

| Piece | What it does |
|---|---|
| **Health** | Server-authoritative amount (synced to everyone). `ServerApply(DamageInfo)` spends it; `Damaged` (server), `Changed`, `Died`, `Revived` (every machine). Holds its Dead Tag while dead and takes no more damage. Also heal, revive, set max, kill. Works unspawned (offline). |
| **Damage Info** | One lot of damage: amount, the hit's own knockback, damage type (a Gameplay Tag), point, direction, source. |
| **Knockback** | Server: every hit (from the Health next to it, or `Apply`) pushes and staggers it. A non-kinematic Rigidbody gets a velocity change; anything else reads `Velocity` (dies away by Damping) and adds it to its own movement, and pauses its AI while `IsStaggered`. `Block(normal)` takes away push going into a wall. |
| **Knockback Profile** (Create > Quarry > Knockback Profile) | How a kind of creature takes hits: extra impulse per point of damage, base stagger, stagger per damage, max stagger, and knockback and stagger multipliers per damage type. A type counts as its parents, so an entry for `Damage` covers every type without its own. |
| **Health Bar** | A bar over the Anchor, drawn on a shared screen overlay, smaller with distance. Hidden until hurt, shows for Show Time after each hit, then fades; hidden when dead, far away or behind something. Give it a Bar Prefab with an `IHealthBarView` for your own look, or it draws a plain bar. |
| **Senses** | Notices targets within Radius, times each target's `INoiseSource.NoiseMultiplier` (1 if it has none), within a height, optionally in sight. `Notices`, `Nearest`, `RadiusFor`. Draws its radius (and preview multiples) in the editor. |
| **INoiseSource** | On a target: how far off it can be noticed, as a multiple of a sense's radius. Your game works it out (sprinting louder, crouching quieter). |
| **Cleave Damage** (`Quarry.Cleave`) | Next to a Cleave Hittable: each swing that lands goes to the Health (and Knockback) with the weapon's damage type, and swings pass through it once dead. |

## How hard a hit pushes

```
impulse = (hit knockback + damage x Damage To Knockback) x type knockback multiplier
speed   = impulse / Poise                       (no more than Max Speed)
stagger = (Base Stagger + damage x Stagger Per Damage) x type stagger multiplier / Poise   (no more than Max Stagger)
```

## Networking

Health is a NetworkBehaviour: only the server changes it. Knockback runs where the movement is decided (the server,
for server-run AI) and needs no networking of its own; sync the position with a NetworkTransform as usual. Health
Bars are local to each machine.
