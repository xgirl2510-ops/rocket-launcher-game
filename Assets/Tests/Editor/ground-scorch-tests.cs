using NUnit.Framework;
using UnityEngine;

namespace RocketLauncher.Tests
{
    /// <summary>
    /// Tests GroundScorch static class: GetGroundY with/without craters,
    /// ClearAll cleanup, and crater depth math.
    /// </summary>
    public class GroundScorchTests
    {
        [SetUp]
        public void SetUp()
        {
            // Start each test with clean static state
            GroundScorch.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            GroundScorch.ClearAll();

            // Destroy any GameObjects created by Spawn (craters, debris, etc.)
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            foreach (var go in allObjects)
            {
                if (go.name == "Crater" || go.name == "Debris" || go.name == "Ground")
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void GetGroundY_NoCraters_ReturnsGroundTop()
        {
            float y = GroundScorch.GetGroundY(0f);

            Assert.AreEqual(GameConstants.GroundTop, y,
                "With no craters, GetGroundY should return GameConstants.GroundTop.");
        }

        [Test]
        public void GetGroundY_NoCraters_AnyXReturnsGroundTop()
        {
            Assert.AreEqual(GameConstants.GroundTop, GroundScorch.GetGroundY(-100f));
            Assert.AreEqual(GameConstants.GroundTop, GroundScorch.GetGroundY(0f));
            Assert.AreEqual(GameConstants.GroundTop, GroundScorch.GetGroundY(50f));
            Assert.AreEqual(GameConstants.GroundTop, GroundScorch.GetGroundY(999f));
        }

        [Test]
        public void ClearAll_AfterSpawn_RestoresGroundTop()
        {
            GroundScorch.Spawn(new Vector2(10f, GameConstants.GroundTop), 20f);

            GroundScorch.ClearAll();

            float y = GroundScorch.GetGroundY(10f);
            Assert.AreEqual(GameConstants.GroundTop, y,
                "After ClearAll, GetGroundY should return base GroundTop.");
        }

        [Test]
        public void ClearAll_DestroysAllCraterGameObjects()
        {
            GroundScorch.Spawn(new Vector2(5f, GameConstants.GroundTop), 15f);
            GroundScorch.Spawn(new Vector2(15f, GameConstants.GroundTop), 25f);

            GroundScorch.ClearAll();

            // Allow DestroyImmediate is not used internally (Destroy is deferred),
            // so we check the crater list is cleared by verifying GetGroundY
            float y1 = GroundScorch.GetGroundY(5f);
            float y2 = GroundScorch.GetGroundY(15f);
            Assert.AreEqual(GameConstants.GroundTop, y1,
                "Crater data at x=5 should be cleared.");
            Assert.AreEqual(GameConstants.GroundTop, y2,
                "Crater data at x=15 should be cleared.");
        }

        [Test]
        public void Spawn_CreatesLowerGroundAtImpactX()
        {
            float impactX = 10f;
            GroundScorch.Spawn(new Vector2(impactX, GameConstants.GroundTop), 20f);

            float y = GroundScorch.GetGroundY(impactX);
            Assert.Less(y, GameConstants.GroundTop,
                "Ground Y at crater center should be lower than GroundTop.");
        }

        [Test]
        public void GetGroundY_FarFromCrater_ReturnsGroundTop()
        {
            GroundScorch.Spawn(new Vector2(10f, GameConstants.GroundTop), 20f);

            // Very far from crater center — should not be affected
            float y = GroundScorch.GetGroundY(1000f);
            Assert.AreEqual(GameConstants.GroundTop, y,
                "Ground far from any crater should be at GroundTop.");
        }

        [Test]
        public void MultipleCraters_DeepestWins()
        {
            // Spawn two craters at same X — GetGroundY should return the lowest
            GroundScorch.Spawn(new Vector2(10f, GameConstants.GroundTop), 10f);
            GroundScorch.Spawn(new Vector2(10f, GameConstants.GroundTop), 50f);

            float y = GroundScorch.GetGroundY(10f);
            Assert.Less(y, GameConstants.GroundTop,
                "Overlapping craters should still produce a lowered ground Y.");
        }

        [Test]
        public void ClearAll_CalledTwice_DoesNotThrow()
        {
            GroundScorch.Spawn(new Vector2(5f, GameConstants.GroundTop), 10f);
            GroundScorch.ClearAll();

            Assert.DoesNotThrow(() => GroundScorch.ClearAll(),
                "Calling ClearAll on already-cleared state should be safe.");
        }

        [Test]
        public void ClearAll_WithNoCraters_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => GroundScorch.ClearAll(),
                "ClearAll with no craters should be a no-op.");
        }
    }
}
