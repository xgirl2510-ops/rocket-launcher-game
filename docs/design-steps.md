# Visual Design & Animation — Step-by-Step Plan

**Art Style:** Cartoon/cute (Angry Birds-like)
**Tools:** AI generate (Midjourney/DALL-E/Stable Diffusion) + Photoshop cleanup
**Budget:** Open to buying asset packs

---

## Step 1: Art Direction & Style Guide

Xác định "look & feel" trước khi vẽ bất cứ gì:
- Color palette (3-5 màu chủ đạo + accent colors)
- Mood board (collect reference images từ games tương tự)
- Art style rules: line thickness, shading style, proportion
- Reference games: Angry Birds, Bad Piggies, Doodle Army

**Output**: 1 trang style guide + mood board

---

## Step 2: Character & Object Design (AI Generate + Photoshop)

| Asset | Mô tả |
|-------|--------|
| Rocket | Main character — idle, flying, exploding states |
| Target | Mục tiêu — nổi bật, animation khi bị hit |
| Slingshot/Launcher | Bệ phóng — thay thế spawn point |
| Obstacles | Vật cản — đa dạng hình dạng |
| Ground | Nền đất — tileable texture |
| Sky/Background | Parallax background (2-3 layers) |

---

## Step 3: UI/UX Design

| Element | Cần làm |
|---------|---------|
| HUD | Round counter, shot counter, best score — styled font |
| Buttons | Restart, Auto Play, View Target — cartoon style |
| Win Screen | Victory celebration overlay |
| Fonts | 1-2 custom fonts (cartoon style) |
| Icons | Sound on/off, settings, etc. |

---

## Step 4: Background & Environment

- Sky: gradient + clouds (parallax layer 1)
- Mountains/Hills: silhouette (parallax layer 2)
- Ground: textured terrain, grass on top
- Decorations: trees, bushes, rocks (foreground props)

---

## Step 5: Animation (Sprite/Skeletal)

| Animation | Type | Chi tiết |
|-----------|------|----------|
| Rocket idle | Sprite | Lắc lư nhẹ trên bệ phóng |
| Rocket flying | Code + VFX | Rotate to velocity + trail mới |
| Rocket hit target | Sprite sheet | Explosion cartoon (poof/stars) |
| Rocket hit ground | Sprite sheet | Crash + debris |
| Target idle | Sprite | Bounce nhẹ / glow |
| Target hit | Sprite sheet | Shatter / confetti |
| Win celebration | Particles + UI | Stars, confetti, text zoom |
| Slingshot stretch | Sprite/code | Elastic band visual khi aim |

---

## Step 6: VFX Polish

| Effect hiện tại | Upgrade |
|-----------------|---------|
| RocketTrail (particle) | Custom cartoon smoke trail sprite |
| ExplosionBurst (particle) | Cartoon explosion spritesheet |
| GroundScorch (sprite mask) | Textured crater mark |
| RocketDebris (squares) | Shaped debris pieces |

---

## Step 7: Sound Design Upgrade

- Cartoon sound effects (boing, whoosh, splat)
- Background music (chill/happy loop)
- Có thể mua từ asset stores

---

## Step 8: Integration & Polish

- Replace placeholder sprites
- Set up Sorting Layers (Background < Environment < Gameplay < UI)
- Implement parallax scrolling
- Tune animation timing
- Screen transitions
- Test trên device

---

## Thứ tự thực hiện

```
Step 1 → Step 2 → Step 4 → Step 3 → Step 5 → Step 6 → Step 7 → Step 8
```
