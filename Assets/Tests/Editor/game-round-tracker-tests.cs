using NUnit.Framework;

namespace RocketLauncher.Tests
{
    /// <summary>
    /// Unit tests for GameRoundTracker — pure C# logic, no MonoBehaviour.
    /// Covers shot counting, round progression, best score tracking, and stats formatting.
    /// </summary>
    public class GameRoundTrackerTests
    {
        private GameRoundTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new GameRoundTracker();
        }

        [Test]
        public void InitialState_RoundOneZeroShots()
        {
            Assert.AreEqual(1, _tracker.RoundNumber);
            Assert.AreEqual(0, _tracker.RoundShots);
            Assert.AreEqual(0, _tracker.BestScore);
        }

        [Test]
        public void IncrementShots_IncreasesCount()
        {
            _tracker.IncrementShots();
            Assert.AreEqual(1, _tracker.RoundShots);

            _tracker.IncrementShots();
            _tracker.IncrementShots();
            Assert.AreEqual(3, _tracker.RoundShots);
        }

        [Test]
        public void NewRound_IncrementsRoundResetsShots()
        {
            _tracker.IncrementShots();
            _tracker.IncrementShots();
            _tracker.NewRound();

            Assert.AreEqual(2, _tracker.RoundNumber);
            Assert.AreEqual(0, _tracker.RoundShots);
        }

        [Test]
        public void TryUpdateBest_FirstCall_AlwaysUpdates()
        {
            bool updated = _tracker.TryUpdateBest(5);
            Assert.IsTrue(updated);
            Assert.AreEqual(5, _tracker.BestScore);
        }

        [Test]
        public void TryUpdateBest_LowerScore_Updates()
        {
            _tracker.TryUpdateBest(5);
            bool updated = _tracker.TryUpdateBest(3);
            Assert.IsTrue(updated);
            Assert.AreEqual(3, _tracker.BestScore);
        }

        [Test]
        public void TryUpdateBest_HigherScore_DoesNotUpdate()
        {
            _tracker.TryUpdateBest(3);
            bool updated = _tracker.TryUpdateBest(7);
            Assert.IsFalse(updated);
            Assert.AreEqual(3, _tracker.BestScore);
        }

        [Test]
        public void TryUpdateBest_EqualScore_DoesNotUpdate()
        {
            _tracker.TryUpdateBest(4);
            bool updated = _tracker.TryUpdateBest(4);
            Assert.IsFalse(updated);
            Assert.AreEqual(4, _tracker.BestScore);
        }

        [Test]
        public void GetStatsText_NoBest_ShowsDash()
        {
            string text = _tracker.GetStatsText();
            StringAssert.Contains("Round 1", text);
            StringAssert.Contains("Shots 0", text);
            StringAssert.Contains("Best --", text);
        }

        [Test]
        public void GetStatsText_WithBest_ShowsNumber()
        {
            _tracker.IncrementShots();
            _tracker.IncrementShots();
            _tracker.TryUpdateBest(2);

            string text = _tracker.GetStatsText();
            StringAssert.Contains("Round 1", text);
            StringAssert.Contains("Shots 2", text);
            StringAssert.Contains("Best 2", text);
        }

        [Test]
        public void MultipleRounds_TrackCorrectly()
        {
            // Round 1: 3 shots
            _tracker.IncrementShots();
            _tracker.IncrementShots();
            _tracker.IncrementShots();
            _tracker.TryUpdateBest(3);

            // Round 2: 1 shot (new best)
            _tracker.NewRound();
            _tracker.IncrementShots();
            _tracker.TryUpdateBest(1);

            Assert.AreEqual(2, _tracker.RoundNumber);
            Assert.AreEqual(1, _tracker.RoundShots);
            Assert.AreEqual(1, _tracker.BestScore);
        }
    }
}
