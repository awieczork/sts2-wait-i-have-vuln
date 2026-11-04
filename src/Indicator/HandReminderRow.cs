namespace WaitIHaveVuln.Indicator;

/// <summary>
/// One player row as the bubbles and the card reminder see it at the moment of a check.
/// <see cref="IsFull"/> is whether the row's icon is full: the player is in their Play phase, connected,
/// alive, and a supported card in hand passes the game's play check (<see cref="IndicatorRule"/>).
/// </summary>
internal readonly record struct HandReminderRow<TPlayer>(
    TPlayer Player,
    bool IsOwnRow,
    bool IsFull,
    bool IsReadyToEndTurn);
