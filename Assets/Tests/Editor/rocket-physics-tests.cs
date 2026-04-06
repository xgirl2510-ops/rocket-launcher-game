using NUnit.Framework;
using UnityEngine;

namespace RocketLauncher.Tests
{
    /// <summary>
    /// Tests Rocket component physics: Launch/Reset state transitions,
    /// IsFlying flag, bodyType toggling, and velocity/position reset.
    /// </summary>
    public class RocketPhysicsTests
    {
        private GameObject _rocketGo;
        private Rocket _rocket;
        private Rigidbody2D _rb;

        [SetUp]
        public void SetUp()
        {
            _rocketGo = new GameObject("TestRocket");
            _rb = _rocketGo.AddComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;

            // Add SpriteRenderer so GetComponentsInChildren<SpriteRenderer> returns non-empty
            _rocketGo.AddComponent<SpriteRenderer>();

            _rocket = _rocketGo.AddComponent<Rocket>();
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy rocket and any child objects created during Awake (e.g. trail)
            if (_rocketGo != null)
                Object.DestroyImmediate(_rocketGo);
        }

        [Test]
        public void InitialState_IsNotFlying()
        {
            Assert.IsFalse(_rocket.IsFlying,
                "Rocket should not be flying before Launch() is called.");
        }

        [Test]
        public void Launch_SetsIsFlying_True()
        {
            _rocket.Launch(Vector2.up, 10f);

            Assert.IsTrue(_rocket.IsFlying,
                "Rocket should be flying after Launch().");
        }

        [Test]
        public void Launch_SetsBodyType_Dynamic()
        {
            _rocket.Launch(Vector2.right, 15f);

            Assert.AreEqual(RigidbodyType2D.Dynamic, _rb.bodyType,
                "Launch should switch Rigidbody2D to Dynamic.");
        }

        [Test]
        public void ResetToPosition_SetsIsFlying_False()
        {
            _rocket.Launch(Vector2.up, 10f);

            _rocket.ResetToPosition(Vector2.zero);

            Assert.IsFalse(_rocket.IsFlying,
                "ResetToPosition should clear the flying flag.");
        }

        [Test]
        public void ResetToPosition_SetsBodyType_Kinematic()
        {
            _rocket.Launch(Vector2.up, 10f);

            _rocket.ResetToPosition(Vector2.zero);

            Assert.AreEqual(RigidbodyType2D.Kinematic, _rb.bodyType,
                "ResetToPosition should switch Rigidbody2D back to Kinematic.");
        }

        [Test]
        public void ResetToPosition_MatchesGivenPosition()
        {
            Vector2 resetPos = new Vector2(3f, 7f);

            _rocket.Launch(Vector2.up, 10f);
            _rocket.ResetToPosition(resetPos);

            Vector2 actualPos = _rocketGo.transform.position;
            Assert.AreEqual(resetPos.x, actualPos.x, 0.001f, "X position should match.");
            Assert.AreEqual(resetPos.y, actualPos.y, 0.001f, "Y position should match.");
        }

        [Test]
        public void ResetToPosition_ZerosVelocity()
        {
            _rocket.Launch(Vector2.up, 20f);

            _rocket.ResetToPosition(Vector2.zero);

            Assert.AreEqual(Vector2.zero, _rb.linearVelocity,
                "Velocity should be zero after reset.");
            Assert.AreEqual(0f, _rb.angularVelocity,
                "Angular velocity should be zero after reset.");
        }

        [Test]
        public void ResetToPosition_ResetsRotation()
        {
            _rocket.Launch(new Vector2(1f, 1f), 10f);

            _rocket.ResetToPosition(Vector2.zero);

            Assert.AreEqual(Quaternion.identity, _rocketGo.transform.rotation,
                "Rotation should be identity after reset.");
        }

        [Test]
        public void Launch_FiresOnRocketLaunchedEvent()
        {
            bool eventFired = false;
            _rocket.OnRocketLaunched += () => eventFired = true;

            _rocket.Launch(Vector2.up, 10f);

            Assert.IsTrue(eventFired,
                "OnRocketLaunched event should fire on Launch().");
        }

        [Test]
        public void Launch_ThenReset_ThenLaunch_WorksCleanly()
        {
            _rocket.Launch(Vector2.up, 10f);
            _rocket.ResetToPosition(Vector2.zero);

            Assert.IsFalse(_rocket.IsFlying);

            _rocket.Launch(Vector2.right, 15f);

            Assert.IsTrue(_rocket.IsFlying);
            Assert.AreEqual(RigidbodyType2D.Dynamic, _rb.bodyType);
        }
    }
}
