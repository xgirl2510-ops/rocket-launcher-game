using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Defensive interceptor missile fired by a jet. Cinematic flow:
    ///
    ///   1. DROP — missile drops from the jet's belly and flies a short stretch in the jet's
    ///      forward direction (like a real munition release before its engine kicks in).
    ///   2. CURVE — heading bends smoothly toward the rocket's tail (behind the rocket along
    ///      its velocity vector). Capped turn-rate so the curve reads as a guided turn.
    ///   3. CHASE — pure pursuit from behind the rocket at fixed speed (matching rocket).
    ///      Detonates on contact when the rocket is inside the owning jet's DetectionRange.
    ///
    /// Only Initialize(target, rendezvous, time, jetPos, range, ascending, jetForward) is supported
    /// going forward; legacy overloads remain for back-compat (use sane defaults).
    ///
    /// Sprite: Assets/Sprites/Generated/rk.png (1591×410, nose pointing LEFT in raw PNG → +180°).
    /// </summary>
    public class InterceptorMissile : MonoBehaviour
    {
        public const float DefaultScale = 0.04f;

        // -------- Phase 1: Drop --------
        private const float DropSpeed = 8f;
        private const float DropDuration = 0.25f;       // shorter so chase has time to engage
        private const float DropBellyOffsetY = -0.4f;
        // If predicted time-to-rendezvous is below this threshold, SKIP drop + curve and chase
        // immediately. Set higher than the combined drop+curve duration so cinematic only runs
        // when there's comfortable budget left for the chase to actually catch the rocket.
        private const float CinematicMinBudget = 2.0f;

        // -------- Phase 2: Curve --------
        private const float CurveTurnRateDeg = 360f;   // how fast heading rotates toward "behind rocket"
        private const float CurveSpeed = 14f;          // constant speed during curve

        // -------- Phase 3: Chase --------
        // Faster than rocket peak speed (~30 m/s) so the chase can actually close the gap
        // when the missile starts behind the rocket. With chase speed = rocket speed the
        // distance stayed constant and the rocket exited DetectionRange before contact.
        private const float ChaseSpeed = 35f;
        // BOOST: applied while rocket is inside owner jet's DetectionRange — last-second
        // burst so missile is guaranteed to catch the rocket before it escapes the range.
        private const float ChaseBoostSpeed = 55f;
        private const float ChaseTurnRateDeg = 720f;
        private const float KillRadius = 0.7f;

        // -------- World bounds --------
        private const float GroundFloorBuffer = 0.2f;

        // -------- Lifecycle --------
        private const float MaxLifetime = 8f;

        // Configuration
        private Transform _target;             // player rocket transform
        private Vector2 _jetPos;
        private float _detectionRange;
        private float _detectionRangeSqr;
        private bool _directShot;              // ascending impact → kill anywhere
        private bool _boostEnabled = true;     // false on cap jets — they cruise at ChaseSpeed even in range

        // Phase state
        private enum Phase { Drop, Curve, Chase }
        private Phase _phase;
        private float _phaseStartTime;
        private Vector2 _heading;              // current unit direction of travel
        private bool _hasHit;

        // ---- Initialize ----

        public void Initialize(Transform target, Vector2 rendezvous, float timeToRendezvous,
                               Vector2 jetPos, float detectionRange, bool rocketAscending, Vector2 jetForward,
                               bool boostEnabled)
        {
            _target = target;
            _jetPos = jetPos;
            _detectionRange = detectionRange;
            _detectionRangeSqr = detectionRange * detectionRange;
            _directShot = rocketAscending;
            _boostEnabled = boostEnabled;

            // Start at the jet's belly (slight Y offset down from centre).
            transform.position = new Vector3(jetPos.x, jetPos.y + DropBellyOffsetY, transform.position.z);

            // DIRECT SHOT — rocket is barreling straight at the jet. Skip the cinematic drop +
            // curve choreography; aim the missile straight at the rocket and chase from the
            // first frame so it actually has a chance to hit before the rocket arrives.
            //
            // Also skip drop+curve for LOBBING shots whose remaining time budget is too short
            // to spend on cinematic phases. Without this, Drop's fixed 0.4s + Curve's turn time
            // consume most of the budget and the rocket reaches the jet first. The threshold
            // (CinematicMinBudget) gives the typical 3-5s lobbing shot the full choreography
            // while emergency-short shots go straight to chase.
            bool skipCinematic = _directShot || timeToRendezvous < CinematicMinBudget;
            if (skipCinematic && target != null)
            {
                Vector2 toRocket = (Vector2)target.position - (Vector2)transform.position;
                _heading = toRocket.sqrMagnitude > 0.0001f ? toRocket.normalized : Vector2.left;
                _phase = Phase.Chase;
            }
            else
            {
                // LOBBING SHOT — drop from belly first, then curve toward rocket's tail, then chase.
                _heading = jetForward.sqrMagnitude > 0.0001f ? jetForward.normalized : Vector2.left;
                _phase = Phase.Drop;
            }
            _phaseStartTime = Time.time;
            ApplySpriteRotation(_heading);
        }

        // Legacy overloads — use jet forward = world-left (matches protector.png nose-left default).
        public void Initialize(Transform target, Vector2 rendezvous, float timeToRendezvous,
                               Vector2 jetPos, float detectionRange, bool rocketAscending, Vector2 jetForward)
        {
            Initialize(target, rendezvous, timeToRendezvous, jetPos, detectionRange, rocketAscending, jetForward, boostEnabled: true);
        }
        public void Initialize(Transform target, Vector2 rendezvous, float timeToRendezvous,
                               Vector2 jetPos, float detectionRange, bool rocketAscending)
        {
            Initialize(target, rendezvous, timeToRendezvous, jetPos, detectionRange, rocketAscending, Vector2.left, boostEnabled: true);
        }
        public void Initialize(Transform target, Vector2 rendezvous, float timeToRendezvous)
        {
            Initialize(target, rendezvous, timeToRendezvous, (Vector2)transform.position,
                       JetInterceptorLauncher.DetectionRange, false, Vector2.left);
        }
        public void Initialize(Transform target)
        {
            Initialize(target, target != null ? (Vector2)target.position : (Vector2)transform.position, 0f);
        }

        // ---- Update loop ----

        private void Update()
        {
            if (_hasHit) return;

            if (_target == null || !_target.gameObject.activeInHierarchy
                || Time.time - _phaseStartTime > MaxLifetime + DropDuration)
            {
                Destroy(gameObject);
                return;
            }

            switch (_phase)
            {
                case Phase.Drop:  DropStep();  break;
                case Phase.Curve: CurveStep(); break;
                case Phase.Chase: ChaseStep(); break;
            }

            ClampInsideViewport();
        }

        // ---- Phase 1: Drop ----

        private void DropStep()
        {
            // Move along current heading (= jet forward) for the drop duration.
            transform.position += (Vector3)(_heading * DropSpeed * Time.deltaTime);
            ApplySpriteRotation(_heading);

            if (Time.time - _phaseStartTime >= DropDuration)
            {
                _phase = Phase.Curve;
                _phaseStartTime = Time.time;
            }
        }

        // ---- Phase 2: Curve ----

        /// <summary>
        /// Bend heading toward a point BEHIND the rocket (along -rocketVelocity), capped by
        /// CurveTurnRateDeg/sec so the turn is visibly smooth. Transitions to Chase once the
        /// heading is close to the desired "tail" direction.
        /// </summary>
        private void CurveStep()
        {
            Vector2 desiredDir = ComputeBehindRocketHeading();

            float curAng = Mathf.Atan2(_heading.y, _heading.x) * Mathf.Rad2Deg;
            float wantAng = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg;
            float maxStep = CurveTurnRateDeg * Time.deltaTime;
            float newAng = Mathf.MoveTowardsAngle(curAng, wantAng, maxStep);
            float rad = newAng * Mathf.Deg2Rad;
            _heading = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            transform.position += (Vector3)(_heading * CurveSpeed * Time.deltaTime);
            ApplySpriteRotation(_heading);

            // Done curving once heading aligned with desired direction (within 8°).
            if (Mathf.Abs(Mathf.DeltaAngle(curAng, wantAng)) < 8f)
            {
                _phase = Phase.Chase;
                _phaseStartTime = Time.time;
            }
        }

        /// <summary>
        /// Heading from missile pointing toward a spot just behind the rocket along its current
        /// velocity vector — so when chase begins, we're already on the rocket's tail.
        /// </summary>
        private Vector2 ComputeBehindRocketHeading()
        {
            Vector2 rocketPos = _target.position;
            var rb = _target.GetComponent<Rocket>();
            Vector2 rocketVel = rb != null ? rb.LinearVelocity : Vector2.zero;
            Vector2 velDir = rocketVel.sqrMagnitude > 0.01f ? rocketVel.normalized : Vector2.right;

            // Tail point = rocket's current position minus a small lead distance along its velocity.
            Vector2 tailPoint = rocketPos - velDir * 1.5f;
            Vector2 toTail = tailPoint - (Vector2)transform.position;
            return toTail.sqrMagnitude > 0.0001f ? toTail.normalized : velDir;
        }

        // ---- Phase 3: Chase ----

        private void ChaseStep()
        {
            Vector2 toRocket = (Vector2)_target.position - (Vector2)transform.position;
            float dist = toRocket.magnitude;

            if (dist > 0.0001f)
            {
                Vector2 desiredDir = toRocket / dist;
                if (_directShot)
                {
                    // Direct shot — snap heading hard at the rocket every frame so the missile
                    // flies in a straight intercept line, no soft homing curve. Reads as a
                    // "fire-along-the-line" point defense response to a head-on attack.
                    _heading = desiredDir;
                }
                else
                {
                    // Lobbing shot — capped turn-rate gives a visible homing curve.
                    float curAng = Mathf.Atan2(_heading.y, _heading.x) * Mathf.Rad2Deg;
                    float wantAng = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg;
                    float maxStep = ChaseTurnRateDeg * Time.deltaTime;
                    float newAng = Mathf.MoveTowardsAngle(curAng, wantAng, maxStep);
                    float rad = newAng * Mathf.Deg2Rad;
                    _heading = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                }
            }

            // Speed boost: when rocket is inside owner jet's DetectionRange, switch from cruise
            // to boost speed so the missile closes the gap fast enough to detonate before the
            // rocket escapes the range. Direct shots already kill anywhere so they don't need it.
            // Cap jets (above target) have _boostEnabled=false — they intentionally miss so the
            // player's dive can still slip through the slot between them.
            float rocketDistFromJetSqr = ((Vector2)_target.position - _jetPos).sqrMagnitude;
            bool rocketInRange = rocketDistFromJetSqr <= _detectionRangeSqr;
            bool shouldBoost = _boostEnabled && !_directShot && rocketInRange;
            float speed = shouldBoost ? ChaseBoostSpeed : ChaseSpeed;
            Vector2 nextPos = (Vector2)transform.position + _heading * speed * Time.deltaTime;

            // Detonation gate: rocket must be inside DetectionRange (lobbing shots) OR direct shot.
            bool killAllowed = _directShot || rocketInRange;
            if (killAllowed && (Vector2.Distance(nextPos, _target.position) < KillRadius || dist < KillRadius))
            {
                Vector2 midpoint = (nextPos + (Vector2)_target.position) * 0.5f;
                var rocket = _target.GetComponent<Rocket>();
                if (rocket != null) rocket.ForceLand();
                transform.position = nextPos;
                Detonate(midpoint);
                return;
            }

            transform.position = nextPos;
            ApplySpriteRotation(_heading);
        }

        // ---- Helpers ----

        private void ApplySpriteRotation(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            // Sprite nose points LEFT in raw PNG → +180°.
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 180f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>Clamp position to camera viewport so missile stays visible at all times.</summary>
        private void ClampInsideViewport()
        {
            var cam = Camera.main;
            if (cam == null) return;
            const float Padding = 0.6f;

            float orthoY = cam.orthographicSize;
            float orthoX = orthoY * cam.aspect;
            Vector3 camPos = cam.transform.position;
            float minX = camPos.x - orthoX + Padding;
            float maxX = camPos.x + orthoX - Padding;
            float minY = camPos.y - orthoY + Padding;
            float maxY = camPos.y + orthoY - Padding;

            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, minX, maxX);
            p.y = Mathf.Clamp(p.y, minY, maxY);
            float floorY = GameConstants.GroundTop + GroundFloorBuffer;
            if (p.y < floorY) p.y = floorY;
            transform.position = p;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;
            // Only kill in chase phase — drop/curve are "harmless" cinematic phases.
            if (_phase != Phase.Chase) return;
            if (!other.CompareTag("Player")) return;

            float rocketDistFromJetSqr = ((Vector2)other.transform.position - _jetPos).sqrMagnitude;
            if (!_directShot && rocketDistFromJetSqr > _detectionRangeSqr) return;

            Vector2 impact = (transform.position + other.transform.position) * 0.5f;
            Detonate(impact);
            var rocket = other.GetComponent<Rocket>();
            if (rocket != null) rocket.ForceLand();
        }

        private void Detonate(Vector2 position)
        {
            _hasHit = true;
            ExplosionEffect.Spawn(position, isHit: true, isVerticalImpact: false);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayHitTarget();
            Destroy(gameObject);
        }
    }
}
