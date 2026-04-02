/// <summary>
/// Tracks per-round and cross-round game statistics.
/// Plain C# class (not MonoBehaviour) — instantiated by LaunchController.
/// </summary>
public class GameRoundTracker
{
    private int _roundShots;
    private int _roundNumber = 1;
    private int _bestScore;

    public int RoundShots => _roundShots;
    public int RoundNumber => _roundNumber;
    public int BestScore => _bestScore;

    /// <summary>Increment shot counter for current round.</summary>
    public void IncrementShots() => _roundShots++;

    /// <summary>Start a new round — increments round number, resets shot counter.</summary>
    public void NewRound()
    {
        _roundNumber++;
        _roundShots = 0;
    }

    /// <summary>Update best score if current round is better. Returns true if best was updated.</summary>
    public bool TryUpdateBest(int shots)
    {
        if (_bestScore == 0 || shots < _bestScore)
        {
            _bestScore = shots;
            return true;
        }
        return false;
    }

    /// <summary>Formatted stats string for the HUD.</summary>
    public string GetStatsText()
    {
        string best = _bestScore > 0 ? _bestScore.ToString() : "--";
        return $"Round {_roundNumber}  \u00b7  Shots {_roundShots}  \u00b7  Best {best}";
    }
}
