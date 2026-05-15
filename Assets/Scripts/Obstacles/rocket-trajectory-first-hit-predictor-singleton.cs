using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// One-shot first-hit predictor. Called by LaunchController the moment the rocket leaves
    /// the slingshot, with the exact launch direction + force (so we don't depend on physics
    /// having stepped yet). We simulate a parabolic arc from spawn position with that v0,
    /// find the first registered jet whose DetectionRange the arc enters, and tell THAT jet
    /// to fire its interceptor immediately.
    ///
    /// No per-frame work — fully event-driven.
    /// </summary>
    public class RocketTrajectoryPredictor : MonoBehaviour
    {
        // Trajectory simulation resolution. dt = 8/2400 ≈ 0.0033s → 0.1u per step at v=30.
        // High enough that consecutive samples can be linecast as a swept segment without
        // tunnelling through small jet colliders.
        private const int ArcSteps = 2400;
        private const float MaxPredictTime = 8f;

        private static RocketTrajectoryPredictor _instance;
        public static RocketTrajectoryPredictor Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[RocketTrajectoryPredictor]");
                    _instance = go.AddComponent<RocketTrajectoryPredictor>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        private readonly HashSet<JetInterceptorLauncher> _jets = new HashSet<JetInterceptorLauncher>();

        // Pending victim: predictor flagged this jet at launch, but the jet is still off-screen.
        // Each frame we re-check visibility; once the camera reveals it, the interceptor fires.
        // Cleared when fired OR when the rocket flight ends.
        private JetInterceptorLauncher _pendingVictim;
        private Vector2 _pendingRendezvous;
        private float _pendingTimeToRendezvous;
        private float _pendingFlagTime;          // Time.time when flagged (used to age out the rendezvous estimate)
        private bool _pendingAscending;
        private Rocket _playerRocket;
        private Camera _mainCamera;

        // Buffer reused by CircleCast to avoid per-step heap alloc inside the predict loop.
        // Sized to comfortably cover the jet count we ever spawn (~20) plus collider neighbours.
        private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];
        // Contact filter for the unfiltered CircleCast (matches the unconditional behaviour
        // the deprecated CircleCastAll/CircleCastNonAlloc had). Initialised at first use.
        private ContactFilter2D _hitFilter;
        private bool _hitFilterReady;

        public void Register(JetInterceptorLauncher jet) => _jets.Add(jet);
        public void Unregister(JetInterceptorLauncher jet)
        {
            _jets.Remove(jet);
            if (_pendingVictim == jet) ClearPending();
        }

        /// <summary>
        /// Called by LaunchController immediately after Rocket.Launch(). Predicts the arc
        /// from launchPos with v0 = direction × force; if it enters a jet's DetectionRange,
        /// that jet fires its interceptor right away with the rendezvous point.
        /// </summary>
        public void OnRocketLaunched(Vector2 launchPos, Vector2 launchDirection, float launchForce)
        {
            ClearPending();
            // Defer the heavy arc simulation (2400 swept CircleCasts) to NEXT frame so the
            // launch frame itself only pays for Rocket.AddForce + audio + trail spawn. Without
            // this defer, the predictor work piles onto the same frame as the impulse and the
            // player sees a visible hitch the instant the rocket leaves the slingshot.
            StartCoroutine(PredictNextFrame(launchPos, launchDirection, launchForce));
        }

        private IEnumerator PredictNextFrame(Vector2 launchPos, Vector2 launchDirection, float launchForce)
        {
            yield return null;

            Vector2 v0 = launchDirection * launchForce;   // mass = 1, so velocity == impulse
            var hit = FindFirstJetInRange(launchPos, v0, out Vector2 rendezvous, out float timeToRendezvous, out bool isAscending);

            if (hit == null) yield break;

            // Stage as pending — actual fire happens in Update() the first frame this jet is
            // FULLY visible inside the camera viewport. Player must see the threatening jet
            // before its interceptor launches, regardless of direct/lobbing classification.
            _pendingVictim = hit;
            _pendingRendezvous = rendezvous;
            _pendingTimeToRendezvous = timeToRendezvous;
            _pendingAscending = isAscending;
            _pendingFlagTime = Time.time;
        }

        private void Update()
        {
            if (_pendingVictim == null) return;

            // Cancel pending if rocket landed / no longer flying — no interceptor for next launch.
            if (_playerRocket == null) _playerRocket = Object.FindAnyObjectByType<Rocket>();
            if (_playerRocket != null && !_playerRocket.IsFlying)
            {
                ClearPending();
                return;
            }

            // Fire condition differs by shot type:
            //   • DIRECT (ascending — lao thẳng): fire as soon as rocket enters jet's DetectionRange.
            //     No visibility gate — head-on attack reacts purely on threat distance.
            //   • LOBBING (descending — bổ nhào): fire only once the jet is fully visible so the
            //     player can see the threat before its interceptor launches.
            bool shouldFire;
            if (_pendingAscending)
            {
                float detectSqr = JetInterceptorLauncher.DetectionRange * JetInterceptorLauncher.DetectionRange;
                Vector2 jetPos = _pendingVictim.transform.position;
                Vector2 rocketPos = _playerRocket != null ? (Vector2)_playerRocket.transform.position : jetPos;
                shouldFire = (rocketPos - jetPos).sqrMagnitude <= detectSqr;
            }
            else
            {
                shouldFire = IsJetFullyOnScreen(_pendingVictim);
            }
            if (!shouldFire) return;

            // Adjust timeToRendezvous by the delay we waited so the missile gets the remaining
            // time budget instead of the original (which already partially elapsed).
            float delay = Time.time - _pendingFlagTime;
            float adjustedTime = Mathf.Max(0.1f, _pendingTimeToRendezvous - delay);

            _pendingVictim.OnFlaggedAsVictim(_pendingRendezvous, adjustedTime, _pendingAscending);
            ClearPending();
        }

        private void ClearPending()
        {
            _pendingVictim = null;
            _pendingRendezvous = Vector2.zero;
            _pendingTimeToRendezvous = 0f;
            _pendingFlagTime = 0f;
            _pendingAscending = false;
        }

        /// <summary>
        /// True if the jet's ENTIRE sprite footprint sits inside the camera viewport — every
        /// corner of the SpriteRenderer.bounds projects within [0..1]. Used so the interceptor
        /// only fires when the player can clearly see the threatening jet, not when only its
        /// edge is peeking on-screen.
        /// </summary>
        private bool IsJetFullyOnScreen(JetInterceptorLauncher jet)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            var cam = _mainCamera;
            if (cam == null) return true;   // safety: no camera → don't gate
            var sr = jet.GetComponent<SpriteRenderer>();
            // Fall back to point-test if no renderer (shouldn't happen for jets).
            if (sr == null)
            {
                Vector3 vp = cam.WorldToViewportPoint(jet.transform.position);
                return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
            }

            Bounds b = sr.bounds;
            // Check the four corners of the AABB — all must project inside the viewport.
            Vector3[] corners =
            {
                new Vector3(b.min.x, b.min.y, b.center.z),
                new Vector3(b.max.x, b.min.y, b.center.z),
                new Vector3(b.min.x, b.max.y, b.center.z),
                new Vector3(b.max.x, b.max.y, b.center.z),
            };
            for (int i = 0; i < 4; i++)
            {
                Vector3 vp = cam.WorldToViewportPoint(corners[i]);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return false;
            }
            return true;
        }

        /// <summary>
        /// Walk the predicted arc using the SAME physics the rocket experiences (impulse + thrust
        /// + drag + gravity). At each step we sweep a circle from prevPos→curPos and pick the
        /// jet collider hit closest along the segment — that's the rocket's first physical
        /// rendezvous with a defender.
        /// </summary>
        private JetInterceptorLauncher FindFirstJetInRange(Vector2 pos, Vector2 vel, out Vector2 rendezvous, out float time, out bool isAscending)
        {
            const float g = 9.81f;
            // Must match Rocket._thrustDuration / _thrustForce / _airDrag exactly so prediction
            // matches the rocket's real flight path.
            const float thrustDuration = 0.6f;
            const float thrustForce = 12f;
            const float drag = 0.4f;

            float dt = MaxPredictTime / ArcSteps;

            Vector2 thrustDir = vel.sqrMagnitude > 0.0001f ? vel.normalized : Vector2.right;

            Vector2 simPos = pos;
            Vector2 simVel = vel;
            float thrustRemaining = thrustDuration;

            // Track the time at which the arc transitions from ASCENDING to DESCENDING (i.e.
            // when vy crosses zero — the apex of the arc). Impacts BEFORE peakTime live on the
            // "first leg" of the arc (lao thẳng); impacts AFTER live on the "second leg" (bổ nhào).
            // Initialized to +∞ → if vy never goes negative within the predict window, every
            // impact counts as ascending.
            float peakTime = float.PositiveInfinity;

            for (int i = 1; i <= ArcSteps; i++)
            {
                Vector2 prevPos = simPos;
                float prevVy = simVel.y;

                if (thrustRemaining > 0f)
                {
                    simVel += thrustDir * thrustForce * dt;
                    thrustRemaining -= dt;
                }
                simVel.y -= g * dt;
                float damp = 1f / (1f + drag * dt);
                simVel *= damp;
                simPos += simVel * dt;

                if (simPos.y < GameConstants.GroundTop) break;

                float t = dt * i;

                // Capture apex transition (vy crossed from + to -). First crossing wins.
                if (float.IsPositiveInfinity(peakTime) && prevVy > 0f && simVel.y <= 0f)
                    peakTime = t;

                // Swept CIRCLE against ALL physics colliders along the prevPos→simPos
                // segment. CircleCast uses a radius matching the rocket's collider half-width
                // (~0.3u) so "near miss" arcs that graze the jet silhouette get caught — pure
                // Linecast would miss them because the rocket's actual flight (drag/thrust may
                // shift by tiny amounts vs. prediction) ends up clipping the jet at the edge.
                JetInterceptorLauncher closestHit = null;
                Vector2 closestHitPoint = Vector2.zero;
                Vector2 segVec = simPos - prevPos;
                float segLen = segVec.magnitude;
                if (segLen > 0.0001f)
                {
                    const float RocketRadius = 0.35f;
                    Vector2 dir = segVec / segLen;
                    // CircleCast(filter, results) reuses _hitBuffer instead of allocating a
                    // fresh RaycastHit2D[] every step (would be 2400 allocs/launch otherwise).
                    if (!_hitFilterReady)
                    {
                        _hitFilter = ContactFilter2D.noFilter;
                        _hitFilter.useTriggers = true;
                        _hitFilterReady = true;
                    }
                    int count = Physics2D.CircleCast(prevPos, RocketRadius, dir, _hitFilter, _hitBuffer, segLen);
                    float bestFrac = float.MaxValue;
                    for (int h = 0; h < count; h++)
                    {
                        var hitJet = _hitBuffer[h].collider.GetComponent<JetInterceptorLauncher>();
                        if (hitJet == null || !_jets.Contains(hitJet)) continue;
                        if (_hitBuffer[h].fraction < bestFrac)
                        {
                            bestFrac = _hitBuffer[h].fraction;
                            closestHit = hitJet;
                            closestHitPoint = _hitBuffer[h].point;
                        }
                    }
                }

                if (closestHit != null)
                {
                    // Rendezvous = the SILHOUETTE-IMPACT point itself, NOT the earlier range-entry.
                    // Reason: missile must meet rocket where rocket would physically collide with
                    // the jet, otherwise it intercepts too early/high and rocket keeps falling
                    // through the DetectionRange to kill the jet anyway.
                    // Time is pulled back ~0.05s so the missile arrives a hair BEFORE silhouette
                    // contact — leaves room for chase + kill to detonate the rocket before it
                    // physically rams the jet.
                    rendezvous = closestHitPoint;
                    time = Mathf.Max(0.05f, t - 0.05f);
                    // Ascending = impact happens on the FIRST leg of the arc, before the apex.
                    isAscending = t < peakTime;
                    return closestHit;
                }
            }
            rendezvous = Vector2.zero;
            time = 0f;
            isAscending = false;
            return null;
        }
    }
}
