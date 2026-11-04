namespace WaitIHaveVuln.Indicator;

/// <summary>A committed change of a row's level; the arrival bubble goes with <see cref="BecameFull"/> and comes down with <see cref="LeftFull"/>.</summary>
internal readonly record struct IndicatorChange(IndicatorLevel From, IndicatorLevel To)
{
    internal bool BecameFull => To == IndicatorLevel.Full && From != IndicatorLevel.Full;

    internal bool LeftFull => From == IndicatorLevel.Full && To != IndicatorLevel.Full;
}
