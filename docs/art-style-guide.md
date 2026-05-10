# Art Style Guide — Rocket Launcher Game

## Vision
3D toy/retro style mobile game. Matte-finish objects with soft shading, like physical plastic toys photographed on clean backgrounds. Chunky, solid, tactile — inspired by vinyl toy collectibles and retro space toys. Pre-rendered 3D sprites for a 2D game.

---

## Color Palette

### Primary Colors
| Name | Hex | Usage |
|------|-----|-------|
| Sky Blue | `#4FC3F7` | Sky background, UI accents |
| Grass Green | `#66BB6A` | Ground, environment |
| Rocket Red | `#D32F2F` | Rocket body, primary game object |
| Matte Red | `#C62828` | Rocket shading, darker accents |
| Earth Brown | `#8D6E63` | Ground dirt, wood textures |

### Accent Colors
| Name | Hex | Usage |
|------|-----|-------|
| Sunny Yellow | `#FFEE58` | Stars, win effects, target glow |
| Cloud White | `#FAFAFA` | Clouds, smoke, text |
| Night Navy | `#1A237E` | Shadows, dark accents |
| Soft Purple | `#AB47BC` | UI highlights, special effects |
| Warm Orange | `#FFA726` | Flame/exhaust, explosion highlights |

### Rules
- No outlines on 3D-rendered objects — rely on form and shading for definition
- Shadows use soft ambient occlusion, not hard drop shadows
- No pure black (`#000000`) — use Navy or dark grey instead
- No pure white for objects — use Cloud White or tinted whites
- Matte finish on all objects — no glossy/specular highlights (subtle highlight allowed on curved surfaces only)

---

## Art Style Rules

### Surface Treatment
- **Matte/soft-touch finish** on all game objects — like vinyl or painted plastic
- Subtle surface texture (very fine grain) to avoid looking too CG-perfect
- Smooth, clean forms — no scratches, dirt, or weathering

### Shading
- **Soft 3D shading** — smooth gradients from light to shadow
- Light source: top-left (consistent across all assets)
- Subtle ambient occlusion where parts meet (e.g., fins meeting rocket body)
- No cel shading — use realistic-ish soft light falloff

### Proportions
- Slightly exaggerated/chunky — objects feel solid, weighty, toy-like
- Rocket: ~3:1 height-to-width ratio
- Rounded edges everywhere — no sharp geometric corners
- Target: bigger than realistic — easy to see on mobile

### 3D Rendering Style
- Pre-rendered 3D sprites exported as 2D PNG
- Consistent camera angle across all objects (slight 3/4 top-down for ground objects, front view for rocket)
- Soft studio lighting — no harsh directional shadows
- Clean transparent backgrounds for sprites

---

## Typography

### Primary Font
**Luckiest Guy** (Google Fonts, free)
- Usage: Titles, win text, round numbers
- Style: Bold, chunky, cartoon
- Color: White with soft shadow (no hard outline)

### Secondary Font
**Bubblegum Sans** (Google Fonts, free)
- Usage: HUD stats, button labels, hints
- Style: Rounded, friendly, readable
- Color: Dark navy or white depending on background

### Text Rules
- All game text has soft drop shadow for readability
- Minimum size: 32pt on 1080p reference (readable on mobile)
- Numbers always use Primary Font (more impactful)
- No hard outlines on text — use shadow/glow instead

---

## Game Objects Style

### Rocket
- Classic retro toy rocket shape — cylindrical body, cone nose, 3 fins at base
- **Entire body: Rocket Red (#D32F2F)** — single color, monochrome
- Matte finish with subtle shading from ambient light
- Nose cone: same red, separated by a subtle groove/seam line
- Fins: same red, thick and chunky, slightly swept back
- Nozzle at base: darker red/grey cylinder
- No window/porthole — clean, minimal toy design
- Flame/exhaust: Yellow-orange soft glow (when flying)
- Reference: retro tin rocket toys, vinyl collectible figures

### Target
- Bullseye design — concentric circles (red/white/red)
- 3D rendered — looks like a physical target board
- Mounted on wooden post/stick (matte wood texture)
- Subtle glow effect (Sunny Yellow) to stand out

### Launcher/Slingshot
- Wooden Y-frame slingshot — chunky, toy-like wood
- Matte wood finish with subtle grain
- Elastic band: bright red rubber, slightly glossy
- Base: sits on ground naturally

### Obstacles
- Floating clouds (soft, puffy, 3D-rendered cotton look)
- Birds (small, simple, toy-like)
- Balloons (bright colors, round, matte rubber look)
- All objects feel like they could be physical toys

### Ground
- Top edge: grass tufts (green, varied heights, soft 3D look)
- Body: layered dirt (brown gradient, matte)
- Tileable horizontally

### Sky
- Top: deeper blue (`#1976D2`)
- Bottom: lighter blue (`#4FC3F7`)
- Soft gradient — clean, not painterly
- Scattered soft clouds (3D-rendered, puffy)

---

## Effects Style

| Effect | Visual Style |
|--------|-------------|
| Rocket trail | Soft puffy smoke (grey-white), 3D cloud puffs dissipating |
| Explosion (target) | Yellow-orange burst + soft particle poof |
| Explosion (ground) | Brown dust cloud + small debris pieces |
| Crater | Dark circle with cracked edge |
| Win celebration | Yellow stars + colorful confetti particles |
| Screen shake | Already implemented — keep subtle |

---

## Asset Specifications

| Asset Type | Max Size | Format | Notes |
|------------|----------|--------|-------|
| Sprites | 512x512 | PNG-24, transparent | Power of 2 preferred |
| Sprite sheets | 1024x1024 | PNG-24, transparent | Atlas packing |
| Background layers | 1920x1080 | PNG-24 | Tileable horizontally |
| UI elements | 256x256 | PNG-24, transparent | 9-slice where possible |
| Particle sprites | 64x64 | PNG-24, transparent | Small, simple shapes |

### Import Settings (Unity)
- Filter Mode: Bilinear
- Compression: ASTC 6x6 (iOS) / ETC2 (Android)
- Pixels Per Unit: 100 (default, adjust per asset)
- Sprite Mode: Single or Multiple (for sheets)

---

## Sorting Layers (back to front)

1. `Background` — sky gradient
2. `Clouds` — parallax cloud layer
3. `Mountains` — parallax hill silhouettes
4. `Environment` — ground, trees, bushes, rocks
5. `Gameplay` — rocket, target, obstacles, launcher
6. `Effects` — trail, explosion, debris, crater
7. `UI` — HUD, buttons, text, win screen

---

## AI Prompt Templates

### For generating game assets
```
3D rendered toy-style game sprite, [object name], matte finish, soft shading, 
chunky proportions, retro vinyl toy aesthetic, clean white background, 
no outlines, smooth surfaces, mobile game asset, PNG transparent background
```

### For generating rocket specifically
```
3D rendered retro toy rocket, solid matte red, cone nose with seam line, 
cylindrical body, 3 chunky swept fins at base, small nozzle, no window, 
vinyl toy collectible style, soft studio lighting, white background, 
PNG transparent background
```

### For generating backgrounds
```
3D rendered toy diorama background, [scene description], bright cheerful colors, 
soft lighting, clean gradients, parallax layer, mobile game, 
miniature toy world aesthetic
```

### For generating UI elements
```
3D rendered toy-style game UI button, [button type], rounded corners, 
matte finish, soft shadow, bright colors, chunky, mobile game interface
```
