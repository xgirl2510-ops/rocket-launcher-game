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

        /// <summary>Start a new screen shake. Replaces any in-progress shake.</summary>
        public void Shake(float duration, float magnitude)
        {
            _duration = duration;
            _magnitude = magnitude;
            _elapsed = 0f;
        }

        /// <summary>
        /// Returns the current shake offset (decays linearly to zero).
        /// Call once per frame from the camera positioning code.
        /// </summary>
        public Vector2 GetOffset()
        {
            if (_elapsed >= _duration) return Vector2.zero;

            _elapsed += Time.deltaTime;
            float decay = 1f - (_elapsed / _duration);
            return Random.insideUnitCircle * _magnitude * decay;
        }
    }
}
