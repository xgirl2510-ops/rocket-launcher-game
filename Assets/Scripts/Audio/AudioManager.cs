using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Singleton that manages all game audio.
    /// Uses mp3 files for launch/flight/boom, procedural clips for UI sounds and win jingle.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        private const float TargetHitPitchMultiplier = 1.3f;

        /// <summary>Singleton instance, set in Awake. Null if no AudioManager in scene.</summary>
        public static AudioManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        [Header("Audio Clips (assign via inspector or editor tool)")]
        [SerializeField] private AudioClip _launchClip;
        [SerializeField] private AudioClip _thrustClip;
        [SerializeField] private AudioClip _boomClip;

        private AudioSource _oneShotSource;
        private AudioSource _thrustSource;

        private AudioClip _winClip;
        private AudioClip _stretchClip;
        private AudioClip _clickClip;
        private AudioClip _groundHitClip;
        private AudioClip _targetHitClip;

        // Single-scene only — no DontDestroyOnLoad by design
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _oneShotSource = gameObject.AddComponent<AudioSource>();
            _oneShotSource.playOnAwake = false;

            _thrustSource = gameObject.AddComponent<AudioSource>();
            _thrustSource.playOnAwake = false;
            _thrustSource.loop = true;

            _winClip = ProceduralAudioClipGenerator.CreateWinJingle();
            _stretchClip = ProceduralAudioClipGenerator.CreateStretch();
            _clickClip = ProceduralAudioClipGenerator.CreateClick();
            _groundHitClip = ProceduralAudioClipGenerator.CreateGroundHit();
            _targetHitClip = ProceduralAudioClipGenerator.CreateTargetHit();
        }

        /// <summary>Play rocket launch sound effect.</summary>
        public void PlayLaunch()
        {
            _oneShotSource.pitch = 1.0f;
            if (_launchClip != null) _oneShotSource.PlayOneShot(_launchClip);
        }

        /// <summary>Play ground impact explosion at normal pitch.</summary>
        public void PlayHitGround()
        {
            _oneShotSource.pitch = 1.0f;
            if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip);
            else if (_groundHitClip != null) _oneShotSource.PlayOneShot(_groundHitClip);
        }

        /// <summary>Play target impact explosion at higher pitch.</summary>
        public void PlayHitTarget()
        {
            _oneShotSource.pitch = TargetHitPitchMultiplier;
            if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip);
            else if (_targetHitClip != null) _oneShotSource.PlayOneShot(_targetHitClip);
        }

        /// <summary>Play slingshot stretch feedback sound.</summary>
        public void PlayStretch() { _oneShotSource.pitch = 1.0f; if (_stretchClip != null) _oneShotSource.PlayOneShot(_stretchClip); }
        /// <summary>Play UI button click sound.</summary>
        public void PlayClick() { _oneShotSource.pitch = 1.0f; if (_clickClip != null) _oneShotSource.PlayOneShot(_clickClip); }
        /// <summary>Play win jingle after hitting the target.</summary>
        public void PlayWin() { _oneShotSource.pitch = 1.0f; if (_winClip != null) _oneShotSource.PlayOneShot(_winClip); }

        /// <summary>Start looping thrust audio while rocket is in flight.</summary>
        public void StartThrust()
        {
            if (_thrustSource.isPlaying) return;
            if (_thrustClip == null) return;
            _thrustSource.clip = _thrustClip;
            _thrustSource.Play();
        }

        /// <summary>Stop the thrust loop audio.</summary>
        public void StopThrust()
        {
            _thrustSource.Stop();
        }

        private void OnDestroy()
        {
            if (_winClip != null) Destroy(_winClip);
            if (_stretchClip != null) Destroy(_stretchClip);
            if (_clickClip != null) Destroy(_clickClip);
            if (_groundHitClip != null) Destroy(_groundHitClip);
            if (_targetHitClip != null) Destroy(_targetHitClip);
        }
    }
}
