using UnityEngine;
using UnityEngine.EventSystems;

namespace RocketLauncher
{
    /// <summary>
    /// Tactile button-press feedback: the RectTransform scales down on pointer-down, springs back
    /// to slightly above 1.0 on pointer-up, then settles at 1.0. Total ~120ms — short enough to
    /// stay snappy, long enough to feel intentional.
    ///
    /// Stateless beyond the captured base scale, so it survives prefab edits + scene reloads
    /// without needing serialization. Attach next to any Button (or any clickable Selectable).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonPressScaleAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("Scale multiplier while the button is held down.")]
        [SerializeField, Range(0.7f, 0.99f)] private float _pressedScale = 0.92f;
        [Tooltip("Time (s) for the press-down animation.")]
        [SerializeField, Range(0.02f, 0.3f)] private float _pressTime = 0.06f;
        [Tooltip("Time (s) for the release / spring-back animation.")]
        [SerializeField, Range(0.05f, 0.5f)] private float _releaseTime = 0.12f;

        private Vector3 _baseScale = Vector3.one;
        private Coroutine _animation;
        private bool _basScaleCaptured;

        private void Awake()
        {
            CaptureBaseScale();
        }

        private void OnEnable()
        {
            // Defensive: restore base scale if the GameObject was disabled mid-animation
            // (e.g. button hidden while pressed).
            CaptureBaseScale();
            transform.localScale = _baseScale;
        }

        private void CaptureBaseScale()
        {
            if (_basScaleCaptured) return;
            _baseScale = transform.localScale;
            _basScaleCaptured = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            StartAnim(_baseScale * _pressedScale, _pressTime);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StartAnim(_baseScale, _releaseTime);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Treat drag-away as a release so the button doesn't stay squished after the user
            // changes their mind.
            StartAnim(_baseScale, _releaseTime);
        }

        private void StartAnim(Vector3 target, float duration)
        {
            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(LerpScaleRoutine(target, duration));
        }

        private System.Collections.IEnumerator LerpScaleRoutine(Vector3 target, float duration)
        {
            Vector3 from = transform.localScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // ease-out cubic so the motion feels rubbery
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                transform.localScale = Vector3.LerpUnclamped(from, target, eased);
                yield return null;
            }
            transform.localScale = target;
            _animation = null;
        }
    }
}
