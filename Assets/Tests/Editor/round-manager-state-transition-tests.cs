using NUnit.Framework;

namespace RocketLauncher.Tests
{
    /// <summary>
    /// Tests RoundManager-driven state transitions via GameRoundTracker.
    /// Covers miss counting, round progression, best score tracking across rounds.
    /// </summary>
    public class RoundManagerStateTransitionTests
    {
        private GameRoundTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new GameRoundTracker();
        }

        [Test]
        public void MissSequence_FiveMisses_ShotsCountCorrect()
        {
            for (int i = 0; i < 5; i++)
                _tracker.IncrementShots();

            Assert.AreEqual(5, _tracker.RoundShots);
        }

        [Test]
        public void HitAfterMisses_BestScoreRecorded()
        {
            _tracker.IncrementShots();
            _tracker.IncrementShots();
            _tracker.IncrementShots();
            bool updated = _tracker.TryUpdateBest(3);

            Assert.IsTrue(updated);
            Assert.AreEqual(3, _tracker.BestScore);
        }

        [Test]
        public void Restart_NewRound_ResetsShots()
        {
            _tracker.IncrementShots();
            _tracker.IncrementShots();
            _tracker.NewRound();

            Assert.AreEqual(0, _tracker.RoundShots);
            Assert.AreEqual(2, _tracker.RoundNumber);
        }

        [Test]
        public void MultiRound_BestScorePersistedAcrossRounds()
        {
            // Round 1: 4 shots
            for (int i = 0; i < 4; i++) _tracker.IncrementShots();
            _tracker.TryUpdateBest(4);
            _tracker.NewRound();

            // Round 2: 2 shots (new best)
            for (int i = 0; i < 2; i++) _tracker.IncrementShots();
            _tracker.TryUpdateBest(2);
            _tracker.NewRound();

            // Round 3: 6 shots (not best)
            for (int i = 0; i < 6; i++) _tracker.IncrementShots();
            _tracker.TryUpdateBest(6);

            Assert.AreEqual(2, _tracker.BestScore);
            Assert.AreEqual(3, _tracker.RoundNumber);
        }

        [Test]
        public void AutoPlayRound_NewRound_ResetsProperly()
        {
            // Simulate auto-play: increment shots, then new round
            _tracker.IncrementShots();
            _tracker.NewRound();

            Assert.AreEqual(0, _tracker.RoundShots);
            Assert.AreEqual(2, _tracker.RoundNumber);
        }

        [Test]
        public void GetStatsText_AfterMultipleRounds_FormatsCorrectly()
        {
            _tracker.IncrementShots();
            _tracker.IncrementShots();
            _tracker.TryUpdateBest(2);
            _tracker.NewRound();
            _tracker.IncrementShots();

            string text = _tracker.GetStatsText();
            StringAssert.Contains("Round 2", text);
            StringAssert.Contains("Shots 1", text);
            StringAssert.Contains("Best 2", text);
        }

        [Test]
        public void TryUpdateBest_ZeroShots_ReturnsFalse()
        {
            bool updated = _tracker.TryUpdateBest(0);
            Assert.IsFalse(updated);
        }

        [Test]
        public void TryUpdateBest_NegativeShots_ReturnsFalse()
        {
            bool updated = _tracker.TryUpdateBest(-1);
            Assert.IsFalse(updated);
        }
    }
}
