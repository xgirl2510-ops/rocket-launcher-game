using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uses SpriteMask to cut real holes in the ground at rocket impact points.
/// Three layers per crater:
///   1. SpriteMask — cuts the ground sprite (VisibleOutsideMask)
///   2. Dark interior — behind ground, visible through the hole (shows depth)
///   3. Scorch marks — burn stains on ground surface around the hole
/// On first spawn, sets Ground SpriteRenderer.maskInteraction = VisibleOutsideMask.
/// Persists until ClearAll() is called (round restart).
/// </summary>
public class GroundScorch : MonoBehaviour
{
    private static readonly List<GameObject> _allCraters = new List<GameObject>();
    private static readonly List<float> _craterXPositions = new List<float>();
    private static readonly List<float> _craterWidths = new List<float>();
    private static readonly List<float> _craterDepths = new List<float>();
    private static Sprite _holeSprite;
    private static bool _groundPrepared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _allCraters.Clear();
        _craterXPositions.Clear();
        _craterWidths.Clear();
        _craterDepths.Clear();
        _holeSprite = null;
        _groundPrepared = false;
    }

    /// <summary>Find ground and enable mask interaction so SpriteMasks cut it.</summary>
    private static void PrepareGround()
    {
        if (_groundPrepared) return;
        var ground = GameObject.FindWithTag(GameConstants.TagGround);
        if (ground == null) return;
        var sr = ground.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        _groundPrepared = true;
    }

    /// <summary>Spawn a real crater hole in the ground. Size scales with max flight height.</summary>
    public static void Spawn(Vector2 impactPosition, float maxHeight = 10f)
    {
        PrepareGround();
        EnsureSprites();

        float groundY = GameConstants.GroundTop;

        // Scale crater by how high the rocket flew (higher = more impact energy = bigger crater)
        // Normalize: 0-5 = small, 5-20 = medium, 20-45 = large
        float heightAboveGround = Mathf.Max(0f, maxHeight - groundY);
        float scale;
        if (heightAboveGround <= 15f)
            scale = Random.Range(0.8f, 1.2f);
        else if (heightAboveGround <= 30f)
            scale = Random.Range(1.2f, 1.8f);
        else
            scale = Random.Range(1.8f, 2.5f);

        float craterW = Random.Range(1.2f, 1.6f) * scale;
        float craterH = Random.Range(0.8f, 1.2f) * scale;

        // Parent GO for easy cleanup
        var parent = new GameObject("Crater");
        parent.transform.position = new Vector3(impactPosition.x, groundY, 0f);

        // 1) Dark hole interior — sits behind ground, visible through the mask hole
        var holeGo = new GameObject("HoleInterior");
        holeGo.transform.SetParent(parent.transform, false);
        // Push rectangle below ground line so it only fills the hole area
        holeGo.transform.localPosition = new Vector3(0f, -craterH, 0f);
        holeGo.transform.localScale = new Vector3(craterW * 3f, craterH * 3f, 1f);
        var holeSr = holeGo.AddComponent<SpriteRenderer>();
        holeSr.sprite = _holeSprite;
        holeSr.color = new Color(0.2f, 0.14f, 0.06f, 1f); // dark earth color
        holeSr.sortingLayerName = "Environment";
        holeSr.sortingOrder = -1; // behind ground (ground = 0)
        // Only visible inside mask area — hidden outside the crater hole
        holeSr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        // 2) SpriteMask — cuts a jagged hole in the ground
        var maskGo = new GameObject("CraterMask");
        maskGo.transform.SetParent(parent.transform, false);
        // Mask wider than hole so mouth of crater is fully open
        maskGo.transform.localScale = new Vector3(craterW * 1.15f, craterH * 1.1f, 1f);
        var mask = maskGo.AddComponent<SpriteMask>();
        // Each crater gets unique jagged shape
        mask.sprite = BuildMaskSprite();
        mask.alphaCutoff = 0.5f;

        // Spawn dirt/rock chunks flying up from the crater
        RocketDebris.SpawnDirtDebris(impactPosition, scale);

        _craterXPositions.Add(impactPosition.x);
        _craterWidths.Add(craterW);
        _craterDepths.Add(craterH);
        _allCraters.Add(parent);
    }

    /// <summary>
    /// Returns ground Y at given X, accounting for craters.
    /// Debris uses this to fall into crater holes instead of stopping at surface.
    /// </summary>
    public static float GetGroundY(float x)
    {
        float baseY = GameConstants.GroundTop;
        for (int i = 0; i < _craterXPositions.Count; i++)
        {
            float halfW = _craterWidths[i] * 0.4f;
            float dx = Mathf.Abs(x - _craterXPositions[i]);
            if (dx >= halfW) continue;

            // Deeper at center, shallower at edges. Matches visual crater depth.
            float t = 1f - (dx / halfW);
            float depth = _craterDepths[i] * t * 0.7f;
            float craterY = baseY - depth;
            if (craterY < baseY)
                baseY = craterY;
        }
        return baseY;
    }

    /// <summary>Destroy all craters (call on round restart).</summary>
    public static void ClearAll()
    {
        for (int i = _allCraters.Count - 1; i >= 0; i--)
        {
            if (_allCraters[i] != null)
                Destroy(_allCraters[i]);
        }
        _allCraters.Clear();
        _craterXPositions.Clear();
        _craterWidths.Clear();
        _craterDepths.Clear();
    }

    private static void EnsureSprites()
    {
        if (!_holeSprite) _holeSprite = BuildSolidSprite();
    }

    /// <summary>
    /// Jagged semicircle for the SpriteMask. Perlin noise on radius for irregular edges.
    /// Pivot at top-center so it hangs downward from ground line.
    /// </summary>
    private static Sprite BuildMaskSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size);
        float cx = size / 2f;
        float baseRadius = size / 2f;

        // Precompute jagged radius per angle using Perlin noise
        float noiseOffset = Random.Range(0f, 100f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - size; // relative to top-center
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Perlin noise modulates radius: +-20% jaggedness
                float noise = Mathf.PerlinNoise(angle * 3f + noiseOffset, 0.5f);
                float jaggedRadius = baseRadius * (0.8f + noise * 0.4f);

                bool inside = dist < jaggedRadius && y < size - 1;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Point; // sharp jagged edges
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 1f), size);
    }

    /// <summary>Simple 4x4 white solid sprite — color tinted via SpriteRenderer.</summary>
    private static Sprite BuildSolidSprite()
    {
        var tex = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    private void OnDestroy()
    {
        _allCraters.Remove(gameObject);
    }
}
