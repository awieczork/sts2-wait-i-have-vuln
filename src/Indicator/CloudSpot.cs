namespace WaitIHaveVuln.Indicator;

/// <summary>The spot found for the card cloud: its side of the card and its box.</summary>
internal readonly record struct CloudSpot(CloudSide Side, Box Cloud);
