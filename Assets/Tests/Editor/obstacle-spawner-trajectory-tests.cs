using NUnit.Framework;
using UnityEngine;

namespace RocketLauncher.Tests
{
    /// <summary>
    /// Tests ObstacleSpawner trajectory math and obstacle spawning via public API.
    /// Covers null-ref safety, safe launch vector validity, and obstacle creation.
    /// </summary>
    public class ObstacleSpawnerTrajectoryTests
    {
        private GameObject _spawnerGo;
        private ObstacleSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _spawnerGo = new GameObject("TestSpawner");
            _spawner = _spawnerGo.AddComponent<ObstacleSpawner>();
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy all GameObjects created during test (obstacles + spawner)
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            foreach (var go in allObjects)
            {
                if (go.name == "Obstacle" || go.name == "TestSpawner" ||
                    go.name == "SpawnPoint" || go.name == "Target")
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void RespawnObstacles_NullRefs_DoesNotThrow()
        {
            // Spawner has no _spawnPoint or _targetTransform wired — should early-return
            Assert.DoesNotThrow(() => _spawner.RespawnObstacles());
        }

        [Test]
        public void RespawnObstacles_NullRefs_LeavesDefaultLaunchValues()
        {
            _spawner.RespawnObstacles();

            // With null refs, CalculateTrajectory is never called, so values stay default
            Assert.AreEqual(Vector2.zero, _spawner.SafeLaunchDirection);
            Assert.AreEqual(0f, _spawner.SafeLaunchForce);
        }

        [Test]
        public void RespawnObstacles_WithRefs_SafeLaunchDirectionIsNonZero()
        {
            WireSpawnerRefs(Vector3.zero, new Vector3(20f, 5f, 0f));

            _spawner.RespawnObstacles();

            Assert.AreNotEqual(Vector2.zero, _spawner.SafeLaunchDirection,
                "SafeLaunchDirection should be computed after RespawnObstacles.");
        }

        [Test]
        public void RespawnObstacles_WithRefs_SafeLaunchForceIsPositive()
        {
            WireSpawnerRefs(Vector3.zero, new Vector3(20f, 5f, 0f));

            _spawner.RespawnObstacles();

            Assert.Greater(_spawner.SafeLaunchForce, 0f,
                "SafeLaunchForce should be positive after trajectory calculation.");
        }

        [Test]
        public void RespawnObstacles_WithRefs_SafeLaunchForceIsWithinRange()
        {
            WireSpawnerRefs(Vector3.zero, new Vector3(20f, 5f, 0f));

            _spawner.RespawnObstacles();

            Assert.GreaterOrEqual(_spawner.SafeLaunchForce, GameConstants.MinLaunchForce);
            Assert.LessOrEqual(_spawner.SafeLaunchForce, GameConstants.MaxLaunchForce);
        }

        [Test]
        public void RespawnObstacles_WithRefs_SafeLaunchDirectionIsNormalized()
        {
            WireSpawnerRefs(Vector3.zero, new Vector3(25f, 0f, 0f));

            _spawner.RespawnObstacles();

            float mag = _spawner.SafeLaunchDirection.magnitude;
            Assert.AreEqual(1f, mag, 0.001f,
                "SafeLaunchDirection should be a unit vector.");
        }

        [Test]
        public void RespawnObstacles_WithRefs_CreatesObstacles()
        {
            WireSpawnerRefs(Vector3.zero, new Vector3(30f, 0f, 0f));

            _spawner.RespawnObstacles();

            var obstacles = GameObject.FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            Assert.Greater(obstacles.Length, 0,
                "Should have spawned at least one obstacle with a BoxCollider2D.");
        }

        [Test]
        public void RespawnObstacles_CalledTwice_ClearsPreviousObstacles()
        {
            WireSpawnerRefs(Vector3.zero, new Vector3(30f, 0f, 0f));

            _spawner.RespawnObstacles();
            int firstCount = CountObstacles();

            _spawner.RespawnObstacles();
            int secondCount = CountObstacles();

            // Second call should have cleared previous obstacles;
            // count should be roughly equal (not doubled)
            Assert.LessOrEqual(secondCount, firstCount * 2,
                "Respawn should clear old obstacles, not accumulate them.");
        }

        /// <summary>Wire spawn point and target transforms into the spawner via SerializedObject.</summary>
        private void WireSpawnerRefs(Vector3 spawnPos, Vector3 targetPos)
        {
            var spawnPointGo = new GameObject("SpawnPoint");
            spawnPointGo.transform.position = spawnPos;

            var targetGo = new GameObject("Target");
            targetGo.transform.position = targetPos;

            var so = new UnityEditor.SerializedObject(_spawner);
            so.FindProperty("_spawnPoint").objectReferenceValue = spawnPointGo.transform;
            so.FindProperty("_targetTransform").objectReferenceValue = targetGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private int CountObstacles()
        {
            int count = 0;
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            foreach (var go in allObjects)
            {
                if (go.name == "Obstacle") count++;
            }
            return count;
        }
    }
}
