using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Reserves bottom screen space for a banner ad by shrinking the Camera's viewport Rect
    /// from the bottom up. World gameplay tọa độ KHÔNG đổi — the camera simply stops drawing
    /// the bottom strip, leaving that pixel area for the AdMob banner view (which sits in
    /// device window space, not Unity world space).
    ///
    /// Also bumps orthographicSize proportionally so the visible gameplay area keeps the
    /// same aspect ratio as before — without this compensation, the playfield gets squashed
    /// vertically when the viewport is shorter.
    ///
    /// Attach to the same GameObject as Camera + CameraController.
    /// Toggle <see cref="BannerVisible"/> at runtime when the ad shows/hides.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraBannerAdViewportReservation : MonoBehaviour
    {
        [Tooltip("Fraction of screen height reserved at the bottom for the banner ad (0..0.25).")]
        [SerializeField, Range(0f, 0.25f)] private float _bannerHeightFraction = 0.10f;

        [Tooltip("Compensate ortho size so visible world height stays the same after the cut.")]
        [SerializeField] private bool _compensateOrthoSize = true;

        [Tooltip("Reserve viewport on Awake. Disable if you only want to call Show/Hide manually.")]
        [SerializeField] private bool _reserveOnAwake = true;

        private Camera _camera;
        private float _baseOrthoSize;
        private bool _bannerVisible;

        /// <summary>True while the bottom strip is reserved (banner is showing).</summary>
        public bool BannerVisible => _bannerVisible;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _baseOrthoSize = _camera.orthographicSize;
            if (_reserveOnAwake) ShowBanner();
        }

        /// <summary>Reserve bottom strip — camera renders only top (1 - bannerFraction).</summary>
        public void ShowBanner()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            // Rect: x=0, y=bannerFraction (bottom edge of viewport), w=1, h=1-bannerFraction.
            _camera.rect = new Rect(0f, _bannerHeightFraction, 1f, 1f - _bannerHeightFraction);

            // Aspect compensation: with the viewport shrunk vertically, the same orthoSize
            // shows a SHORTER world strip. Multiply orthoSize by (1 / (1 - bannerFraction))
            // so the visible world height is the same as before the cut.
            if (_compensateOrthoSize)
                _camera.orthographicSize = _baseOrthoSize / Mathf.Max(0.01f, 1f - _bannerHeightFraction);

            _bannerVisible = true;
        }

        /// <summary>Restore full-screen viewport — banner is hidden.</summary>
        public void HideBanner()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            _camera.rect = new Rect(0f, 0f, 1f, 1f);
            if (_compensateOrthoSize)
                _camera.orthographicSize = _baseOrthoSize;
            _bannerVisible = false;
        }
    }
}
