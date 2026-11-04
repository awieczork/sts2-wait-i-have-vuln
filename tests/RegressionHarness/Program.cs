using WaitIHaveVuln.Indicator;
using WaitIHaveVuln.Settings;

using Policy = WaitIHaveVuln.Indicator.HandReminderPolicy<string, int>;

namespace WaitIHaveVuln.RegressionHarness;

internal static class Program
{
    private static readonly Action[] Cases =
    {
        VerifyExactSupportedSet,
        VerifyMadScienceRider,
        VerifyIndicatorLevel,
        VerifyCommitGate,
        VerifyRowTransitions,
        VerifyPendingChange,
        VerifyReminderLevel,
        VerifyVulnerableNeed,
        VerifyReactionTurn,
        VerifyArrivalQueue,
        VerifyGameBubbleTiming,
        VerifySettingsParsing,
        VerifyIndicatorVisibilityRule,
        VerifySettingsStore,
        VerifyCardClouds,
        VerifyReactions,
        VerifyBubbleLines,
        VerifyBubbleVoice,
        VerifyCloudPlacement
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

    private static void VerifyMadScienceRider()
    {
        Assert(VulnerableCardPredicate.IsSupported("MAD_SCIENCE", "Sapping"), "A Mad Science copy with the Sapping rider applies Vulnerable on every play.");
        foreach (var rider in new[] { "None", "Violence", "Choking", "Energized", "Wisdom", "Chaos", "Expertise", "Curious", "Improvement", null })
        {
            Assert(!VulnerableCardPredicate.IsSupported("MAD_SCIENCE", rider), $"A Mad Science copy with the {rider ?? "no"} rider stays excluded.");
        }

        Assert(!VulnerableCardPredicate.IsSupported("BASH_LIKE", "Sapping"), "The Sapping rider counts only on Mad Science.");
        Assert(VulnerableCardPredicate.IsSupported("BASH", null) && VulnerableCardPredicate.IsSupported("BASH", "Sapping"), "Supported ids stay supported with or without a rider name.");
        Assert(!VulnerableCardPredicate.IsSupported("MOLTEN_FIST", null) && !VulnerableCardPredicate.IsSupported("MISERY", null), "Molten Fist and Misery stay excluded.");
    }

    private static void VerifyIndicatorLevel()
    {
        Assert(IndicatorRule.Level(true, true, true, true, true, true) == IndicatorLevel.Full, "A playable supported card in the Play phase gives the full icon.");
        Assert(IndicatorRule.Level(true, true, true, true, true, false) == IndicatorLevel.Dimmed, "A supported card that cannot be played now gives the dimmed icon.");
        Assert(IndicatorRule.Level(true, true, true, true, false, false) == IndicatorLevel.Off, "No supported card gives no icon.");
        Assert(IndicatorRule.Level(true, true, true, false, true, true) == IndicatorLevel.Off, "Outside the Play phase, a kept card gives no icon.");
        Assert(IndicatorRule.Level(true, false, true, true, true, true) == IndicatorLevel.Off, "A disconnected player gives no icon.");
        Assert(IndicatorRule.Level(true, true, false, true, true, true) == IndicatorLevel.Off, "A dead player gives no icon.");
        Assert(IndicatorRule.Level(false, true, true, true, true, true) == IndicatorLevel.Off,
            "A combat that is ending gives no icon, though every phase is still Play then.");
    }

    private static void VerifyCommitGate()
    {
        Assert(IndicatorRule.CanCommit(ActionPhase.None), "With no action running, a new level shows.");
        Assert(!IndicatorRule.CanCommit(ActionPhase.Running), "While an action runs, a new level waits.");
        Assert(IndicatorRule.CanCommit(ActionPhase.WaitingForChoice), "While an action waits for a player's choice, a new level shows, so no icon freezes.");
    }

    private static void VerifyRowTransitions()
    {
        var state = new RowIndicatorState();
        Assert(state.Committed == IndicatorLevel.Off, "A row starts off.");

        var arrived = state.Observe(IndicatorLevel.Full, true);
        Assert(arrived is { BecameFull: true, LeftFull: false }, "Off to full speaks.");
        Assert(state.Observe(IndicatorLevel.Full, true) is null, "The same level again changes nothing.");

        var spent = state.Observe(IndicatorLevel.Dimmed, true);
        Assert(spent is { BecameFull: false, LeftFull: true }, "Full to dimmed takes the arrival bubble down.");
        var regained = state.Observe(IndicatorLevel.Full, true);
        Assert(regained is { BecameFull: true }, "Dimmed to full speaks again.");

        Assert(state.Observe(IndicatorLevel.Dimmed, false) is null && state.Committed == IndicatorLevel.Full,
            "During a running action the energy just spent does not show yet.");
        Assert(state.Observe(IndicatorLevel.Full, true) is null && state.Committed == IndicatorLevel.Full,
            "A spend-then-gain play ends full again with no change and no second bubble.");

        var played = state.Observe(IndicatorLevel.Off, true);
        Assert(played is { LeftFull: true, BecameFull: false }, "Full to off takes the arrival bubble down.");
        var drawnBroke = state.Observe(IndicatorLevel.Dimmed, true);
        Assert(drawnBroke is { BecameFull: false, LeftFull: false }, "Off to dimmed stays quiet.");
        Assert(state.Observe(IndicatorLevel.Off, true) is { BecameFull: false, LeftFull: false }, "Dimmed to off stays quiet.");

        state.Observe(IndicatorLevel.Full, true);
        Assert(state.Reset() is { LeftFull: true } && state.Committed == IndicatorLevel.Off, "The end of a combat goes off at once.");
        Assert(state.Reset() is null, "A second reset changes nothing.");
    }

    private static void VerifyPendingChange()
    {
        var state = new RowIndicatorState();
        state.Observe(IndicatorLevel.Full, true);
        Assert(!state.IsPending, "A committed level leaves nothing pending.");

        Assert(state.Observe(IndicatorLevel.Dimmed, false) is null && state.IsPending,
            "A change refused while an action runs stays pending, so it is checked again.");
        Assert(state.Observe(IndicatorLevel.Dimmed, true) is { LeftFull: true } && !state.IsPending,
            "When the action pauses for a player's choice, the pending change shows.");

        state.Observe(IndicatorLevel.Full, false);
        Assert(state.IsPending, "A gain during an action waits too.");
        Assert(state.Observe(IndicatorLevel.Dimmed, false) is null && !state.IsPending,
            "A refused level equal to the committed one leaves nothing pending.");
        state.Observe(IndicatorLevel.Off, false);
        Assert(state.Reset() is { From: IndicatorLevel.Dimmed, To: IndicatorLevel.Off } && !state.IsPending,
            "The end of a combat goes off at once and clears a pending change.");
    }

    private static void VerifyReminderLevel()
    {
        Assert(ReminderLevel.IsFull(true, false, true, false), "For a reaction, a teammate is read from the game now.");
        Assert(!ReminderLevel.IsFull(true, false, false, true), "For a reaction, a teammate who just lost the play reads as not full.");
        Assert(ReminderLevel.IsFull(true, true, false, true),
            "For a reaction, your own row keeps its state from before your Attack, so its cost does not hide the reminder.");
        Assert(!ReminderLevel.IsFull(false, false, true, false) && ReminderLevel.IsFull(false, false, false, true),
            "For the card cloud, every row reads its committed level, as its icon shows.");
    }

    private static void VerifyGameBubbleTiming()
    {
        Assert(Math.Abs(GameBubbleTiming.SpeechSeconds(0.5) - 2.4) < 1e-9,
            "A short game speech bubble holds arrivals back past its 1.9 s on screen.");
        Assert(Math.Abs(GameBubbleTiming.SpeechSeconds(4.0) - 4.4) < 1e-9, "A longer one holds them for its fade-in, hold, and fade-out.");
        Assert(Math.Abs(GameBubbleTiming.ThoughtSeconds(1.5) - 2.5) < 1e-9, "A timed game thought holds them for its time and its fade.");
        Assert(Math.Abs(GameBubbleTiming.ThoughtSeconds(0.2) - 2.0) < 1e-9, "A thought shorter than a second holds them as one of a second.");
        Assert(GameBubbleTiming.ThoughtSeconds(null) == GameBubbleTiming.UntimedSeconds, "An untimed game thought holds them for a bounded time.");
    }

    private static void VerifyVulnerableNeed()
    {
        Assert(VulnerableNeed.Of(false, false), "An enemy without Vulnerable or Artifact needs Vulnerable.");
        Assert(!VulnerableNeed.Of(true, false), "An enemy with Vulnerable does not.");
        Assert(!VulnerableNeed.Of(false, true), "An enemy with Artifact does not, because the card would only take an Artifact stack.");

        Assert(VulnerableNeed.AttackHitsNeedingEnemy(true, true, true), "An Attack at an enemy that needs Vulnerable counts.");
        Assert(!VulnerableNeed.AttackHitsNeedingEnemy(true, false, true),
            "An Attack at an enemy that has Vulnerable does not count, though another enemy needs it.");
        Assert(VulnerableNeed.AttackHitsNeedingEnemy(false, false, true), "An Attack with no single enemy target counts when any enemy needs Vulnerable.");
        Assert(!VulnerableNeed.AttackHitsNeedingEnemy(false, false, false), "An Attack with no single enemy target does not count when no enemy needs it.");
    }

    private static void VerifyReactionTurn()
    {
        var first = ReactionTurn.Of(3, new[] { 3, 3 });
        Assert(ReactionTurn.Of(3, new[] { 3, 3 }) == first, "The same turn gives the same key.");
        var extra = ReactionTurn.Of(3, new[] { 4, 3 });
        Assert(extra != first, "An extra turn for one player gives a new key, though the round is the same.");
        var next = ReactionTurn.Of(4, new[] { 5, 4 });
        Assert(next != extra, "The next round gives a new key.");
        Assert(ReactionTurn.Of(4, new[] { 5, 4 }) == next,
            "Two players whose turn numbers differ since an extra turn share one key in the same turn, whoever attacks.");

        var policy = new Policy();
        var rows = new[] { Row("ally") };
        Assert(policy.AttackPlayed(1, "me", false, true, rows).SequenceEqual(new[] { ("ally", 1) }), "The count starts in the first turn.");
        Assert(policy.AttackPlayed(1, "other", false, true, rows).SequenceEqual(new[] { ("ally", 2) }), "Another attacker in the same turn raises the same count.");
        Assert(policy.AttackPlayed(2, "me", false, true, rows).SequenceEqual(new[] { ("ally", 1) }), "A new turn key starts the count again.");
    }

    private static void VerifyArrivalQueue()
    {
        var queue = new ArrivalQueue<string>();
        Assert(queue.Arrive("ally", false) && !queue.IsWaiting("ally"), "With no game bubble, the arrival shows at once.");
        Assert(!queue.Arrive("ally", true) && queue.IsWaiting("ally"), "Over a game bubble, the arrival waits.");
        Assert(queue.GameBubbleEnded("ally") && !queue.IsWaiting("ally"), "When the game bubble ends, the waiting arrival is due.");
        Assert(!queue.GameBubbleEnded("ally"), "A game bubble ending with nothing waiting brings nothing.");

        queue.Arrive("ally", true);
        queue.Cancel("ally");
        Assert(!queue.GameBubbleEnded("ally"), "An arrival whose row left full while it waited is dropped.");

        queue.Arrive("a", true);
        queue.Arrive("b", true);
        queue.Clear();
        Assert(!queue.IsWaiting("a") && !queue.IsWaiting("b"), "Clearing drops every waiting arrival.");

        Assert(ArrivalQueue<string>.MayReact(false) && !ArrivalQueue<string>.MayReact(true), "A reaction due over a game bubble is dropped.");
    }

    private static void VerifySettingsParsing()
    {
        var defaults = PlayerSettings.Default;
        Assert(defaults == new PlayerSettings(100, true, true, true, true), "Defaults are 100% icon, bubbles on, own row on, card reminder on, varied lines on.");

        Assert(PlayerSettings.TryParse("""{"iconScalePercent":150,"showBubble":false,"showOnOwnRow":false,"showHandReminder":false,"variedBubbleLines":false}""", out var full)
            && full == new PlayerSettings(150, false, false, false, false), "A complete file is read as written.");
        Assert(PlayerSettings.TryParse("{}", out var empty) && empty == defaults, "Missing fields take their defaults.");
        Assert(PlayerSettings.TryParse("""{"version":1,"iconScalePercent":120,"showBubble":false,"showOnOwnRow":true}""", out var older)
            && older == new PlayerSettings(120, false, true, true, true), "A v0.2.0 file keeps its bubble choice and reads with the card reminder and varied lines on.");
        Assert(PlayerSettings.TryParse("""{"showHandReminder":false,"handReminderSound":true}""", out var armBuild) && armBuild == defaults with { ShowHandReminder = false },
            "A file from the arm builds keeps its card reminder choice, and its sound choice is ignored.");
        Assert(PlayerSettings.TryParse("""{"iconScalePercent":10}""", out var low) && low.IconScalePercent == 50, "A scale below the range clamps to 50%.");
        Assert(PlayerSettings.TryParse("""{"iconScalePercent":1e9}""", out var high) && high.IconScalePercent == 150, "A scale above the range clamps to 150%.");
        Assert(PlayerSettings.TryParse("""{"iconScalePercent":200}""", out var oldTop) && oldTop.IconScalePercent == 150, "The former 200% top clamps to 150%.");
        Assert(PlayerSettings.TryParse("""{"iconScalePercent":1e400}""", out var beyond) && beyond.IconScalePercent == 150, "A number beyond double's range clamps to 150%.");
        Assert(PlayerSettings.TryParse("""{"iconScalePercent":-1e400}""", out var below) && below.IconScalePercent == 50, "A negative number beyond double's range clamps to 50%.");
        Assert(PlayerSettings.TryParse("""{"iconScalePercent":134}""", out var down) && down.IconScalePercent == 130, "A scale snaps down to its step.");
        Assert(PlayerSettings.TryParse("""{"iconScalePercent":125}""", out var mid) && mid.IconScalePercent == 130, "A scale halfway between steps rounds up.");
        Assert(PlayerSettings.TryParse("""{"iconScalePercent":"big","showBubble":1,"showOnOwnRow":null,"showHandReminder":"yes","variedBubbleLines":"no"}""", out var wrong) && wrong == defaults,
            "Wrong-typed fields take their defaults.");
        Assert(PlayerSettings.TryParse("""{"version":7,"future":[1,2],"showBubble":false}""", out var unknown) && unknown == defaults with { ShowBubble = false },
            "Unknown fields are ignored and known fields still read.");

        foreach (var damaged in new[] { "", "not json", "[1,2]", "\"text\"", "{\"showBubble\":" })
        {
            Assert(!PlayerSettings.TryParse(damaged, out var fallback) && fallback == defaults, $"'{damaged}' is damaged and gives the defaults.");
        }

        var written = new PlayerSettings(70, false, true, true, false);
        Assert(PlayerSettings.TryParse(written.ToJson(), out var roundTrip) && roundTrip == written, "Written settings read back unchanged.");
        Assert(!written.ToJson().Contains("handReminderSound", StringComparison.Ordinal), "The sound option is no longer written.");
        Assert(new PlayerSettings(-5, true, true, true, true).Normalize().IconScalePercent == 50, "Normalize clamps a scale set in code.");
        Assert(Math.Abs(new PlayerSettings(150, true, true, true, true).IconScale - 1.5f) < 0.0001f, "The icon scale is the percent as a factor.");

        Assert(defaults.ShowsCardReminder, "By default the card reminder shows.");
        Assert(!(defaults with { ShowBubble = false }).ShowsCardReminder, "With bubbles off, the card reminder is off too, whatever its own switch says.");
        Assert(!(defaults with { ShowHandReminder = false }).ShowsCardReminder, "With the card reminder off, no cloud shows.");
    }

    private static void VerifyIndicatorVisibilityRule()
    {
        var shown = PlayerSettings.Default;
        var ownRowOff = shown with { ShowOnOwnRow = false };

        Assert(shown.ShownLevel(IndicatorLevel.Off, false) == IndicatorLevel.Off && shown.ShownLevel(IndicatorLevel.Off, true) == IndicatorLevel.Off,
            "No qualifying card shows nothing on any row.");
        Assert(shown.ShownLevel(IndicatorLevel.Full, false) == IndicatorLevel.Full && shown.ShownLevel(IndicatorLevel.Full, true) == IndicatorLevel.Full,
            "By default both your row and teammates' rows show the indicator.");
        Assert(shown.ShownLevel(IndicatorLevel.Dimmed, true) == IndicatorLevel.Dimmed, "The dimmed icon follows the same rule.");
        Assert(ownRowOff.ShownLevel(IndicatorLevel.Full, true) == IndicatorLevel.Off && ownRowOff.ShownLevel(IndicatorLevel.Dimmed, true) == IndicatorLevel.Off,
            "With the own row off, your own row shows nothing.");
        Assert(ownRowOff.ShownLevel(IndicatorLevel.Full, false) == IndicatorLevel.Full, "With the own row off, teammates' rows still show the indicator.");
        Assert((shown with { ShowBubble = false }).ShownLevel(IndicatorLevel.Full, false) == IndicatorLevel.Full, "With bubbles off, the row icon still shows.");
    }

    private static void VerifySettingsStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"WaitIHaveVuln.Harness.{Guid.NewGuid():N}");
        try
        {
            var store = new SettingsStore(Path.Combine(directory, "nested"));
            Assert(store.Load(out var missing) == SettingsLoadOutcome.Missing && missing == PlayerSettings.Default, "A missing file gives the defaults.");
            Assert(!Directory.Exists(Path.Combine(directory, "nested")), "Loading a missing file writes nothing.");

            var saved = new PlayerSettings(140, false, true, false, true);
            store.Save(saved);
            Assert(store.Load(out var loaded) == SettingsLoadOutcome.Loaded && loaded == saved, "Saved settings load back, creating the directory.");
            Assert(!File.Exists(store.FilePath + ".tmp"), "Saving leaves no temporary file.");

            store.Save(saved with { ShowBubble = true });
            Assert(store.Load(out var replaced) == SettingsLoadOutcome.Loaded && replaced.ShowBubble, "A second save replaces the first.");

            File.WriteAllText(store.FilePath + ".tmp", "half-written");
            Assert(store.Load(out var withLeftover) == SettingsLoadOutcome.Loaded && withLeftover.ShowBubble, "A leftover temporary file does not affect loading.");
            store.Save(saved);
            Assert(store.Load(out var afterLeftover) == SettingsLoadOutcome.Loaded && afterLeftover == saved, "Saving over a leftover temporary file succeeds.");

            File.WriteAllText(store.FilePath, "{ damaged");
            Assert(store.Load(out var damaged) == SettingsLoadOutcome.Damaged && damaged == PlayerSettings.Default, "A damaged file gives the defaults.");
            Assert(!File.Exists(store.FilePath) && File.ReadAllText(store.DamagedFilePath) == "{ damaged", "A damaged file is moved aside unchanged.");

            File.WriteAllText(store.FilePath, "still damaged");
            Assert(store.Load(out _) == SettingsLoadOutcome.Damaged && File.ReadAllText(store.DamagedFilePath) == "still damaged",
                "A newer damaged file replaces an older .bad file.");
            Assert(store.Load(out _) == SettingsLoadOutcome.Missing, "After moving a damaged file aside, the next load finds no file.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void VerifyCardClouds()
    {
        var own = Row("me", isOwnRow: true);
        var ally = Row("ally");

        Assert(Policy.ShowsCloud(true, false, true, true, false, true, new[] { own, ally }), "In co-op a teammate with a full icon brings the cloud.");
        Assert(!Policy.ShowsCloud(true, false, true, true, false, true, new[] { own }), "In co-op your own row alone brings no cloud.");
        Assert(Policy.ShowsCloud(true, false, true, true, true, true, new[] { own }), "In solo mode your own row brings the cloud.");

        var notNeeded = new[]
        {
            Row("dimmed or off", isFull: false),
            Row("done", isReadyToEndTurn: true)
        };
        Assert(!Policy.ShowsCloud(true, false, true, true, true, true, notNeeded),
            "Rows without a full icon (no card, cannot play it, dead, disconnected, outside Play) or ready to end turn bring no cloud.");
        Assert(Policy.ShowsCloud(true, false, true, true, false, true, notNeeded.Append(ally)), "One needed teammate among the others brings the cloud.");
        Assert(!Policy.ShowsCloud(true, false, true, true, false, false, new[] { ally }), "No cloud shows when no enemy needs Vulnerable.");
        Assert(!Policy.ShowsCloud(true, false, true, false, false, true, new[] { ally }), "No cloud shows while the local player cannot act.");
        Assert(!Policy.ShowsCloud(false, false, true, true, false, true, new[] { ally }), "Hovering a card that is not an Attack shows no cloud.");
        Assert(!Policy.ShowsCloud(true, true, true, true, false, true, new[] { ally }), "Hovering an Attack that applies Vulnerable itself shows no cloud.");
        Assert(!Policy.ShowsCloud(true, false, false, true, false, true, new[] { ally }), "Hovering an Attack you cannot play now shows no cloud.");

        Assert(Policy.IsWorthAnnouncing(ally, false, true) && !Policy.IsWorthAnnouncing(ally, false, false), "A bubble announces a card only while an enemy needs Vulnerable.");
        Assert(!Policy.IsWorthAnnouncing(own, false, true), "In co-op no bubble announces your own card.");
        Assert(Policy.IsWorthAnnouncing(own, true, true), "In solo mode a bubble announces your own card, which stands for a teammate's.");
        Assert(!Policy.IsWorthAnnouncing(Row("dimmed", isFull: false), false, true), "A dimmed or absent icon is never announced.");
        Assert(!Policy.IsWorthAnnouncing(Row("done", isReadyToEndTurn: true), false, true), "A bubble stays quiet for a player who ended the turn.");
    }

    private static void VerifyReactions()
    {
        var policy = new Policy();
        var rows = new[] { Row("me", isOwnRow: true), Row("ally"), Row("other", isFull: false) };

        Assert(policy.AttackPlayed(1, "me", false, true, rows).SequenceEqual(new[] { ("ally", 1) }),
            "Your Attack makes a teammate with a full icon react, as the first Attack of the turn.");
        Assert(policy.AttackPlayed(1, "other", false, true, rows).SequenceEqual(new[] { ("ally", 2) }), "Another teammate's Attack counts too.");
        Assert(policy.AttackPlayed(1, "me", false, true, rows).SequenceEqual(new[] { ("ally", 3) }), "Each Attack in the turn raises the count.");
        Assert(policy.AttackPlayed(1, "ally", false, true, rows).Count == 0, "The holder's own Attack brings no reaction.");
        Assert(policy.AttackPlayed(1, "me", false, false, rows).Count == 0, "An Attack that hits no enemy needing Vulnerable brings no reaction.");
        Assert(policy.AttackPlayed(1, "me", false, true, rows).SequenceEqual(new[] { ("ally", 4) }), "An Attack nobody reacts to does not count.");
        Assert(policy.AttackPlayed(2, "me", false, true, rows).SequenceEqual(new[] { ("ally", 1) }), "A new turn starts the count again.");

        foreach (var (row, what) in new[]
        {
            (Row("ally", isFull: false), "whose icon is dimmed or off"),
            (Row("ally", isReadyToEndTurn: true), "who ended the turn")
        })
        {
            Assert(policy.AttackPlayed(2, "me", false, true, new[] { row }).Count == 0, $"A teammate {what} does not react.");
        }

        var two = new Policy();
        var holders = new[] { Row("a"), Row("b") };
        Assert(two.AttackPlayed(1, "me", false, true, holders).SequenceEqual(new[] { ("a", 1), ("b", 1) }), "Every needed teammate reacts, in row order.");
        Assert(two.AttackPlayed(1, "a", false, true, holders).SequenceEqual(new[] { ("b", 2) }), "A holder's Attack makes the other holder react, and not itself.");

        Assert(new Policy().AttackPlayed(1, "ally", false, true, new[] { Row("me", isOwnRow: true) }).Count == 0, "In co-op your own row never reacts.");
        Assert(new Policy().AttackPlayed(1, "me", true, true, new[] { Row("me", isOwnRow: true) }).SequenceEqual(new[] { ("me", 1) }),
            "In solo mode your own Attack counts for your own row, which stands for a teammate.");
    }

    private static void VerifyBubbleLines()
    {
        Assert(BubbleLines.CleanName(null) is null && BubbleLines.CleanName("  ") is null && BubbleLines.CleanName("[]") is null, "Missing or empty names give no name.");
        Assert(BubbleLines.CleanName(" [b]Lim[/b] ") == "bLim/b", "Square brackets are removed and the name trimmed.");
        Assert(BubbleLines.CleanName("A\nB") == "AB", "Control characters are removed.");
        var cut = BubbleLines.CleanName("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        Assert(cut is { Length: BubbleLines.MaxNameLength } && cut.EndsWith('…'), "A long name is cut to 16 characters with an ellipsis.");
        // The emoji's two halves sit at the cut, characters 15 and 16.
        var emoji = BubbleLines.CleanName(new string('A', 14) + "\U0001F600" + "BCDEF");
        Assert(emoji is not null && !emoji.Any(char.IsSurrogate) && emoji.Length <= BubbleLines.MaxNameLength, "A cut never splits an emoji in half.");
        Assert(BubbleLines.Fill("{you}, wait!", null, "B") is null && BubbleLines.Fill("{them} here!", "A", null) is null, "A line whose name is missing is skipped.");
        Assert(BubbleLines.Fill("{you}, wait!", "A", null) == "A, wait!" && BubbleLines.Fill("{them} here!", null, "B") == "B here!", "Names fill their places.");
        Assert(BubbleLines.Fill("{attacker}, wait!", "A", "B") is null && BubbleLines.Fill("{attacker}, wait!", null, null, "C") == "C, wait!",
            "The attacker's name fills its place, and a line needing it is skipped without it.");
        Assert(BubbleLines.Row[0] == BubbleLines.Signature && !BubbleLines.Angry.Any(tier => tier.Contains(BubbleLines.Signature)),
            "The signature is a row line and never an angry one.");

        var never = (Func<BubbleLine, bool>)(_ => false);
        var three = new BubbleLinePool(new[] { "a", "b", "c" }, new Random(7));
        var picks = Enumerable.Range(0, 30).Select(_ => three.Next(null, never, null, null)!.Value.Text).ToList();
        for (var pass = 0; pass < 10; pass++)
        {
            Assert(picks.Skip(pass * 3).Take(3).OrderBy(text => text).SequenceEqual(new[] { "a", "b", "c" }), $"Pass {pass} uses every line once.");
        }

        Assert(picks.Zip(picks.Skip(1)).All(pair => pair.First != pair.Second), "No line comes twice in a row, also across refills.");

        var board = new BubbleBoard();
        board.Add(new BubbleLine("a"), 5);
        var avoiding = new BubbleLinePool(new[] { "a", "b" }, new Random(3));
        Assert(Enumerable.Range(0, 6).Select(_ => avoiding.Next(null, line => board.Contains(line, 1), null, null)).All(line => line is null || line.Value.Text == "b"),
            "A line on screen is never picked.");
        Assert(board.Contains(new BubbleLine("a"), 4.9) && !board.Contains(new BubbleLine("a"), 5), "A bubble leaves the board when it goes away.");
        board.Remove(new BubbleLine("a"));
        Assert(!board.Contains(new BubbleLine("a"), 1), "A bubble taken down early leaves the board.");

        var row = new BubbleLinePool(BubbleLines.Row, new Random(11), BubbleLines.Signature);
        Assert(row.Next("fight 1", never, "A", null)!.Value.Text == BubbleLines.Signature, "The first row bubble of a combat is the signature.");
        Assert(Enumerable.Range(0, 7).All(_ => row.Next("fight 1", never, "A", null)!.Value.Text != BubbleLines.Signature),
            "The signature does not repeat within its pass.");
        Assert(row.Next("fight 2", never, "A", null)!.Value.Text == BubbleLines.Signature, "A new combat opens with the signature again.");
        Assert(row.Next("fight 3", line => line.Text == BubbleLines.Signature, "A", null)!.Value.Text != BubbleLines.Signature,
            "When the signature is already on screen, another line opens the combat.");

        var first = BubbleLines.Angry[0];
        var nameless = new BubbleLinePool(first, new Random(5));
        Assert(Enumerable.Range(0, 40).Select(_ => nameless.Next(null, never, null, null)!.Value.Text).All(text => !text.Contains('{')),
            "Without names, lines that need one are never picked.");
        var named = new BubbleLinePool(first, new Random(5));
        var namedPicks = Enumerable.Range(0, first.Count).Select(_ => named.Next(null, never, "A", "B", "C")!.Value.Text).ToHashSet();
        Assert(namedPicks.Count == first.Count && namedPicks.Contains("C, wait!"), "With names, one pass uses every line of a tier, filled.");
    }

    private static void VerifyBubbleVoice()
    {
        var never = (Func<BubbleLine, bool>)(_ => false);
        Assert(BubbleLines.Angry.Count == 3 && BubbleLines.Angry.All(tier => tier.Any(line => line.Contains(BubbleLines.Attacker, StringComparison.Ordinal))),
            "There are three angry tiers, each with a line naming the attacker.");
        Assert(BubbleVoice.AngryTier(1) == 0 && BubbleVoice.AngryTier(2) == 1 && BubbleVoice.AngryTier(3) == 2 && BubbleVoice.AngryTier(9) == 2,
            "The first Attack takes the first tier, the second the second, and every later one the third.");

        var voice = new BubbleVoice(new Random(13));
        for (var count = 1; count <= 4; count++)
        {
            var tier = BubbleLines.Angry[BubbleVoice.AngryTier(count)].Select(line => BubbleLines.Fill(line, "A", "B", "C")).ToHashSet();
            Assert(voice.NextAngry(true, count, never, "A", "B", "C") is { } line && tier.Contains(line.Text), $"Attack {count} picks from its tier.");
        }

        Assert(voice.NextAngry(false, 3, never, "A", "B", "C")?.Text == BubbleLines.FixedAngry, "With varied lines off, every Attack gets the fixed line.");
        Assert(voice.NextAngry(false, 1, line => line.Text == BubbleLines.FixedAngry, "A", "B", "C")?.Text == BubbleLines.FixedAngry
            && voice.NextRow(false, "fight", line => line.Text == BubbleLines.Signature, "A", "B")?.Text == BubbleLines.Signature,
            "With varied lines off, a fixed line shows for every speaker, even while another says it.");
        Assert(voice.NextRow(false, "fight", never, "A", "B")?.Text == BubbleLines.Signature && voice.NextRow(false, "fight", never, "A", "B")?.Text == BubbleLines.Signature,
            "With varied lines off, the row always says the signature.");
        Assert(voice.NextRow(true, "fight", never, "A", "B")?.Text == BubbleLines.Signature, "With varied lines on, each combat still opens with the signature.");

        var noAttacker = new BubbleVoice(new Random(17));
        Assert(Enumerable.Range(0, 40).Select(_ => noAttacker.NextAngry(true, 1, never, "A", "B", null)!.Value.Text).All(text => !text.Contains('{')),
            "Without the attacker's name, the line naming them is never picked.");
    }

    private static void VerifyCloudPlacement()
    {
        var screen = new Box(0, 0, 1920, 1080);
        var card = new Box(800, 400, 300, 420);
        const float width = 80;
        const float height = 60;
        const float gap = 10;
        var tipsRight = new[] { new Box(1110, 450, 300, 200) };
        var tipsLeft = new[] { new Box(490, 450, 300, 200) };

        Assert(new Box(0, 0, 10, 10).Intersects(new Box(5, 5, 10, 10)) && !new Box(0, 0, 10, 10).Intersects(new Box(10, 0, 10, 10)),
            "Boxes that overlap intersect, and boxes that only touch do not.");
        Assert(new Box(10, 10, 20, 20).Grow(5) == new Box(5, 5, 30, 30), "A box grows by the same amount on every side.");

        var left = CloudPlacement.Place(card, tipsRight, screen, width, height, gap);
        Assert(left is { Side: CloudSide.Left } spot && spot.Cloud == new Box(710, 400, width, height),
            "With tips on the right, the cloud sits at the card's top-left corner, a gap away.");
        var right = CloudPlacement.Place(card, tipsLeft, screen, width, height, gap);
        Assert(right is { Side: CloudSide.Right } other && other.Cloud == new Box(1110, 400, width, height),
            "With tips on the left, the cloud sits at the card's top-right corner.");
        Assert(CloudPlacement.Place(new Box(200, 400, 300, 420), Array.Empty<Box>(), screen, width, height, gap) is { Side: CloudSide.Right },
            "Without tips, the side with more room comes first: right of a card on the left.");
        Assert(CloudPlacement.Place(new Box(1400, 400, 300, 420), Array.Empty<Box>(), screen, width, height, gap) is { Side: CloudSide.Left },
            "Without tips, a card on the right gets its cloud on the left.");

        var farLeft = new Box(5, 400, 300, 420);
        Assert(CloudPlacement.Place(farLeft, new[] { new Box(315, 450, 300, 200) }, screen, width, height, gap) is { Side: CloudSide.Above } above
            && above.Cloud == new Box(115, 330, width, height),
            "The far-left card with tips on its right gets the cloud above it, centred.");
        var farRight = new Box(1615, 400, 300, 420);
        Assert(CloudPlacement.Place(farRight, new[] { new Box(1305, 450, 300, 200) }, screen, width, height, gap) is { Side: CloudSide.Above },
            "The far-right card with tips on its left gets the cloud above it.");
        Assert(CloudPlacement.Place(farLeft, Array.Empty<Box>(), screen, width, height, gap) is { Side: CloudSide.Right },
            "The far-left card without tips gets the cloud on its right.");

        Assert(CloudPlacement.Place(card, tipsLeft.Concat(tipsRight).ToList(), screen, width, height, gap) is { Side: CloudSide.Above } bothSides
            && bothSides.Cloud == new Box(910, 330, width, height),
            "With tips on both sides and room above, the cloud sits above the card.");

        var top = new Box(800, 20, 300, 420);
        Assert(CloudPlacement.Place(top, new[] { new Box(490, 30, 300, 200), new Box(1110, 30, 300, 200) }, screen, width, height, gap) is null,
            "With tips on both sides and no room above, no cloud shows.");

        Assert(CloudPlacement.Place(new Box(800, 1030, 300, 40), Array.Empty<Box>(), screen, width, height, gap) is { Side: CloudSide.Above } low
            && low.Cloud == new Box(910, 960, width, height),
            "A card too low for a cloud beside its top gets the cloud above it.");

        foreach (var placed in new[] { left, right })
        {
            Assert(placed is { } found && !found.Cloud.Intersects(card) && found.Cloud.IsInside(screen),
                "A cloud never overlaps the hovered card or leaves the screen.");
        }

        Assert(CloudPlacement.Place(card, tipsRight, screen, 0, height, gap) is null, "A cloud without a size gets no spot.");

        Assert(!CloudPlacement.IsSettled(0, 0.1), "The cloud waits a moment even for a card already at rest.");
        Assert(CloudPlacement.IsSettled(0.5f, 0.15), "After that moment, a card within a unit of its spot has settled.");
        Assert(!CloudPlacement.IsSettled(26, 0.15), "A card still sliding sideways to its spot has not settled.");
        Assert(CloudPlacement.IsSettled(26, 0.6), "After the longest wait the cloud shows even if the card has not settled.");
    }

    private static HandReminderRow<string> Row(string player, bool isOwnRow = false, bool isFull = true, bool isReadyToEndTurn = false)
    {
        return new HandReminderRow<string>(player, isOwnRow, isFull, isReadyToEndTurn);
    }

    private static void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition)
        {
            throw new InvalidOperationException($"Regression failure: {message}");
        }
    }
}
