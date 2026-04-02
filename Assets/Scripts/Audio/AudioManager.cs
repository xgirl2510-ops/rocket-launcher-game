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
        }

        public void PlayLaunch()
        {
            if (_launchClip != null) _oneShotSource.PlayOneShot(_launchClip);
        }

        public void PlayHitGround()
        {
            if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip);
            else if (_groundHitClip != null) _oneShotSource.PlayOneShot(_groundHitClip);
        }

        public void PlayHitTarget()
        {
            if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip);
            else if (_groundHitClip != null) _oneShotSource.PlayOneShot(_groundHitClip);
        }

        public void PlayStretch() { if (_stretchClip != null) _oneShotSource.PlayOneShot(_stretchClip); }
        public void PlayClick() { if (_clickClip != null) _oneShotSource.PlayOneShot(_clickClip); }
        public void PlayWin() { if (_winClip != null) _oneShotSource.PlayOneShot(_winClip); }

        public void StartThrust()
        {
            if (_thrustSource.isPlaying) return;
            if (_thrustClip == null) return;
            _thrustSource.clip = _thrustClip;
            _thrustSource.Play();
        }

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
        }
    }
}
