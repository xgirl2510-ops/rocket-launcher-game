using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Standalone screen shake component. Computes a decaying random offset each frame.
    /// CameraController reads GetOffset() when positioning the camera.
    /// </summary>
    public class CameraScreenShake : MonoBehaviour
    {
        private float _duration;
        private float _magnitude;
        private float _elapsed;
        private Vector2 _currentOffset;

        /// <summary>Start a new screen shake. Replaces any in-progress shake.</summary>
        public void Shake(float duration, float magnitude)
        {
            _duration = duration;
            _magnitude = magnitude;
            _elapsed = 0f;
        }

        private void Update()
        {
            if (_elapsed >= _duration)
            {
                _currentOffset = Vector2.zero;
                return;
            }
            _elapsed += Time.deltaTime;
            float decay = 1f - (_elapsed / _duration);
            _currentOffset = Random.insideUnitCircle * _magnitude * decay;
        }

        /// <summary>Returns current shake offset. Pure read — no side effects.</summary>
        public Vector2 GetOffset()
        {
            return _currentOffset;
        }

        private void OnDisable()
        {
            _currentOffset = Vector2.zero;
            _elapsed = _duration;
        }
    }
}
