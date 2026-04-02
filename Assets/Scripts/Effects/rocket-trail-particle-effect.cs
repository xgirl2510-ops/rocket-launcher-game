using UnityEngine;

/// <summary>
/// Manages a ParticleSystem trail on the Rocket. Call StartTrail() on launch,
/// StopTrail() on land/hit. Configures particles programmatically if no
/// ParticleSystem exists yet.
/// </summary>
public class RocketTrail : MonoBehaviour
{
    [Header("Trail Settings")]
    [SerializeField] private float _emissionRate = 40f;
    [SerializeField] private float _particleLifetime = 0.4f;
    [SerializeField] private float _startSize = 0.15f;
    [SerializeField] private Color _startColor = new Color(1f, 0.3f, 0f, 1f);   // bright red-orange
    [SerializeField] private Color _endColor = new Color(0.4f, 0.4f, 0.4f, 0f); // grey, fade out

    private ParticleSystem _ps;

    private void Awake()
    {
        _ps = GetComponentInChildren<ParticleSystem>();
        if (_ps == null)
        {
            _ps = CreateTrailParticleSystem();
        }

        // Ensure trail is off at start
        StopTrail();
    }

    /// <summary>Begin emitting trail particles (call on rocket launch).</summary>
    public void StartTrail()
    {
        if (_ps == null) return;
        _ps.Clear();
        _ps.Play();
    }

    /// <summary>Stop emitting trail particles (call on land/hit).</summary>
    public void StopTrail()
    {
        if (_ps == null) return;
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    /// <summary>Stop and clear all particles immediately (call on reset).</summary>
    public void ClearTrail()
    {
        if (_ps == null) return;
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private ParticleSystem CreateTrailParticleSystem()
    {
        var trailGO = new GameObject("TrailParticles");
        trailGO.transform.SetParent(transform, false);
        trailGO.transform.localPosition = new Vector3(0f, -0.4f, 0f);

        var ps = trailGO.AddComponent<ParticleSystem>();

        // Main module
        var main = ps.main;
        main.startLifetime = _particleLifetime;
        main.startSpeed = 0.5f;
        main.startSize = _startSize;
        main.startColor = new Color(1f, 0.2f, 0f, 1f); // red-orange at spawn
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.playOnAwake = false;

        // Emission
        var emission = ps.emission;
        emission.rateOverTime = _emissionRate;

        // Shape: cone pointing backward (opposite of rocket nose)
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // emit downward from rocket

        // Size over lifetime: shrink
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        // Color over lifetime: red (near rocket) → orange → dark grey smoke, fade out
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.1f, 0f), 0f),     // red
                new GradientColorKey(new Color(1f, 0.5f, 0f), 0.35f),  // orange
                new GradientColorKey(new Color(0.25f, 0.22f, 0.2f), 1f) // dark grey-black smoke
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // Renderer: must assign material or particles show as pink
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.sortingLayerName = "Projectile";
        renderer.sortingOrder = -1; // behind rocket sprite

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }
}
