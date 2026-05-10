using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Three-layer afterburner exhaust for hovering aircraft (jets, target).
    /// Layers: smoke (back) → flame (middle) → core (front).
    ///
    /// Tuning knobs exposed as fields so different aircraft can have visually distinct trails:
    ///  • _trailLengthScale — multiplies particle lifetime + speed (longer = more stretched out)
    ///  • _hueShift          — per-instance flame color shift in HSV degrees so a fleet of jets
    ///                         doesn't all look identical
    ///  • _coreTint, _flameTint — base colors that drive the gradient
    /// </summary>
    public class JetExhaustTrail : MonoBehaviour
    {
        [Header("Anchor")]
        // Sprite layout: nose/cockpit on LEFT, tail/nozzle on RIGHT.
        // +1 = RIGHT edge (tail nozzle, exhaust shoots away from cockpit).
        [SerializeField] private float _nozzleXFraction = 1.0f;
        [SerializeField] private float _nozzleYFraction = -0.45f;

        [Header("Length & Speed")]
        // 1.0 = default short jet trail. <1 = shorter, >1 = longer (used by target with bigger sprite).
        [SerializeField] private float _trailLengthScale = 0.8f;

        [Header("Intensity")]
        // Multiplier on emission rate across all 3 layers. >1 = denser/punchier flame (used by
        // target boss to read as more powerful than protector jets).
        [SerializeField] private float _emissionMultiplier = 1f;

        [Header("Color Variation")]
        // Per-instance hue rotation in degrees. Auto-randomized in Awake unless overridden.
        [SerializeField] private float _hueShiftDegrees = 0f;
        [SerializeField] private bool _randomizeHueOnAwake = true;
        [SerializeField] private Color _coreTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _flameTint = new Color(1f, 0.55f, 0.1f, 1f);

        // Soft circle texture (built once, shared by every aircraft's particle materials)
        private static Texture2D _softCircleTexture;

        private void Awake()
        {
            if (_randomizeHueOnAwake)
                _hueShiftDegrees = Random.Range(-25f, 25f);  // ±25° hue swing across fleet

            // Order: smoke (back) → flame → core (front) so brightness layers stack correctly.
            CreateSmokeSystem();
            CreateFlameSystem();
            CreateCoreSystem();
        }

        /// <summary>External tuning — used by SceneSetupTool to set target's distinctive trail.</summary>
        public void Configure(float trailLengthScale, float hueShiftDegrees, Color coreTint, Color flameTint, float nozzleXFraction = 1f, float emissionMultiplier = 1f)
        {
            _trailLengthScale = trailLengthScale;
            _hueShiftDegrees = hueShiftDegrees;
            _randomizeHueOnAwake = false;
            _coreTint = coreTint;
            _flameTint = flameTint;
            _nozzleXFraction = nozzleXFraction;
            _emissionMultiplier = emissionMultiplier;
        }

        // ============================================================
        // Core — bright hot nozzle point
        // ============================================================
        private ParticleSystem CreateCoreSystem()
        {
            var go = CreateChildAtNozzle("ExhaustCore");
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 0.18f * _trailLengthScale;
            main.startSpeed = 8f * _trailLengthScale;
            main.startSize = 0.10f;
            main.startColor = ShiftHue(_coreTint, _hueShiftDegrees);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 200;

            var coreEmission = ps.emission;
            coreEmission.rateOverTime = 80f * _emissionMultiplier;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.02f;
            shape.rotation = Vector3.zero;

            var velocity = ps.limitVelocityOverLifetime;
            velocity.enabled = true;
            velocity.dampen = 0.2f;
            velocity.limit = 8f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(_coreTint, 0f),
                    new GradientColorKey(ShiftHue(_coreTint, _hueShiftDegrees), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

            ConfigureRenderer(ps, additive: true, sortingOrderOffset: 1);
            return ps;
        }

        // ============================================================
        // Flame — orange plume (main visible body)
        // ============================================================
        private ParticleSystem CreateFlameSystem()
        {
            var go = CreateChildAtNozzle("ExhaustFlame");
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 0.4f * _trailLengthScale;
            main.startSpeed = 4f * _trailLengthScale;
            main.startSize = 0.18f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 150;

            var flameEmission = ps.emission;
            flameEmission.rateOverTime = 50f * _emissionMultiplier;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.04f;
            shape.rotation = Vector3.zero;

            var velocity = ps.limitVelocityOverLifetime;
            velocity.enabled = true;
            velocity.dampen = 0.15f;
            velocity.limit = 4f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            Color flameStart = ShiftHue(_flameTint, _hueShiftDegrees);
            Color flameMid = Color.Lerp(flameStart, new Color(0.8f, 0.1f, 0.05f), 0.6f);
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(flameStart, 0f),
                    new GradientColorKey(flameMid, 0.6f),
                    new GradientColorKey(new Color(0.3f, 0.1f, 0.05f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.5f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));

            ConfigureRenderer(ps, additive: true, sortingOrderOffset: 0);
            return ps;
        }

        // ============================================================
        // Smoke — lingering grey tail (alpha-blend)
        // ============================================================
        private ParticleSystem CreateSmokeSystem()
        {
            var go = CreateChildAtNozzle("ExhaustSmoke");
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 0.9f * _trailLengthScale;
            main.startSpeed = 1.2f * _trailLengthScale;
            main.startSize = 0.26f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;

            var smokeEmission = ps.emission;
            smokeEmission.rateOverTime = 25f * _emissionMultiplier;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.05f;
            shape.rotation = Vector3.zero;

            var velocity = ps.limitVelocityOverLifetime;
            velocity.enabled = true;
            velocity.dampen = 0.1f;
            velocity.limit = 2f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.8f, 0.8f, 0.8f), 0f),
                    new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.5f, 0f),
                    new GradientAlphaKey(0.3f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.6f));

            ConfigureRenderer(ps, additive: false, sortingOrderOffset: -1);
            return ps;
        }

        // ============================================================
        // Anchoring helpers
        // ============================================================
        private GameObject CreateChildAtNozzle(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            float nozzleX = GetNozzleLocalEdge() * _nozzleXFraction;
            float nozzleY = GetNozzleLocalY();
            go.transform.localPosition = new Vector3(nozzleX, nozzleY, 0f);
            // Exhaust shoots OPPOSITE the nozzle direction (away from the aircraft body),
            // not parallel to it. Nozzle on RIGHT → flame trails to the LEFT, and vice-versa.
            float yRot = _nozzleXFraction > 0 ? 90f : -90f;
            go.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            return go;
        }

        private float GetNozzleLocalEdge()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return 4.96f * 0.5f;
            float worldHalf = sr.bounds.extents.x;
            float parentScale = Mathf.Max(transform.localScale.x, 0.0001f);
            return worldHalf / parentScale;
        }

        private float GetNozzleLocalY()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return 0.5f * _nozzleYFraction;
            float worldHalf = sr.bounds.extents.y;
            float parentScale = Mathf.Max(transform.localScale.y, 0.0001f);
            return (worldHalf / parentScale) * _nozzleYFraction;
        }

        // ============================================================
        // Hue shift utility — rotate base color in HSV space
        // ============================================================
        private static Color ShiftHue(Color c, float degrees)
        {
            if (Mathf.Approximately(degrees, 0f)) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + degrees / 360f, 1f);
            Color shifted = Color.HSVToRGB(h, s, v);
            shifted.a = c.a;
            return shifted;
        }

        // ============================================================
        // Renderer + material setup
        // ============================================================
        private static void ConfigureRenderer(ParticleSystem ps, bool additive, int sortingOrderOffset)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var mat = additive ? CreateAdditiveMaterial() : CreateAlphaMaterial();
            mat.mainTexture = GetSoftCircleTexture();
            renderer.material = mat;
            renderer.sortingLayerName = GameConstants.SortingLayerGameplay;
            renderer.sortingOrder = sortingOrderOffset;
        }

        private static Material CreateAlphaMaterial() => new Material(Shader.Find("Sprites/Default"));

        private static Material CreateAdditiveMaterial()
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            return mat;
        }

        private static Texture2D GetSoftCircleTexture()
        {
            if (_softCircleTexture != null) return _softCircleTexture;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxDist = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(dist / maxDist);
                    float alpha = Mathf.Exp(-3f * t * t);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _softCircleTexture = tex;
            return tex;
        }
    }
}
