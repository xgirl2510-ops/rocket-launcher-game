using UnityEngine;

/// <summary>
/// One-shot particle burst at rocket impact position.
/// Gold/yellow for target hit, grey/white for miss.
/// Auto-destroys after particles finish playing.
/// Instantiate via ExplosionEffect.Spawn() static helper.
/// </summary>
public class ExplosionEffect : MonoBehaviour
{
    [Header("Burst Settings")]
    [SerializeField] private int _burstCount = 30;
    [SerializeField] private float _particleLifetime = 0.6f;
    [SerializeField] private float _startSpeed = 4f;
    [SerializeField] private float _startSize = 0.2f;

    private ParticleSystem _ps;

    /// <summary>
    /// Spawn an explosion at the given position.
    /// isHit=true → gold/yellow burst; isHit=false → grey/white burst.
    /// </summary>
    public static void Spawn(Vector2 position, bool isHit)
    {
        var go = new GameObject("Explosion");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var fx = go.AddComponent<ExplosionEffect>();
        fx.InitAndPlay(isHit);
    }

    private void InitAndPlay(bool isHit)
    {
        _ps = gameObject.AddComponent<ParticleSystem>();

        // Stop auto-play so we can configure first
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Color burstColor = isHit
            ? new Color(1f, 0.84f, 0f, 1f)  // gold
            : new Color(0.7f, 0.7f, 0.7f, 1f); // grey

        Color fadeColor = isHit
            ? new Color(1f, 1f, 0f, 0f)     // yellow, fade out
            : new Color(1f, 1f, 1f, 0f);    // white, fade out

        ConfigureParticleSystem(burstColor, fadeColor);

        _ps.Play();

        // Auto-destroy after particles finish
        Destroy(gameObject, _particleLifetime + 0.2f);
    }

    private void ConfigureParticleSystem(Color burstColor, Color fadeColor)
    {
        // Main module
        var main = _ps.main;
        main.startLifetime = _particleLifetime;
        main.startSpeed = _startSpeed;
        main.startSize = _startSize;
        main.startColor = burstColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;
        main.playOnAwake = false;
        main.loop = false;

        // Emission: single burst
        var emission = _ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, _burstCount)
        });

        // Shape: circle burst outward
        var shape = _ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        // Size over lifetime: shrink to nothing
        var sizeOverLifetime = _ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        // Color over lifetime: burst color to fade color
        var colorOverLifetime = _ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(burstColor, 0f),
                new GradientColorKey(fadeColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // Renderer settings
        var renderer = _ps.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerName = "Projectile";
        renderer.sortingOrder = 10; // on top of everything
    }
}
