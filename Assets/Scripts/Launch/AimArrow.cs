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
        [SerializeField, Range(0.3f, 1f)] private float _minScale = 0.5f;
        [SerializeField, Range(1f, 3f)] private float _maxScale = 2.0f;
        [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.7f);

        private void Awake()
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_spriteRenderer != null)
                _spriteRenderer.color = _color;

            Hide();
        }

        public void Show()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = true;
        }

        public void Hide()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        /// <summary>
        /// Rotate arrow to face direction, scale Y by normalizedForce (0-1).
        /// Arrow sprite points UP by default, so -90f offset to align with direction.
        /// </summary>
        public void UpdateArrow(Vector2 direction, float normalizedForce)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            float scaleY = Mathf.Lerp(_minScale, _maxScale, normalizedForce);
            transform.localScale = new Vector3(transform.localScale.x, scaleY, 1f);
        }
    }
}
