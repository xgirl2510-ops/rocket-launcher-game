using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns colored debris pieces that fly outward then fall to the ground.
/// Pieces stay on the ground until ClearAll() is called (on rocket reset).
/// Uses manual movement + gravity (no Rigidbody) to avoid fall-through.
/// </summary>
public class RocketDebris : MonoBehaviour
{
    private static readonly Color[] DebrisColors = {
        new Color(0.8f, 0f, 0f, 1f),      // dark red (body)
        new Color(1f, 0.2f, 0f, 1f),       // orange-red
        new Color(0.6f, 0f, 0f, 1f),       // deep red
        new Color(0.3f, 0.3f, 0.3f, 1f),   // grey (metal)
    };

    private static readonly List<GameObject> _allDebris = new List<GameObject>();

    private const float GroundY = -5f; // must match GroundTop in scene setup
    private const float Gravity = 12f;

    private const float AutoDestroyDelay = 3f; // debris fades and self-destructs

    private Vector2 _velocity;
    private float _angularSpeed;
    private bool _grounded;
    private float _groundedTimer;
    private SpriteRenderer _sr;

    /// <summary>Spawn debris pieces at position flying outward.</summary>
    public static void Spawn(Vector2 position, int count = 16)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Debris");
            go.transform.position = new Vector3(position.x, position.y, 0f);

            float size = Random.Range(0.08f, 0.22f);
            go.transform.localScale = new Vector3(size, size, 1f);

            // Sprite
            var sr = go.AddComponent<SpriteRenderer>();
            Color color = DebrisColors[Random.Range(0, DebrisColors.Length)];
            sr.color = color;
            sr.sortingLayerName = "Projectile";
            sr.sortingOrder = 5;

            // Simple white square sprite
            Texture2D tex = new Texture2D(4, 4);
            Color[] pixels = new Color[16];
            for (int p = 0; p < 16; p++) pixels[p] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

            // Movement component
            var debris = go.AddComponent<RocketDebris>();

            // Random direction biased upward
            float angle = Random.Range(15f, 165f) * Mathf.Deg2Rad;
            float speed = Random.Range(1.5f, 4f);
            debris._velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            debris._angularSpeed = Random.Range(-360f, 360f);

            _allDebris.Add(go);
        }
    }

    /// <summary>Destroy all debris pieces (call on rocket reset).</summary>
    public static void ClearAll()
    {
        for (int i = _allDebris.Count - 1; i >= 0; i--)
        {
            if (_allDebris[i] != null)
                Destroy(_allDebris[i]);
        }
        _allDebris.Clear();
    }

    private void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (_grounded)
        {
            // Fade out then self-destruct
            _groundedTimer += Time.deltaTime;
            if (_sr != null)
            {
                float fade = 1f - Mathf.Clamp01(_groundedTimer / AutoDestroyDelay);
                var c = _sr.color;
                c.a = fade;
                _sr.color = c;
            }
            if (_groundedTimer >= AutoDestroyDelay)
                Destroy(gameObject);
            return;
        }

        // Apply gravity
        _velocity.y -= Gravity * Time.deltaTime;

        // Move
        transform.position += (Vector3)(_velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, _angularSpeed * Time.deltaTime);

        // Land on ground
        if (transform.position.y <= GroundY)
        {
            transform.position = new Vector3(transform.position.x, GroundY, 0f);
            _grounded = true;
        }
    }

    private void OnDestroy()
    {
        _allDebris.Remove(gameObject);
    }
}
