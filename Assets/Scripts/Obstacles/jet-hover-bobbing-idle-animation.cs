using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Gentle vertical bobbing for hovering jet obstacles — sells "actively flying" rather than
    /// "frozen in midair." Uses sine-based oscillation in Update with per-instance random
    /// amplitude/period/phase so a fleet of jets doesn't move in lockstep.
    ///
    /// Position is offset relative to the SPAWN position captured on Awake, so the hover
    /// motion does not drift over time.
    /// </summary>
    public class JetHoverAnimation : MonoBehaviour
    {
        // Vertical bob: small enough to read as "atmospheric drift," not a flight maneuver.
        private const float MinAmplitudeY = 0.10f;
        private const float MaxAmplitudeY = 0.22f;
        // Period in seconds for one full up-down cycle.
        private const float MinPeriod = 1.6f;
        private const float MaxPeriod = 2.6f;
        // Tiny tilt (degrees) so the nose pitches slightly with the bob — makes hover feel alive.
        private const float MaxTiltDegrees = 2.5f;

        private Vector3 _basePosition;
        private float _amplitudeY;
        private float _angularFreq;       // 2π / period
        private float _phase;             // random offset so instances desync
        private float _tiltAmplitude;

        private void Awake()
        {
            _basePosition = transform.position;
            _amplitudeY = Random.Range(MinAmplitudeY, MaxAmplitudeY);
            float period = Random.Range(MinPeriod, MaxPeriod);
            _angularFreq = 2f * Mathf.PI / period;
            _phase = Random.Range(0f, 2f * Mathf.PI);
            _tiltAmplitude = Random.Range(0.5f, MaxTiltDegrees);
        }

        private void Update()
        {
            float t = Time.time * _angularFreq + _phase;
            // Vertical bob — pure sine, anchored to spawn Y.
            float yOffset = Mathf.Sin(t) * _amplitudeY;
            transform.position = new Vector3(_basePosition.x, _basePosition.y + yOffset, _basePosition.z);

            // Slight tilt offset by 90° from the bob so nose pitches up at peak/bottom of climb.
            // This is what makes the motion read as "flying" not "elevator."
            float tiltDeg = Mathf.Cos(t) * _tiltAmplitude;
            transform.rotation = Quaternion.Euler(0f, 0f, tiltDeg);
        }
    }
}
