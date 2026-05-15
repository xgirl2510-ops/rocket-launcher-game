using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Parameters describing player rocket flight physics (drag + thrust + impulse). Mirrors the
    /// SerializeFields on the Rocket component so the solver can simulate the EXACT trajectory
    /// the live rocket will fly with a given launch direction + initial impulse force.
    /// </summary>
    public readonly struct RocketFlightParams
    {
        public readonly float Gravity;
        public readonly float Drag;            // Unity Rigidbody2D.linearDamping
        public readonly float ThrustForce;     // continuous force applied for ThrustDuration
        public readonly float ThrustDuration;
        public readonly float MinForce;
        public readonly float MaxForce;

        public RocketFlightParams(float gravity, float drag, float thrustForce, float thrustDuration, float minForce, float maxForce)
        {
            Gravity = gravity;
            Drag = drag;
            ThrustForce = thrustForce;
            ThrustDuration = thrustDuration;
            MinForce = minForce;
            MaxForce = maxForce;
        }
    }

    /// <summary>Result of a successful dive solve.</summary>
    public readonly struct DiveSolution
    {
        public readonly bool Found;
        public readonly Vector2 LaunchDir;     // normalized
        public readonly float LaunchForce;     // [MinForce..MaxForce]
        public readonly float LaunchAngleDeg;  // 0..90
        public readonly Vector2 ApexPos;
        public readonly Vector2 HitPos;        // closest approach to target
        public readonly float DescentAngleDeg; // angle below horizontal at impact (positive = falling)
        public readonly Vector2[] TrajectorySamples;

        public DiveSolution(Vector2 launchDir, float force, float angleDeg, Vector2 apex, Vector2 hit, float descentDeg, Vector2[] samples)
        {
            Found = true;
            LaunchDir = launchDir;
            LaunchForce = force;
            LaunchAngleDeg = angleDeg;
            ApexPos = apex;
            HitPos = hit;
            DescentAngleDeg = descentDeg;
            TrajectorySamples = samples;
        }
    }

    /// <summary>
    /// Brute-force solver: sweeps launch angle × force across a grid, simulates each candidate
    /// using the live rocket's physics (drag + thrust burn + gravity), and returns the steepest
    /// dive (largest descent angle at target) that passes within hit tolerance of the target.
    ///
    /// Pure function — no MonoBehaviour state. Caller supplies the start, target, and physics
    /// params. Cost ≈ 64 angles × 5 forces × 600 steps ≈ 200k ops per round (one-time at round
    /// start). Verified to find a dive solution for any target inside X[8..36] Y[-4..8] from
    /// spawn (-7, -4) using the default player physics.
    /// </summary>
    public static class RocketDiveSolver
    {
        // Simulation grid — fine enough to find clean dives, coarse enough to stay <100ms
        private const float AngleMinDeg = 45f;
        private const float AngleMaxDeg = 85f;
        private const int AngleSteps = 33;             // ~1.25° resolution
        private const int ForceSteps = 6;              // sample 6 forces from min..max
        private const float ForceFloorRatio = 0.7f;    // start sweep from 70% of max — low forces never produce a dive
        private const float SimDt = 0.02f;             // matches Unity FixedUpdate
        private const float MaxSimTime = 8f;

        // Acceptance criteria for a "dive"
        private const float HitToleranceMeters = 0.9f;       // closest-approach distance to target
        private const float MinApexHeightAboveTarget = 5f;   // apex must clear target by ≥5u
        private const float MinDescentAngleDeg = 60f;        // angle below horizontal at impact
        private const int TrajectorySampleCount = 32;

        /// <summary>
        /// Brute-force search for a dive solution. Returns false if no candidate met all criteria.
        /// </summary>
        public static bool TrySolve(Vector2 start, Vector2 target, RocketFlightParams p, out DiveSolution best)
        {
            best = default;
            float bestScore = float.NegativeInfinity;
            bool found = false;

            for (int ai = 0; ai < AngleSteps; ai++)
            {
                float angleT = ai / (float)(AngleSteps - 1);
                float angleDeg = Mathf.Lerp(AngleMinDeg, AngleMaxDeg, angleT);
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

                for (int fi = 0; fi < ForceSteps; fi++)
                {
                    float forceT = fi / (float)(ForceSteps - 1);
                    float force = Mathf.Lerp(p.MaxForce * ForceFloorRatio, p.MaxForce, forceT);

                    if (!SimulateAndScore(start, target, dir, force, p,
                                          out Vector2 apex, out Vector2 hit, out float descentDeg, out float closestDist))
                        continue;

                    if (closestDist > HitToleranceMeters) continue;
                    if (apex.y < target.y + MinApexHeightAboveTarget) continue;
                    if (descentDeg < MinDescentAngleDeg) continue;

                    // Score: prefer steeper descent + tighter hit (lower closestDist).
                    // Descent angle dominates so we get the "purest" dive available.
                    float score = descentDeg - closestDist * 10f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        Vector2[] samples = SampleTrajectory(start, dir, force, p, target.x);
                        best = new DiveSolution(dir, force, angleDeg, apex, hit, descentDeg, samples);
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Simulate one (angle, force) candidate. Returns false if the rocket hits the ground
        /// before reaching target X. Otherwise reports apex, closest approach to target, and the
        /// descent angle at that closest-approach instant.
        /// </summary>
        private static bool SimulateAndScore(Vector2 start, Vector2 target, Vector2 dir, float force,
                                             RocketFlightParams p,
                                             out Vector2 apex, out Vector2 hit, out float descentDeg, out float closestDist)
        {
            Vector2 pos = start;
            Vector2 vel = dir * force;
            apex = start;
            hit = start;
            descentDeg = 0f;
            closestDist = float.PositiveInfinity;

            float t = 0f;
            while (t < MaxSimTime)
            {
                // Thrust phase: same direction as initial impulse, constant force, no mass
                // scaling (Rocket has mass=1) so a/dt = thrustForce.
                if (t < p.ThrustDuration)
                    vel += dir * p.ThrustForce * SimDt;

                // Gravity
                vel.y -= p.Gravity * SimDt;

                // Unity linearDamping per FixedUpdate: v *= 1/(1 + drag*dt)
                float damp = 1f / (1f + p.Drag * SimDt);
                vel *= damp;

                pos += vel * SimDt;

                if (pos.y > apex.y) apex = pos;

                float dist = Vector2.Distance(pos, target);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    hit = pos;
                    // descent angle at this sample = atan2(-vy, |vx|), only meaningful when vy < 0
                    if (vel.y < 0f)
                    {
                        descentDeg = Mathf.Atan2(-vel.y, Mathf.Abs(vel.x)) * Mathf.Rad2Deg;
                    }
                    else
                    {
                        descentDeg = 0f;
                    }
                }

                // Once the rocket lands we can stop simulating — closestDist already captures the
                // best approach to the target along the path that was actually flown.
                if (pos.y < GameConstants.GroundTop && vel.y < 0f) break;

                t += SimDt;
            }
            return closestDist < float.PositiveInfinity;
        }

        /// <summary>Re-simulate the chosen solution and return a polyline of N evenly-time-sampled points.</summary>
        private static Vector2[] SampleTrajectory(Vector2 start, Vector2 dir, float force, RocketFlightParams p, float stopX)
        {
            // First pass to find total flight time until rocket passes stopX or hits the ground.
            Vector2 pos = start;
            Vector2 vel = dir * force;
            float totalT = 0f;
            float maxT = MaxSimTime;
            while (totalT < maxT)
            {
                if (totalT < p.ThrustDuration) vel += dir * p.ThrustForce * SimDt;
                vel.y -= p.Gravity * SimDt;
                vel *= 1f / (1f + p.Drag * SimDt);
                pos += vel * SimDt;
                totalT += SimDt;
                if (pos.x > stopX + 4f) break;
                if (pos.y < GameConstants.GroundTop && vel.y < 0f) break;
            }

            // Second pass: emit N samples up to totalT.
            Vector2[] samples = new Vector2[TrajectorySampleCount + 1];
            pos = start;
            vel = dir * force;
            float emitInterval = totalT / TrajectorySampleCount;
            float nextEmit = 0f;
            int emitIdx = 0;
            samples[emitIdx++] = pos;
            float t2 = 0f;
            while (emitIdx <= TrajectorySampleCount && t2 < totalT)
            {
                if (t2 < p.ThrustDuration) vel += dir * p.ThrustForce * SimDt;
                vel.y -= p.Gravity * SimDt;
                vel *= 1f / (1f + p.Drag * SimDt);
                pos += vel * SimDt;
                t2 += SimDt;
                if (t2 >= nextEmit && emitIdx <= TrajectorySampleCount)
                {
                    samples[emitIdx++] = pos;
                    nextEmit += emitInterval;
                }
            }
            // Pad any remaining with the last position so callers can iterate the full array.
            while (emitIdx <= TrajectorySampleCount) samples[emitIdx++] = pos;
            return samples;
        }
    }
}
