using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.RegressionHarness;

internal static class Program
{
    private static readonly Action[] Cases =
    {
        VerifyExactSupportedSet,
        VerifyHandTransitions,
        VerifyRebindingAndCombatEnd,
        VerifyInterruptedTracking,
        VerifyOwnerCleanup,
        VerifyLocalAndRemoteRows
    };

    private static int _assertions;

    private static void Main()
    {
        foreach (var regressionCase in Cases)
        {
            regressionCase();
        }

        Console.WriteLine($"PASS: {Cases.Length} regression cases, {_assertions} assertions.");
    }

    private static void VerifyExactSupportedSet()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "ASSASSINATE",
            "BASH",
            "BEAM_CELL",
            "BREAK",
            "COMET",
            "DOMINATE",
            "EXPOSE",
            "FALLING_STAR",
            "FEAR",
            "GAMMA_BLAST",
            "HIGH_FIVE",
            "KNOW_THY_PLACE",
            "METEOR_SHOWER",
            "PUTREFY",
            "SHOCKWAVE",
            "SQUASH",
            "TAUNT",
            "THUNDERCLAP",
            "TREMBLE",
            "UPPERCUT"
        };
        var excluded = new[] { "MOLTEN_FIST", "MAD_SCIENCE", "VICIOUS", "MISERY", "bash" };

        Assert(expected.Count == 20, "The expected support set has 20 models.");
        Assert(expected.SetEquals(VulnerableCardPredicate.SupportedIds), "The production predicate has exactly the approved model set.");
        foreach (var modelId in expected)
        {
            Assert(VulnerableCardPredicate.IsSupported(modelId), $"{modelId} is supported.");
        }

        foreach (var modelId in excluded)
        {
            Assert(!VulnerableCardPredicate.IsSupported(modelId), $"{modelId} is excluded.");
        }
    }

    private static void VerifyHandTransitions()
    {
        var hand = new FakeHand();
        var visibilityChanges = new List<bool>();
        using var state = CreateState(visibilityChanges.Add);

        state.Bind(hand);
        Assert(!state.IsVisible, "An empty hand starts hidden.");
        hand.Add("VICIOUS");
        Assert(!state.IsVisible, "An unsupported card does not show the indicator.");
        state.Bind(hand);
        Assert(hand.AddedSubscriptions == 1 && hand.RemovedSubscriptions == 1, "Repeated binding does not duplicate listeners.");

        var first = hand.Add("BASH");
        Assert(state.IsVisible, "The first supported card shows the indicator.");
        var second = hand.Add("UPPERCUT");
        Assert(state.IsVisible && visibilityChanges.Count == 1, "Two supported cards still produce one visible indicator.");
        hand.Remove(first);
        Assert(state.IsVisible, "Removing one of two supported cards keeps the indicator visible.");
        hand.Remove(second);
        Assert(!state.IsVisible, "Removing the last supported card hides the indicator.");
        Assert(visibilityChanges.SequenceEqual(new[] { true, false }), "Visibility changes only at the first and last qualifying card.");
    }

    private static void VerifyRebindingAndCombatEnd()
    {
        var firstHand = new FakeHand();
        var nextHand = new FakeHand();
        var state = CreateState(_ => { });
        firstHand.Add("BASH");

        state.Bind(firstHand);
        Assert(state.IsVisible, "Initial binding scans existing cards.");
        state.Bind(nextHand);
        Assert(!state.IsVisible, "Rebinding refreshes state from the new hand.");
        Assert(firstHand.AddedSubscriptions == 0 && firstHand.RemovedSubscriptions == 0, "Rebinding detaches the old hand.");
        Assert(nextHand.AddedSubscriptions == 1 && nextHand.RemovedSubscriptions == 1, "Rebinding attaches one listener pair to the new hand.");

        firstHand.Add("UPPERCUT");
        Assert(!state.IsVisible, "The old hand no longer changes the indicator.");
        nextHand.Add("COMET");
        Assert(state.IsVisible, "The rebound hand drives the indicator.");
        state.EndCombat();
        Assert(!state.IsVisible, "Combat end hides the indicator.");
        Assert(nextHand.AddedSubscriptions == 0 && nextHand.RemovedSubscriptions == 0, "Combat end removes both listeners.");
        nextHand.Add("BASH");
        Assert(!state.IsVisible, "Hand changes after combat end do not revive the indicator.");

        state.Bind(nextHand);
        Assert(state.IsVisible, "A later setup can bind the state again.");
        Assert(nextHand.AddedSubscriptions == 1 && nextHand.RemovedSubscriptions == 1, "Rebinding after combat end installs one listener pair.");
        state.Dispose();
    }

    private static void VerifyOwnerCleanup()
    {
        var hand = new FakeHand();
        var state = CreateState(_ => { });
        hand.Add("BASH");
        state.Bind(hand);
        Assert(state.IsVisible, "Owner cleanup begins with an active indicator.");

        state.Dispose();
        Assert(!state.IsVisible, "Owner cleanup hides the indicator.");
        Assert(hand.AddedSubscriptions == 0 && hand.RemovedSubscriptions == 0, "Owner cleanup removes both listeners.");
        hand.Add("UPPERCUT");
        Assert(!state.IsVisible, "An exited owner cannot be revived by later hand events.");

        var threw = false;
        try
        {
            state.Bind(hand);
        }
        catch (ObjectDisposedException)
        {
            threw = true;
        }

        Assert(threw, "A disposed owner cannot be rebound.");
    }

    private static void VerifyInterruptedTracking()
    {
        using var initial = CreateState(_ => { });
        initial.Suspend();
        Assert(!initial.IsVisible, "An interruption before any hand observation remains hidden.");

        var interruptedHand = new FakeHand();
        interruptedHand.Add("BASH");
        var visibilityChanges = new List<bool>();
        using var state = CreateState(visibilityChanges.Add);
        state.Bind(interruptedHand);
        Assert(state.IsVisible, "The interruption starts with an observed visible result.");

        state.Suspend();
        state.Suspend();
        Assert(state.IsVisible, "Repeated suspension preserves the last visible result.");
        Assert(interruptedHand.AddedSubscriptions == 0 && interruptedHand.RemovedSubscriptions == 0, "Suspension detaches the old hand listener pair.");
        interruptedHand.Remove(interruptedHand.Cards[0]);
        Assert(state.IsVisible && visibilityChanges.SequenceEqual(new[] { true }), "Old-hand events cannot change the result during suspension.");

        var restoredHand = new FakeHand();
        restoredHand.Add("VICIOUS");
        state.Bind(restoredHand);
        Assert(!state.IsVisible, "Resuming with a new ineligible hand refreshes a retained visible result to hidden.");
        Assert(restoredHand.AddedSubscriptions == 1 && restoredHand.RemovedSubscriptions == 1, "Resume installs one listener pair.");
        state.Suspend();
        restoredHand.Add("BASH");
        Assert(!state.IsVisible, "An observed hidden result survives old-hand events while suspended.");
        Assert(restoredHand.AddedSubscriptions == 0 && restoredHand.RemovedSubscriptions == 0, "Hidden-state suspension detaches the hand listener pair.");
        state.Bind(new FakeHandAdapter(restoredHand));
        Assert(state.IsVisible, "Resuming rescans a hand that changed while the observed result was hidden.");
        Assert(restoredHand.AddedSubscriptions == 1 && restoredHand.RemovedSubscriptions == 1, "Resume through a new adapter installs one listener pair.");
        state.Bind(new FakeHandAdapter(restoredHand));
        Assert(restoredHand.AddedSubscriptions == 1 && restoredHand.RemovedSubscriptions == 1, "Repeated resume does not duplicate listeners.");

        restoredHand.Add("COMET");
        Assert(state.IsVisible, "Normal tracking resumes with the new hand's current eligibility.");
        state.Suspend();
        state.Suspend();
        restoredHand.Remove(restoredHand.Cards.Single(card => card.ModelId == "COMET"));
        Assert(state.IsVisible, "Repeated suspension preserves the last visible result after old-hand changes.");

        var emptyHand = new FakeHand();
        state.Bind(emptyHand);
        Assert(!state.IsVisible, "Resuming with a changed empty hand refreshes the result to hidden.");
        emptyHand.Add("UPPERCUT");
        Assert(state.IsVisible, "The restored hand resumes normal event tracking.");
        state.Suspend();
        state.EndCombat();
        Assert(!state.IsVisible, "Combat end hides the indicator when tracking is interrupted.");
        Assert(emptyHand.AddedSubscriptions == 0 && emptyHand.RemovedSubscriptions == 0, "Combat end leaves no hand listeners during interruption.");
        emptyHand.Add("BASH");
        Assert(!state.IsVisible, "Hand events after interrupted combat end cannot show the indicator.");

        state.Bind(emptyHand);
        emptyHand.Add("COMET");
        Assert(state.IsVisible, "A later combat setup can resume after interrupted combat end.");
        state.Suspend();
        state.Dispose();
        Assert(!state.IsVisible, "Owner disposal hides the indicator during interruption.");
        Assert(emptyHand.AddedSubscriptions == 0 && emptyHand.RemovedSubscriptions == 0, "Owner disposal leaves no hand listeners during interruption.");
    }

    private static void VerifyLocalAndRemoteRows()
    {
        var localHand = new FakeHand();
        var remoteHand = new FakeHand();
        var local = CreateState(_ => { });
        var remote = CreateState(_ => { });
        local.Bind(localHand);
        remote.Bind(remoteHand);

        localHand.Add("BASH");
        remoteHand.Add("VICIOUS");
        Assert(local.IsVisible && !remote.IsVisible, "The same state path independently evaluates the viewing and remote rows.");
        remoteHand.Add("COMET");
        Assert(local.IsVisible && remote.IsVisible, "Both rows can show their own indicator concurrently.");
        localHand.Remove(localHand.Cards[0]);
        Assert(!local.IsVisible && remote.IsVisible, "A hand change affects only its owning row.");

        local.Dispose();
        remote.Dispose();
    }

    private static HandIndicatorState<FakeCard> CreateState(Action<bool> setVisible)
    {
        return new HandIndicatorState<FakeCard>(card => card.ModelId, setVisible);
    }

    private static void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition)
        {
            throw new InvalidOperationException($"Regression failure: {message}");
        }
    }

    private sealed record FakeCard(string ModelId);

    private sealed class FakeHand : ICardHand<FakeCard>
    {
        private readonly List<FakeCard> _cards = new();
        private Action<FakeCard>? _cardAdded;
        private Action<FakeCard>? _cardRemoved;

        public IReadOnlyList<FakeCard> Cards => _cards;
        public int AddedSubscriptions { get; private set; }
        public int RemovedSubscriptions { get; private set; }

        public event Action<FakeCard> CardAdded
        {
            add
            {
                _cardAdded += value;
                AddedSubscriptions++;
            }
            remove
            {
                _cardAdded -= value;
                AddedSubscriptions--;
            }
        }

        public event Action<FakeCard> CardRemoved
        {
            add
            {
                _cardRemoved += value;
                RemovedSubscriptions++;
            }
            remove
            {
                _cardRemoved -= value;
                RemovedSubscriptions--;
            }
        }

        internal FakeCard Add(string modelId)
        {
            var card = new FakeCard(modelId);
            _cards.Add(card);
            _cardAdded?.Invoke(card);
            return card;
        }

        internal void Remove(FakeCard card)
        {
            if (!_cards.Remove(card))
            {
                throw new InvalidOperationException("Cannot remove a card that is not in the hand.");
            }

            _cardRemoved?.Invoke(card);
        }
    }

    private sealed class FakeHandAdapter : ICardHand<FakeCard>
    {
        private readonly FakeHand _hand;

        internal FakeHandAdapter(FakeHand hand)
        {
            _hand = hand;
        }

        public IReadOnlyList<FakeCard> Cards => _hand.Cards;

        public event Action<FakeCard> CardAdded
        {
            add => _hand.CardAdded += value;
            remove => _hand.CardAdded -= value;
        }

        public event Action<FakeCard> CardRemoved
        {
            add => _hand.CardRemoved += value;
            remove => _hand.CardRemoved -= value;
        }
    }
}
