using System;
using UnityEngine;

/// <summary>
/// Rocket physics: launch with impulse, rotate to face velocity, detect ground/target collision.
/// Starts Kinematic, becomes Dynamic on Launch(). Events notify GameManager and CameraController.
/// Integrates with RocketTrail (trail particles) and ExplosionEffect (impact burst).
/// </summary>
public class Rocket : MonoBehaviour
{
    [Header("Tags")]
    [SerializeField] private string _groundTag = GameConstants.TagGround;
    [SerializeField] private string _targetTag = GameConstants.TagTarget;

    // Events — subscribed by GameManager, CameraController
    public event Action OnRocketLaunched;
    public event Action OnRocketLanded;
    public event Action OnTargetHit;

    private Rigidbody2D _rb;
    private bool _isFlying;
    private float _maxHeight;
    private RocketTrail _trail;
    private SpriteRenderer[] _spriteRenderers;

    /// <summary>Whether the rocket is currently in flight.</summary>
    public bool IsFlying => _isFlying;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _trail = GetComponent<RocketTrail>();
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// Switch to Dynamic, apply impulse force, fire OnRocketLaunched.
    /// </summary>
    public void Launch(Vector2 direction, float force)
    {
        // Rotate to face launch direction immediately (no 1-frame delay)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.AddForce(direction * force, ForceMode2D.Impulse);
        _isFlying = true;
        _maxHeight = transform.position.y;

        if (_trail != null) _trail.StartTrail();

        OnRocketLaunched?.Invoke();
    }

    /// <summary>
    /// Reset rocket to spawn position, zero velocity, set Kinematic.
    /// </summary>
    public void ResetToPosition(Vector2 position)
    {
        _isFlying = false;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        transform.rotation = Quaternion.identity;

        if (_trail != null) _trail.ClearTrail();
        SetSpritesVisible(true);
    }

    /// <summary>Show/hide all child SpriteRenderers (for shatter effect).</summary>
    private void SetSpritesVisible(bool visible)
    {
        foreach (var sr in _spriteRenderers)
            sr.enabled = visible;
    }

    private void FixedUpdate()
    {
        if (!_isFlying) return;
        if (transform.position.y > _maxHeight)
            _maxHeight = transform.position.y;
        RotateToVelocity();
    }

    /// <summary>
    /// Rotate sprite so nose (top) faces velocity direction.
    /// -90f because rocket sprite points UP by default.
    /// </summary>
    private void RotateToVelocity()
    {
        Vector2 vel = _rb.linearVelocity;
        if (vel.sqrMagnitude < 0.01f) return;

        float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f;
        _rb.MoveRotation(angle);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isFlying) return;
        if (!collision.gameObject.CompareTag(_groundTag)) return;

        _isFlying = false;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        if (_trail != null) _trail.StopTrail();
        ExplosionEffect.Spawn(transform.position, false);
        RocketDebris.Spawn(transform.position);

        // Only spawn crater if rocket actually hit the ground, not an obstacle in the air
        if (transform.position.y < GameConstants.GroundTop + 1.5f)
            GroundScorch.Spawn(transform.position, _maxHeight);
        SetSpritesVisible(false);

        OnRocketLanded?.Invoke();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isFlying) return;
        if (!other.CompareTag(_targetTag)) return;

        _isFlying = false;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        if (_trail != null) _trail.StopTrail();
        ExplosionEffect.Spawn(transform.position, true);
        RocketDebris.Spawn(transform.position);

        // Shatter the target — spawn bigger red debris, hide target
        RocketDebris.SpawnTargetDebris(other.transform.position);
        other.gameObject.SetActive(false);

        SetSpritesVisible(false);

        OnTargetHit?.Invoke();
    }
}
