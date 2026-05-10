using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Freezes / unfreezes the simulation WITHOUT touching Time.timeScale, so Unity UI
    /// (Button clicks via EventSystem) keeps responding while gameplay sits still.
    ///
    /// What we pause:
    ///   • All Rigidbody2D — set simulated=false (stops physics, preserves velocity state)
    ///   • AudioListener — pauses every AudioSource without touching individual sources
    ///
    /// What we INTENTIONALLY do NOT pause:
    ///   • ParticleSystem — letting explosion/smoke/fire play through is the whole point
    ///     of the game-over moment. Pausing particles in the same frame they spawn freezes
    ///     them invisibly (PS hasn't rendered yet) and the player sees no explosion at all.
    ///     Each effect self-destructs on its own timer, so leaving them running is safe.
    ///
    /// Why not Time.timeScale=0: Unity's StandaloneInputModule has known issues processing
    /// click events when timeScale=0 in some configurations, leaving UI unresponsive.
    /// Pausing per-object keeps Time running so EventSystem.Update() ticks normally.
    /// </summary>
    public static class WorldPauseController
    {
        private static bool _frozen;

        /// <summary>True between Freeze() and Unfreeze() calls.</summary>
        public static bool IsFrozen => _frozen;

        /// <summary>Freeze all 2D physics + audio. Particles run free. Idempotent.</summary>
        public static void Freeze()
        {
            if (_frozen) return;
            _frozen = true;

            foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                rb.simulated = false;

            AudioListener.pause = true;
        }

        /// <summary>Restore physics + audio. Idempotent.</summary>
        public static void Unfreeze()
        {
            if (!_frozen) return;
            _frozen = false;

            foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                rb.simulated = true;

            AudioListener.pause = false;
        }

        /// <summary>Reset on Unity domain reload — defensive against stuck-frozen state.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _frozen = false;
            AudioListener.pause = false;
        }
    }
}
