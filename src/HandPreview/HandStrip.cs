namespace WaitIHaveVuln.HandPreview;

/// <summary>
/// Where the preview's cards go: one scale for every card and the centre of each, because the game's
/// card scene has its origin at the card's centre. Card <c>i</c> is centred at
/// (<see cref="FirstCenterX"/> + i × <see cref="Step"/>, <see cref="CenterY"/>).
/// </summary>
internal readonly record struct HandStrip(float Scale, float FirstCenterX, float CenterY, float Step)
{
    internal float CenterX(int index) => FirstCenterX + index * Step;
}
