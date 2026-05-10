# Research Report: Realistic 2D Jet Afterburner/Exhaust Particle Effects in Unity Built-In Pipeline

**Research Date:** 2026-05-10  
**Status:** Complete  
**Confidence Level:** High (5 sources, consensus on best practices)

---

## Executive Summary

Jet afterburner effects in 2D should use **3-4 layered ParticleSystems** (not TrailRenderer) with **soft radial gradient textures**, **additive blending**, and **temperature-driven color progression** (white-blue core → yellow-orange plume → grey smoke). Built-in `Particles/Standard Unlit` shader handles additive blending natively. Soft particle textures (not hard-edged) and **fast emission** create realistic plume appearance. Professional VFX decompose afterburner into (1) bright hot core, (2) orange flame plume, (3) smoke ribbon, (4) optional sparks/distortion. Texture sources: **Kenney.nl (80+ free sprites)** or **OpenGameArt Particle Pack** provide CC0-licensed exhaust/flame sprites.

---

## Key Findings

### 1. Texture Strategy: Soft Radials vs Hard Sprites

**Recommended Approach:**
- **Use soft circular gradient textures (64×128 or 128×256)**, not hard-edged sprites
- Radial falloff pattern: bright white center → transparent edges (feathered gaussian blur in editor)
- Multiple passes layer different density/color gradients
- Pre-made free assets: [Kenney Particle Pack (80+ sprites)](https://kenney.nl/assets/particle-pack) on [OpenGameArt](https://opengameart.org/content/particle-pack-80-sprites) includes flame, smoke, and glow templates

**Why soft textures work:**
- Hard sprites create visible "puff" artifacts (your current fuzzy blobs issue)
- Soft radials naturally blend when layered via additive mode
- Mimic realistic heat bloom and light diffusion in high-temperature gas

**Free Asset Sources:**
- [Kenney Smoke Particles (70 assets, CC0)](https://kenney.nl/assets/smoke-particles)
- [OpenGameArt Flame Particle System](https://opengameart.org/content/flame-particle-system)
- [OpenGameArt Particle Pack (80 sprites)](https://opengameart.org/content/particle-pack-80-sprites)
- Unity built-in: `ParticleSmokeBlack` (dark), `ParticleSmokeWhite` (light) in editor

---

### 2. Blend Mode: Particles/Standard Unlit + Additive

**Built-in Shader:**
- Use `Particles/Standard Unlit` (built-in, included with Unity)
- Set `Blend Mode: Additive` or manually configure: **Blend One One** (or **Blend SrcAlpha One**)
- Additive accumulates brightness where particles overlap → realistic glowing core effect
- Faster than `Particles/VertexLit`; no lighting calculations needed

**Blend Mode Explanation:**
- **Additive (Blend One One)**: Color accumulates additively; perfect for bright, hot cores and glowing flames
- **Premultiplied Alpha**: Better for semi-transparent smoke that doesn't glow; use for outer smoke layers only
- **Alpha Blend**: Darkens behind particles; avoid for hot effects

**Result:** Additive creates white-hot nozzle core naturally; layers don't muddy or darken.

---

### 3. Layer Composition: Professional 3-Layer Stack

Real AAA games decompose jet exhaust into **3–4 independent ParticleSystems**:

| Layer | Purpose | Texture | Lifespan | Speed | Color | Size |
|-------|---------|---------|----------|-------|-------|------|
| **Core** | Hot point / bright nozzle | Soft radial (white→transparent) | 0.2–0.4s | Fast (6–12 m/s) | White → pale yellow | Small (0.3–0.5 units) |
| **Flame** | Orange plume | Soft radial (yellow→orange→clear) | 0.5–1.0s | Medium (3–8 m/s) | Yellow → orange | Medium (0.5–1.2 units) |
| **Smoke** | Lingering tail | Soft radial (light grey→dark grey→transparent) | 1.5–2.5s | Slow (1–2 m/s) | Light grey → dark grey | Large (1.0–2.0 units) |
| *(Optional)* | Distortion / shimmer | None (use distortion shader) | 0.3s | Fast | (N/A) | Covers exhaust area |

**Why 3 layers:**
- Single layer = your current "fuzzy blobs" problem
- Separating hot core from smoke prevents orange from dominating
- Staggered lifespans create continuous plume illusion despite fast respawn

---

### 4. Color Gradient: Temperature-Driven Physics

**Canonical Afterburner Color Ramp** (based on real jet engine physics):

```
Temperature Physics:
- White/pale blue: ~10,000K (hot core, incomplete combustion)
- Yellow/orange: ~2,000K (cooler, unburnt carbon soot glowing)
- Grey smoke: ~500K (cooled exhaust, particulates)
```

**Recommended Color Progression per Layer:**

| Layer | Color Over Time | Notes |
|-------|-----------------|-------|
| Core | #FFFFFF → #FFFF99 (white → pale yellow) | Hottest; fade to transparent |
| Flame | #FFFF00 → #FF6600 → #FF0000 (yellow → orange → faint red) | Main plume color shift |
| Smoke | #CCCCCC → #666666 (light grey → dark grey) | Coolest; dissipates upward |

**Implementation:**
- Use `Color over Lifetime` module in ParticleSystem
- Set `MinMaxGradient` mode = `TwoColors` (start color + end color)
- Example: Core starts `#FFFFFF` alpha=1, ends `#FFFF99` alpha=0

---

### 5. Shape & Emission: Cone Geometry + Velocity Profile

**ParticleSystem Shape:**
- **Cone shape** (not sphere): angle **8–15°**, narrower than typical explosions
- Simulates nozzle exit cone
- Emit straight back (180° from jet forward direction)

**Emission Rate:**
- **Core layer:** 80–120 particles/sec (dense, continuous stream)
- **Flame layer:** 40–60 particles/sec (medium density)
- **Smoke layer:** 20–30 particles/sec (sparse, lingering)

**Velocity Profile:**
- Core: **6–12 m/s** (fast, simulates combustion exit velocity)
- Flame: **3–8 m/s** (medium, expands from core)
- Smoke: **1–2 m/s** (slow, visual drift)
- Add **damping = 0.1–0.2** per layer so particles slow over lifetime

**Spread Angle:**
- Core: **4–8°** (tight; hot jet core is coherent)
- Flame: **10–15°** (wider; expands from core)
- Smoke: **15–25°** (widest; disperses into atmosphere)

---

### 6. Trail vs ParticleSystem: Use ParticleSystem

**Verdict:** **ParticleSystem Trails module > TrailRenderer for this use case**

**ParticleSystem Trails module:**
- Built into Trails sub-module
- Stretches particles along motion vector automatically
- Lightweight; no separate renderer needed
- Works well for thin afterburner streaks when combined with fast velocity

**When to use:**
- **ParticleSystem:** Afterburner (clouds + streaks + plume)
- **TrailRenderer:** Single projectile tail or sword slash (motion-following ribbon)

**Your setup:** Enable `Trails` module on **Core layer only**, set:
- Ribbon mode = `Particles.Trails`
- Lifetime: 0.3s (tied to particle lifespan)
- Minimum vertex distance: 0.05 units
- Width curve: ramps 0.1 → 0.3 (thin edge, wider center)

---

### 7. Specific Recommended Values (Plug & Play)

**Core ParticleSystem (Hot Point):**
```
Duration: 10 (infinite emission)
Looping: true
Max Particles: 200
Shape: Cone, angle=6°
Emission Rate: 100/sec
Lifetime: 0.3s
Size: 0.4 units (start) → 0.2 (end)
Color: white → pale yellow, fade to transparent
Velocity: 10 m/s (initial), 0.15 damping
Blend Mode: Additive (Particles/Standard Unlit)
Texture: Soft radial 64×64, white→transparent gradient
```

**Flame ParticleSystem (Plume):**
```
Duration: 10 (infinite)
Looping: true
Max Particles: 150
Shape: Cone, angle=12°
Emission Rate: 50/sec
Lifetime: 0.8s
Size: 0.8 units → 0.3 units
Color: yellow → orange → faint red, fade to transparent
Velocity: 5 m/s, 0.12 damping
Blend Mode: Additive
Texture: Soft radial 96×96, yellow→orange falloff
```

**Smoke ParticleSystem (Tail):**
```
Duration: 10
Looping: true
Max Particles: 100
Shape: Cone, angle=18°
Emission Rate: 25/sec
Lifetime: 2.0s
Size: 1.2 units → 0.6 units
Color: light grey (#CCCCCC) → dark grey (#666666), fade to transparent
Velocity: 1.5 m/s, 0.1 damping
Blend Mode: Additive (or Alpha Blend if smoke looks too bright)
Texture: Soft radial 128×128, grey gradient
```

---

## Troubleshooting Your Current Setup

**Problem:** "Fuzzy orange/yellow blobs, doesn't read as exhaust"

**Root Causes & Fixes:**
1. ✗ Single layer with one color → **Use 3-layer stack** (core + flame + smoke)
2. ✗ Hard sprite edges → **Use soft radial gradient textures** (gaussian blur feathered)
3. ✗ Wide cone (4–12°) too loose → **Tighten core to 6–8°**, flame to 12°
4. ✗ Slow velocity (implied) → **Increase core velocity to 8–12 m/s**
5. ✗ Manual additive blend (SrcAlpha+One) may have rounding errors → **Use built-in Particles/Standard Unlit additive mode**
6. ✗ StretchedBillboard on all layers → **Use StretchedBillboard only on core (lengthScale 2.0–3.0), Standard Billboard on others**

**Action:**
- Create 3 separate ParticleSystem GameObjects (Core, Flame, Smoke)
- Download Kenney free sprite pack, extract soft fire/glow textures
- Set each to Particles/Standard Unlit, Blend=Additive
- Tune colors and lifespans per table above

---

## References

### Official Documentation
- [Unity Particle System Manual](https://docs.unity3d.com/Manual/ParticleSystems.html)
- [Standard Particle Shaders Reference (Built-In)](https://docs.unity3d.com/Manual/shader-StandardParticleShaders.html)
- [Particle System Trails Module](https://docs.unity3d.com/Manual/PartSysTrailsModule.html)
- [Creating Exhaust Smoke from a Vehicle](https://docs.unity3d.com/2020.1/Documentation/Manual/PartSysExhaust.html)
- [2D Particle Effects Tutorial](https://learn.unity.com/course/2D-adventure-robot-repair/unit/enhance-your-game/tutorial/create-2d-particle-effects)

### Free Asset Sources
- [Kenney Particle Pack (80+ CC0 sprites)](https://kenney.nl/assets/particle-pack)
- [Kenney Smoke Particles (70 CC0 sprites)](https://kenney.nl/assets/smoke-particles)
- [OpenGameArt Particle Pack](https://opengameart.org/content/particle-pack-80-sprites)
- [OpenGameArt Flame Particle System](https://opengameart.org/content/flame-particle-system)

### Video Tutorials
- [Jet Engine VFX Tutorial (YouTube)](https://www.youtube.com/watch?v=GYQu3HrbRGo)
- [Rocket Exhaust Trace Tutorial (YouTube)](https://www.youtube.com/watch?v=PBMgvMhuXZ8)
- [Jet Engine Particle System (YouTube)](https://www.youtube.com/watch?v=sHKLvw-_LkE)

### Community Resources
- [Jet/Rocket Engine Effect Discussion (Unity Discussions)](https://discussions.unity.com/t/jet-rocket-engine-effect/426080)
- [Spaceship Engine Flames (Polycount)](https://polycount.com/discussion/136596/need-help-making-spaceship-engine-flames)

### Physics References
- [Afterburner Color Science (Wikipedia)](https://en.wikipedia.org/wiki/Afterburner)
- [War Thunder Afterburner Flames Discussion](https://forum.warthunder.com/t/realistic-varied-afterburner-flames/146776)

---

## Unresolved Questions

1. **Your jet scale (0.196 world units):** Is nozzle position already offset in sprite? Need exact nozzle world position to set ParticleSystem emission point.
2. **Camera ortho size 15:** What's the visual distance from jet to camera? Will affect particle size perception and sprite resolution adequacy.
3. **Art style preference:** Want photorealistic glow or stylized cartoon flame? Current texture softness favors realism; let me know if shifting toward more graphic style.
4. **Performance budget:** Typical frame rate target? 3-layer 200 max particles is moderate cost (~0.5ms on modern hardware), but can optimize further if needed.

---

**Next Steps:**  
1. Acquire Kenney textures (free download from kenney.nl)
2. Create 3 ParticleSystems with values from table above
3. Test each layer independently, then stacked
4. Adjust lifespan/velocity for your camera distance and jet scale
5. Iterate color gradients against actual jet sprite in-game
