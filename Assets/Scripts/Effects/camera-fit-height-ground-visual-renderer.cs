using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Ground VISUAL renderer that occupies a constant fraction of the camera viewport from
    /// the bottom edge upward. Independent of camera zoom — when the camera zooms out, the
    /// ground strip stays the same screen fraction (does NOT reveal more depth into the dirt).
    /// Physics collider is on a separate GameObject at a fixed world Y, so rocket landing
    /// behaviour is unaffected by zoom.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CameraFitHeightGroundVisual : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        // FIXED world height of the dirt strip. Sized at default camera zoom so the strip
        // occupies ~10% of the viewport (default orthoSize ≈ 9 → viewport height ≈ 18 →
        // 10% ≈ 1.8 world units). When the camera zooms out, the strip stays this many world
        // units tall — its screen fraction SHRINKS, which means the player sees MORE sky and
        // does NOT appear to dig deeper into the dirt.
        [SerializeField] private float _worldHeight = 1.8f;
        [SerializeField] private int _sortingOrder = 10;
        [SerializeField] private string _sortingLayerName = "Environment";

        public float WorldHeight => _worldHeight;

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_camera == null) _camera = Camera.main;
            if (SortingLayer.NameToID(_sortingLayerName) != 0)
                _sr.sortingLayerName = _sortingLayerName;
            _sr.sortingOrder = _sortingOrder;
        }

        private void LateUpdate()
        {
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }
            if (_sr == null || _sr.sprite == null) return;

            float orthoY = _camera.orthographicSize;
            float orthoX = orthoY * _camera.aspect;
            Vector3 camPos = _camera.transform.position;

            // Fixed WORLD height — does NOT scale with camera zoom. Strip's bottom edge sits
            // at camBottom so it always pins to the bottom of the screen.
            float stripHeight = _worldHeight;
            float camBottom = camPos.y - orthoY;
            float stripCenterY = camBottom + stripHeight * 0.5f;

            // Scale Y so the sprite height equals stripHeight. Width scales independently so
            // the dirt always covers the camera width (no edges visible) — minor stretch is
            // acceptable for a ground texture (unlike a sky photo).
            float naturalH = _sr.sprite.bounds.size.y;
            float naturalW = _sr.sprite.bounds.size.x;
            if (naturalH < 0.0001f || naturalW < 0.0001f) return;
            float scaleY = stripHeight / naturalH;
            float scaleX = (2f * orthoX) / naturalW;

            transform.position = new Vector3(camPos.x, stripCenterY, transform.position.z);
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }
}
