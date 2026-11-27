using CapybaraRebuild;

internal static class CatalogSmoke
{
    public static void Run(GameData data)
    {
        var day = new DateTime(2026, 11, 27);
        int highest = data.chapters.Max(chapter => chapter.id);
        var resolvedMaps = new HashSet<int>();
        foreach (var chapter in data.chapters)
        {
            string label = $"Chapter {chapter.id}";
            var game = new Adventure(data, new Save { profile = new Profile { highest = chapter.id } }, null, () => day);
            Require(game.Execute(new AdventureCommand(JourneyAction.Start, chapter.id), chapter.id) != null, label, "entry");
            Require(game.Chapter.id == chapter.id && game.Map?.id == chapter.mapId && game.StageCount == chapter.totalStage,
                label, "configuration and map");
            EnterFirstStage(game, label);
            CheckRestore(data, game, day, label);
            resolvedMaps.Add(game.Map.id);
        }
        Console.WriteLine($"PASS {data.chapters.Length} chapters entered, advanced and restored.");

        foreach (var mode in data.challenges.GroupBy(item => item.mode).OrderBy(group => group.Key))
        {
            foreach (var track in mode.GroupBy(item => item.track))
            {
                var levels = mode.Key switch
                {
                    3 => track.OrderBy(item => item.level).ToArray(),
                    6 => track.OrderBy(item => item.id).ToArray(),
                    _ => track.OrderBy(item => item.level).ThenBy(item => item.id).ToArray()
                };
                var cleared = new List<int>();
                for (int index = 0; index < levels.Length; index++)
                {
                    var level = levels[index];
                    string label = $"Challenge {level.mode}/{level.id}";
                    var date = mode.Key == 6 ? RotationDay(day, index, levels.Length) : day;
                    var profile = new Profile { highest = highest };
                    profile.challenges.Add(new ChallengeProgress
                    {
                        mode = mode.Key,
                        track = track.Key,
                        wins = index,
                        hasClearIds = mode.Key == 3,
                        clearedLevels = mode.Key == 3 ? cleared.ToList() : null
                    });
                    var game = new Adventure(data, new Save { profile = profile }, null, () => date);
                    Require(game.Execute(new AdventureCommand(JourneyAction.StartChallenge, level.id, level.mode), level.id) != null,
                        label, "entry");
                    int expectedMap = level.mapId > 0 ? level.mapId : game.Chapter.mapId;
                    Require(game.ActiveChallenge.id == level.id && game.Map?.id == expectedMap && game.StageCount > 0,
                        label, "configuration and map");
                    EnterFirstStage(game, label);
                    CheckRestore(data, game, date, label);
                    resolvedMaps.Add(game.Map.id);
                    if (mode.Key == 3) cleared.Add(level.id);
                }
            }
            Console.WriteLine($"PASS mode {mode.Key}, {mode.Count()} challenges entered, advanced and restored.");
        }
        CheckDailyRoute(data, day, highest, resolvedMaps);
        Console.WriteLine($"PASS {resolvedMaps.Count} distinct maps resolved on chapter and challenge entry and daily segment changes.");
    }

    private static void CheckDailyRoute(GameData data, DateTime day, int highest, HashSet<int> resolvedMaps)
    {
        var game = new Adventure(data, new Save { profile = new Profile { highest = highest } }, null, () => day);
        var expedition = game.ChallengeChoices(6).Single();
        Require(game.Execute(new AdventureCommand(JourneyAction.StartChallenge, expedition.id, expedition.mode), expedition.id) != null,
            "Daily expedition", "entry");
        EnterFirstStage(game, "Daily expedition");
        for (int index = 1; index < data.dailyRoute.Length; index++)
        {
            game.Run.phase = Phase.Trail;
            game.Run.stage = game.Run.dailySegmentStart + game.Chapter.totalStage;
            int previousStage = game.Run.stage;
            string label = $"Daily expedition segment {index + 1}";
            Require(game.Execute(new AdventureCommand(JourneyAction.Advance), game.Run.seed) != null &&
                game.Run.dailySegment == index && game.Run.dailySegmentStart == previousStage && game.Run.stage == previousStage + 1 &&
                game.Map?.id == data.dailyRoute[index].mapId, label, "transition and map");
            CheckRestore(data, game, day, label);
            resolvedMaps.Add(game.Map.id);
        }
        Console.WriteLine($"PASS all {data.dailyRoute.Length} daily expedition segments loaded and restored.");
    }

    private static DateTime RotationDay(DateTime day, int index, int count)
    {
        int current = (int)(day.Ticks / TimeSpan.TicksPerDay % count);
        return day.AddDays((index - current + count) % count);
    }

    private static void EnterFirstStage(Adventure game, string label)
    {
        for (int picks = 0; game.Phase == Phase.Skill && picks < 20; picks++)
        {
            Require(game.Run.choices.Count > 0, label, "opening skill choices");
            Require(game.Execute(new AdventureCommand(JourneyAction.SelectSkill, game.Run.choices[0]), game.Run.seed) != null,
                label, "opening skill selection");
        }
        Require(game.Phase == Phase.Trail && game.Execute(new AdventureCommand(JourneyAction.Advance), game.Run.seed) != null &&
            game.Run.stage == 1, label, "first stage");
        if (game.Phase != Phase.Battle) return;

        Require(game.Run.enemies.Count > 0, label, "first enemy formation");
        var battle = game.Execute(new AdventureCommand(JourneyAction.PlayRound), game.Run.seed);
        Require(battle?.round?.actions?.Length > 0, label, "first battle round");
    }

    private static void CheckRestore(GameData data, Adventure game, DateTime day, string label)
    {
        string snapshot = AdventureCatalog.Write(game.Save);
        var restored = new Adventure(data, AdventureCatalog.Read<Save>(snapshot), null, () => day);
        Require(restored.Run != null && restored.Run.stage == game.Run.stage && restored.Run.seed == game.Run.seed &&
            restored.Run.round == game.Run.round && restored.Run.hp == game.Run.hp && restored.Phase == game.Phase &&
            restored.ActiveChallenge?.id == game.ActiveChallenge?.id && restored.Map?.id == game.Map?.id, label, "snapshot restored");
    }

    private static void Require(bool condition, string label, string check)
    {
        if (!condition) throw new InvalidOperationException($"{label} failed {check}.");
    }
}
