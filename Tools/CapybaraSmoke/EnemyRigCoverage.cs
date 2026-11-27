using CapybaraRebuild;

internal static class EnemyRigCoverage
{
    internal static int Check(GameData data, string resourceDirectory)
    {
        if (!Directory.Exists(resourceDirectory))
            throw new DirectoryNotFoundException(resourceDirectory);

        var members = data.members.ToDictionary(member => member.id);
        var memberIds = data.monsters.SelectMany(monster => monster.members)
            .Concat(data.challenges.SelectMany(challenge => challenge.waves)
                .SelectMany(wave => wave.members ?? Array.Empty<int>()))
            .Distinct();
        var byModel = memberIds.Select(id => members[id])
            .GroupBy(member => member.modelId)
            .OrderBy(group => group.Key)
            .ToArray();
        int missingRigs = 0;
        foreach (var model in byModel)
        {
            bool mage = model.Key == 220081;
            string folder = mage ? "SpineActors/Mage" : $"EnemyRigs/{model.Key}";
            string[] expected = mage
                ? new[] { "AtlasData.txt", "RigAtlas.png", "Skeleton.bytes", "Rig.bytes" }
                : new[] { "AtlasData.txt", "RigAtlas.png", "Skeleton.bytes" };
            var missingFiles = expected.Where(file => !File.Exists(Path.Combine(resourceDirectory, folder, file))).ToArray();
            if (missingFiles.Length == 0) continue;

            missingRigs++;
            var names = model.Select(member => member.name).Distinct();
            Console.WriteLine($"{model.Key}\t{folder}\t{string.Join(", ", missingFiles)}\t{string.Join(" / ", names)}");
        }
        Console.WriteLine($"{byModel.Length - missingRigs}/{byModel.Length} configured battle models have all expected rig files.");
        return missingRigs == 0 ? 0 : 1;
    }
}
