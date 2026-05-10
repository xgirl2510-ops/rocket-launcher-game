using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Visual aim indicator: rotates to face launch direction, scales Y by force magnitude.
    /// Controlled by LaunchController — Show/Hide/UpdateArrow.
    /// </summary>
    public class AimArrow : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        // Initial length matches the rocket sprite (~1 world unit) so the arrow is visible
        // the moment dragging starts but doesn't poke past the rocket nose and obscure it.
        [SerializeField, Range(0.3f, 2f)] private float _minScale = 1.0f;
        [SerializeField, Range(1f, 4f)] private float _maxScale = 2.5f;
        [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.7f);
        [Tooltip("Line thickness (X scale). Smaller = thinner aim guide.")]
        [SerializeField, Range(0.02f, 0.2f)] private float _lineThickness = 0.05f;
        [Tooltip("Render order — keep negative so the arrow stays BEHIND the rocket sprite.")]
        [SerializeField] private int _sortingOrder = -1;

        [Header("Angle Status Colors")]
        [SerializeField] private Color _validColor = new Color(0.3f, 1f, 0.3f, 0.85f);   // green
        [SerializeField] private Color _nearLimitColor = new Color(1f, 0.85f, 0.2f, 0.9f); // yellow
        [SerializeField] private Color _clampedColor = new Color(1f, 0.25f, 0.25f, 0.95f); // red

        private float _initialScaleX;

        private void Awake()
        {
            // Force-apply thickness from serialized field — overrides whatever the editor/MCP
            // set in the scene transform so we have a reliable single source of truth.
            _initialScaleX = _lineThickness;
            transform.localScale = new Vector3(_lineThickness, transform.localScale.y, transform.localScale.z);

            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _color;
                _spriteRenderer.sortingOrder = _sortingOrder; // ensure arrow renders behind rocket
                // Defensive: cached editor sprite may be null (broken square asset).
                // Without a sprite, the arrow stays invisible even when Show() enables it.
                if (_spriteRenderer.sprite == null)
                    _spriteRenderer.sprite = RuntimeSpriteFactory.GetSolidSprite();
            }

            Hide();
        }

        /// <summary>Make the aim arrow visible.</summary>
        public void Show()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = true;
        }

        /// <summary>Hide the aim arrow.</summary>
        public void Hide()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        /// <summary>
        /// Rotate arrow to face direction, scale Y by normalizedForce (0-1), color by angle status.
        /// Arrow sprite points UP by default, so -90f offset to align with direction.
        /// </summary>
        public void UpdateArrow(Vector2 direction, float normalizedForce, AimAngleStatus status = AimAngleStatus.Valid)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            float scaleY = Mathf.Lerp(_minScale, _maxScale, normalizedForce);
            transform.localScale = new Vector3(_initialScaleX, scaleY, 1f);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = status switch
                {
                    AimAngleStatus.Clamped => _clampedColor,
                    AimAngleStatus.NearLimit => _nearLimitColor,
                    _ => _validColor
                };
            }
        }
    }
}
