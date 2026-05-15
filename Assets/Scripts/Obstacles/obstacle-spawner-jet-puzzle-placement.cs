using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Partial of ObstacleSpawner: jet placement logic for the dive puzzle.
    ///
    /// Layout order (so later phases see earlier jets via AABB overlap):
    ///   PHASE A — Cap pair above target (mandatory). Slot is exactly wide enough for the dive
    ///             trajectory's near-vertical descent, no wider.
    ///   PHASE B — Shield jet in front of target at target altitude (blocks flat shots).
    ///   PHASE C — Tail jet behind target (blocks overshoots).
    ///   PHASE D — Fill remaining jets randomly, avoiding the dive corridor AND the cap slot.
    ///
    /// Caller (RespawnObstacles) supplies the per-round random scale + jet count.
    /// </summary>
    public partial class ObstacleSpawner
    {
        private void PlacePuzzleJets(Vector2 start, Vector2 target, float scale, int jetCount)
        {
            int placed = 0;
            placed += PlaceCapSlotAboveTarget(target, scale);
            placed += PlaceShieldAheadOfTarget(start, target, scale);
            placed += PlaceTailBehindTarget(start, target, scale);

            int remaining = Mathf.Max(0, jetCount - placed);
            PlaceFillerJetsAvoidingDive(start, target, scale, remaining);
        }

        /// <summary>
        /// Two jets directly above the target. The gap between their inner edges == cap slot width
        /// = capSlotHalfRocketWidths * 2 * rocketDiameter. The dive trajectory was solved with apex
        /// ≥5u above target so it falls vertically through this slot.
        /// </summary>
        private int PlaceCapSlotAboveTarget(Vector2 target, float scale)
        {
            float jetHalfWidth = SpriteWidthAtScale1 * scale * 0.5f;
            float slotHalf = _capSlotHalfRocketWidths * RocketDiameterEstimate;
            float capY = target.y + _capHeightAboveTarget;
            // Jet centre = target.x ± (slotHalf + jetHalfWidth) so the gap between inner edges
            // exactly equals 2 * slotHalf.
            Vector2 left = new Vector2(target.x - slotHalf - jetHalfWidth, capY);
            Vector2 right = new Vector2(target.x + slotHalf + jetHalfWidth, capY);
            int placed = 0;
            if (TrySpawnAt(left, scale)) { DisableInterceptorBoostOnLastSpawned(); placed++; }
            if (TrySpawnAt(right, scale)) { DisableInterceptorBoostOnLastSpawned(); placed++; }
            return placed;
        }

        /// <summary>
        /// Cap jets miss intentionally — flag the most-recently-spawned jet's interceptor so it
        /// never enters boost mode. Called immediately after TrySpawnAt succeeds for a cap jet.
        /// </summary>
        private void DisableInterceptorBoostOnLastSpawned()
        {
            if (_obstacles.Count == 0) return;
            var last = _obstacles[_obstacles.Count - 1];
            if (last == null) return;
            var launcher = last.GetComponent<JetInterceptorLauncher>();
            if (launcher != null) launcher.SetBoostEnabled(false);
        }

        /// <summary>
        /// Shield jet planted between launcher and target at target altitude. Kills flat-trajectory
        /// shots — the rocket must arc over it. Direction = sign of (target.x - start.x) so the
        /// shield always sits AHEAD (closer to launcher) than the target.
        /// </summary>
        private int PlaceShieldAheadOfTarget(Vector2 start, Vector2 target, float scale)
        {
            float dir = Mathf.Sign(target.x - start.x);
            if (Mathf.Approximately(dir, 0f)) dir = 1f;
            Vector2 pos = new Vector2(target.x - dir * _shieldDistanceAhead, target.y);
            return TrySpawnAt(pos, scale) ? 1 : 0;
        }

        /// <summary>
        /// Tail jet behind the target (further from launcher) at target altitude. Catches shots
        /// that overshoot and ensures the only approach is the dive from above.
        /// </summary>
        private int PlaceTailBehindTarget(Vector2 start, Vector2 target, float scale)
        {
            float dir = Mathf.Sign(target.x - start.x);
            if (Mathf.Approximately(dir, 0f)) dir = 1f;
            Vector2 pos = new Vector2(target.x + dir * _tailDistanceBehind, target.y);
            return TrySpawnAt(pos, scale) ? 1 : 0;
        }

        /// <summary>
        /// Fill up to "remaining" jets at random positions. A candidate is rejected if:
        ///   • Outside the spawn rectangle.
        ///   • Inside the dive corridor (would intercept the auto-play / solved dive).
        ///   • Overlapping any already-placed jet (cap, shield, tail, or prior filler).
        ///   • Inside the cap slot above the target (would close the dive entry window).
        /// </summary>
        private void PlaceFillerJetsAvoidingDive(Vector2 start, Vector2 target, float scale, int remaining)
        {
            if (remaining <= 0) return;
            float minX = start.x + _spawnPaddingX;
            float maxX = target.x - _spawnPaddingX;
            // If the target is too close to the launcher for any filler corridor, skip silently.
            if (minX >= maxX) return;

            int maxAttempts = remaining * FillerAttemptsPerJet;
            int placed = 0;
            for (int i = 0; i < maxAttempts && placed < remaining; i++)
            {
                float x = Random.Range(minX, maxX);
                float y = Random.Range(_spawnMinY, _spawnMaxY);
                Vector2 candidate = new Vector2(x, y);

                if (IsInsideDiveCorridor(candidate)) continue;
                if (IsInsideCapSlot(candidate, target)) continue;
                if (TrySpawnAt(candidate, scale)) placed++;
            }
        }

        /// <summary>
        /// True if the candidate is within DiveCorridorRadius of any sample along the dive trajectory.
        /// Filler jets must stay outside this corridor so the solver's dive line is always clear.
        /// </summary>
        private bool IsInsideDiveCorridor(Vector2 candidate)
        {
            if (_lastDive.TrajectorySamples == null) return false;
            float rSqr = DiveCorridorRadius * DiveCorridorRadius;
            foreach (var p in _lastDive.TrajectorySamples)
            {
                if ((candidate - p).sqrMagnitude < rSqr) return true;
            }
            return false;
        }

        /// <summary>
        /// True if the candidate sits inside the vertical column above the target (between the cap
        /// jets), where another filler would close the dive entry slot.
        /// </summary>
        private bool IsInsideCapSlot(Vector2 candidate, Vector2 target)
        {
            float slotHalfX = _capSlotHalfRocketWidths * RocketDiameterEstimate + SpriteWidthAtScale1 * _obstacleMaxSize * 0.6f;
            if (Mathf.Abs(candidate.x - target.x) > slotHalfX) return false;
            // Forbid the whole column from cap altitude DOWN to target — keeps the dive slot clear.
            return candidate.y > target.y - 0.5f && candidate.y < target.y + _capHeightAboveTarget + 2f;
        }
    }
}
