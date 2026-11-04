namespace WaitIHaveVuln.Indicator;

/// <summary>
/// The game's running action as the indicator sees it: none, running, or paused for a player's choice.
/// An action paused for a choice stays the game's running action until another one starts, so it
/// has its own phase rather than counting as running.
/// </summary>
internal enum ActionPhase
{
    None,
    Running,
    WaitingForChoice
}
