using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// STATIC world-space background. Behaves like any other sprite — anchored in the world,
    /// NOT locked to the camera. When the camera moves or zooms, this background stays put,
    /// so the car/jets/etc. always appear at the same point on the background image.
    ///
    /// Sizing is done ONCE in Awake (target world height = _targetWorldHeight) so the sprite
    /// dimensions are consistent across plays, then never touched again.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CameraFitHeightBackground : MonoBehaviour
    {
        // Target world height (in units). Sprite is scaled at Awake so its visible height in
        // world matches this. Width follows natural aspect of the source image.
        // 0 = no scaling, keep native sprite size. >0 = scale to that world height.
        [SerializeField] private float _targetWorldHeight = 0f;
        [SerializeField] private int _sortingOrder = -32000;
        [SerializeField] private string _sortingLayerName = "Background";

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (SortingLayer.NameToID(_sortingLayerName) != 0)
                _sr.sortingLayerName = _sortingLayerName;
            _sr.sortingOrder = _sortingOrder;

            // Z tiebreaker (camera at z=-10 looking +Z; larger z = further from camera).
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, p.y, 50f);

            // One-shot scale. If _targetWorldHeight == 0, keep the sprite at its native PPU size
            // (no scaling). Otherwise rescale so the sprite's world height matches the target.
            if (_targetWorldHeight > 0.0001f && _sr.sprite != null)
            {
                float naturalHeight = _sr.sprite.bounds.size.y;
                if (naturalHeight > 0.0001f)
                {
                    float scale = _targetWorldHeight / naturalHeight;
                    transform.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }
    }
}
