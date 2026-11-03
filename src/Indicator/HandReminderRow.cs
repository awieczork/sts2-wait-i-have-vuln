namespace WaitIHaveVuln.Indicator;

/// <summary>
/// One player row as the hand reminder sees it at the moment of a check. <see cref="CanPlayCard"/> is
/// whether a supported card in hand passes the game's own play check now (energy, the Unplayable
/// keyword, powers and relics that block it); it cannot know energy gained later in the turn.
/// </summary>
internal readonly record struct HandReminderRow<TPlayer>(
    TPlayer Player,
    bool IsOwnRow,
    bool HoldsCard,
    bool IsAlive,
    bool IsConnected,
    bool IsReadyToEndTurn,
    bool CanPlayCard = true);
