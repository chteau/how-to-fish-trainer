# How to Fish — Trainer

A trainer for **How to Fish** (Unity/Mono). Patches the game's DLL, gives you an in-game menu on **F7**.

Aimbot with real ballistics, MLG trick shots, ESP, casino rigging, and a bunch of dumb fun stuff.

> Made for messing around in **private lobbies with your friends**. Don't be a dick in public games.

![tabs](https://img.shields.io/badge/tabs-8-blue) ![hooks](https://img.shields.io/badge/injected%20calls-7-green) ![deps](https://img.shields.io/badge/runtime%20deps-0-brightgreen)

## Build

Needs the .NET 8 SDK. That's it.

```bash
./build.sh
```

Grabs the game's DLLs into `lib/`, builds, drops everything in `dist/`.

## Use

Close the game first.

```bash
./dist/htf patch      # inject
./dist/htf status     # am i patched?
./dist/htf unpatch    # put it back exactly how it was
```

Game somewhere else? `--game /path/to/How to Fish`.

Then launch, hit **F7**. Everything's off by default, turn on what you want. Settings save automatically.

**Steam wipes the patch** whenever it verifies or updates files. Just run `patch` again.

## What's in it

**Aim** — aimbot that only kicks in when you're ADS with a gun. Actually leads moving targets: solves
the interception point from the weapon's real muzzle velocity + gravity. Filter by sea creatures /
seagulls / bosses. Auto-fire holds off when a friend walks into your line.

**MLG** — one keybind (**V**) does the whole trick shot: drops ADS, jumps, spins a 360, snaps onto the
predicted spot, fires. Stacks the game's own bonuses (360, no-scope, longshot, headshot, aerial,
dogfight, last bullet) and tells you what it scored. Can target players too if you want to be a menace.

**Weapon** — unlimited ammo, no reload, no recoil, no spread, fire rate, damage, punch damage.

**Move** — speed, jump height, infinite jump.

**ESP** — CS2-style boxes off the real hitboxes. Creatures, loose loot, players, boss HP + timer.
Gold = drip, red = boss, cyan = bird, green = whatever you're locked onto.

**World** — instant bite, max fish weight, perfect cook (money printer), drip odds, unlimited health/food.

**Casino** — always win roulette (bet green, 35x), rig the slot machine, free money.

**Fun** — headshot everyone at once, launch everyone into orbit, kill all creatures, kill the boss,
god mode, one-shot mode, island warp, chat, and the game's own dev console.

## Host only vs not

Some stuff is server-side, so it only works when **you're hosting**:

- unlimited health / food, drip odds, instant bite, fish weight, perfect cook
- casino rigging, free money, god mode, one-shot, island warp, dev console

Everything else works fine as a client, because the game trusts the shooter's machine for damage —
aimbot, MLG, ESP, weapon damage, recoil, punch, movement, killing creatures, and yeeting players
(that last one needs friendly fire on, which the host toggles).

## How it works

Patching adds **7 calls** to `Assembly-CSharp.dll`, nothing else:

| where | what |
|---|---|
| `GameInfo.Awake` | boot the trainer |
| `PlayerAimAssist.GetRotationDelta` | aimbot steers through the game's own aim-assist channel |
| `Attachments.Damage` | damage multiplier |
| `Player.BlockInputs` | stops menu clicks firing your gun |
| `PlayerCamera.Recoil` | no recoil |
| `CasinoManager.ServerRouletteResult` | rig the roulette |
| `KillScoreCalculator.GetMultiplier` | override score multiplier |

No Harmony, no BepInEx, no runtime patching lib — the payload is **one DLL** with zero third-party
deps. Unpatch restores the original byte-for-byte (backed up as `.htfbak`).

Couple of things that took some digging:

- Bullets are **real projectiles**, not raycasts, and they leave the **barrel**, not the camera. The
  barrel sits at a small angle from where you're looking, which is nothing at 10m and over a metre at
  100m. Aimbot undoes that offset, which is why long shots actually land.
- Lock tolerance scales with how big the target *looks*. A flat 2.5° is fine up close and a 4m miss at
  100m.
- Fish thrash around, so leading them off raw velocity sends the shot into empty water. It tracks
  ~0.3s of movement and damps the lead when the target's being erratic.
- MLG bonuses are scored **on impact**, not when you fire — so the jump and spin have to still be
  valid when the bullet lands.

## Layout

```
src/HtfTrainer/   the trainer that runs in-game
src/HtfPatcher/   dnlib CLI that injects / backs up / restores
dist/             build output
```

MIT. Not affiliated with the devs.
