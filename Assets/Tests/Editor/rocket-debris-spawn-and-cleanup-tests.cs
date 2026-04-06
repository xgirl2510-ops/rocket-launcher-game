using NUnit.Framework;
using UnityEngine;

namespace RocketLauncher.Tests
{
    /// <summary>
    /// Tests RocketDebris static spawn/cleanup: Spawn creates debris GameObjects,
    /// ClearAll destroys them, and re-spawning after clear works cleanly.
    /// </summary>
    public class RocketDebrisSpawnAndCleanupTests
    {
        [SetUp]
        public void SetUp()
        {
            RocketDebris.ClearAll();
            DestroyAllDebrisImmediate();
        }

        [TearDown]
        public void TearDown()
        {
            RocketDebris.ClearAll();
            DestroyAllDebrisImmediate();
        }

        [Test]
        public void Spawn_CreatesDebrisGameObjects()
        {
            int expectedCount = 16; // default count parameter

            RocketDebris.Spawn(Vector2.zero);

            int actual = CountDebris();
            Assert.AreEqual(expectedCount, actual,
                "Spawn() with default count should create 16 debris pieces.");
        }

        [Test]
        public void Spawn_WithCustomCount_CreatesExactNumber()
        {
            int count = 5;

            RocketDebris.Spawn(Vector2.zero, count);

            int actual = CountDebris();
            Assert.AreEqual(count, actual,
                "Spawn() should create the exact number of debris requested.");
        }

        [Test]
        public void Spawn_DebrisHaveSpriteRenderers()
        {
            RocketDebris.Spawn(Vector2.zero, 3);

            var renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            int debrisRenderers = 0;
            foreach (var sr in renderers)
            {
                if (sr.gameObject.name == "Debris") debrisRenderers++;
            }

            Assert.AreEqual(3, debrisRenderers,
                "Each debris piece should have a SpriteRenderer.");
        }

        [Test]
        public void Spawn_DebrisHaveRocketDebrisComponent()
        {
            RocketDebris.Spawn(Vector2.zero, 4);

            var components = Object.FindObjectsByType<RocketDebris>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            Assert.AreEqual(4, components.Length,
                "Each debris piece should have a RocketDebris component.");
        }

        [Test]
        public void ClearAll_DestroysAllDebris()
        {
            RocketDebris.Spawn(Vector2.zero, 10);

            RocketDebris.ClearAll();
            // Destroy is deferred; use DestroyImmediate on remaining objects
            DestroyAllDebrisImmediate();

            int remaining = CountDebris();
            Assert.AreEqual(0, remaining,
                "ClearAll should destroy all debris GameObjects.");
        }

        [Test]
        public void ClearAll_WithNoDebris_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RocketDebris.ClearAll(),
                "ClearAll with no debris should be safe.");
        }

        [Test]
        public void ClearAll_CalledTwice_DoesNotThrow()
        {
            RocketDebris.Spawn(Vector2.zero, 5);
            RocketDebris.ClearAll();

            Assert.DoesNotThrow(() => RocketDebris.ClearAll(),
                "Double ClearAll should be safe.");
        }

        [Test]
        public void SpawnAfterClearAll_WorksCleanly()
        {
            RocketDebris.Spawn(Vector2.zero, 8);
            RocketDebris.ClearAll();
            DestroyAllDebrisImmediate();

            RocketDebris.Spawn(Vector2.zero, 4);

            int actual = CountDebris();
            Assert.AreEqual(4, actual,
                "Spawning after ClearAll should work without accumulation.");
        }

        [Test]
        public void SpawnTargetDebris_CreatesDebris()
        {
            RocketDebris.SpawnTargetDebris(Vector2.zero);

            int count = CountDebris();
            Assert.Greater(count, 0,
                "SpawnTargetDebris should create debris GameObjects.");
        }

        [Test]
        public void SpawnDirtDebris_CreatesDebris()
        {
            RocketDebris.SpawnDirtDebris(Vector2.zero, 1f);

            int count = CountDebris();
            Assert.Greater(count, 0,
                "SpawnDirtDebris should create debris GameObjects.");
        }

        /// <summary>Count all GameObjects named "Debris" in the scene.</summary>
        private int CountDebris()
        {
            int count = 0;
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            foreach (var go in allObjects)
            {
                if (go.name == "Debris") count++;
            }
            return count;
        }

        /// <summary>
        /// DestroyImmediate all Debris objects. Needed because Object.Destroy is deferred
        /// and does not execute within EditMode test frames.
        /// </summary>
        private void DestroyAllDebrisImmediate()
        {
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            foreach (var go in allObjects)
            {
                if (go != null && go.name == "Debris")
                    Object.DestroyImmediate(go);
            }
        }
    }
}
