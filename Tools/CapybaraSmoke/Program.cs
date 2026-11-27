using System.Net.Sockets;
using System.Net.WebSockets;
using CapybaraRebuild;
using ET;
using ET.Server;
using MemoryPack;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

if (args is ["--server", var address] && Uri.TryCreate(address, UriKind.Absolute, out var router) &&
    router.Scheme == Uri.UriSchemeHttp && router.IsLoopback)
{
    try
    {
        await GateSmoke.Run(router);
    }
    catch (Exception error) when (error is HttpRequestException or WebSocketException or OperationCanceledException)
    {
        Exception cause = error.GetBaseException();
        string detail = cause is SocketException socket ? $"Socket {socket.NativeErrorCode} ({socket.SocketErrorCode})" : cause.Message;
        Console.Error.WriteLine($"GameNetty connection failed for {router.Host}:{router.Port} ({detail}).");
        Environment.ExitCode = 1;
    }
    return;
}
if (args is ["--catalog"])
{
    CatalogSmoke.Run(AdventureCatalog.Server.Data);
    return;
}
if (args is ["--enemy-rigs", var resources])
{
    Environment.ExitCode = EnemyRigCoverage.Check(AdventureCatalog.Server.Data, resources);
    return;
}
if (args.Length != 0)
    throw new ArgumentException("Usage: CapybaraSmoke [--catalog | --enemy-rigs RESOURCE_DIRECTORY | --server http://127.0.0.1:30300/]");

var catalog = AdventureCatalog.Server;
Require(catalog.Data.chapters.Length == 300 && catalog.Data.challenges.Length == 6889, "Embedded chapter and challenge catalogs");
var day = new DateTime(2026, 11, 27);
var game = new Adventure(catalog.Data, null, null, () => day);
Require(game.Execute(new AdventureCommand(JourneyAction.PlayRound), 0) == null, "Battle command from camp");
Require(game.Execute(new AdventureCommand(JourneyAction.Start, 999), 0) == null, "Unknown chapter");
Apply(JourneyAction.ReplayOpening, id: 1);
Require(game.Phase == Phase.Skill && game.Run.replayOpening, "Recorded opening and first skill");
Apply(JourneyAction.SelectSkill, id: game.Run.choices[0]);

for (int i = 0; i < 25 && !game.HasWheel; i++)
{
    if (game.Phase == Phase.Skill)
        Apply(JourneyAction.SelectSkill, id: game.Run.choices[0]);
    else if (game.Phase == Phase.Event)
        Apply(JourneyAction.ResolveEvent);
    else
        Apply(JourneyAction.Advance);
}
Require(game.HasWheel && game.Run.stage == 6, "Recorded opening turntable");
Require(game.Execute(new AdventureCommand(JourneyAction.ResolveWheel), 0) == null, "Unspun wheel cannot pay out");
Apply(JourneyAction.OpenWheel);
var spin = Apply(JourneyAction.SpinWheel);
Require(spin.wheelIndex >= 0 && game.Run.pendingWheelRewardId != 0, "Persisted turntable target");
int pendingReward = game.Run.pendingWheelRewardId;
Require(Apply(JourneyAction.SpinWheel).wheelIndex == spin.wheelIndex &&
    game.Run.pendingWheelRewardId == pendingReward, "Resume the same turntable target");
Apply(JourneyAction.ResolveWheel);
Require(game.Run.pendingWheelRewardId == 0, "Wheel reward collected");
Require(game.Execute(new AdventureCommand(JourneyAction.ResolveWheel), 0) == null, "Wheel cannot collect twice");

Apply(JourneyAction.Advance);
Require(game.Phase == Phase.Event && game.Run.stage == 7, "Recorded opening battle prompt");
Apply(JourneyAction.ResolveEvent);
Require(game.Phase == Phase.Battle, "Resolve battle prompt");
int previousRound = game.Run.round;
var battleResult = Apply(JourneyAction.PlayRound);
var response = new G2C_CapybaraAdventure
{
    RpcId = 41,
    Revision = 17,
    GameDayTicks = day.Ticks,
    StateJson = AdventureCatalog.Write(game.Save),
    ResultJson = AdventureCatalog.Write(battleResult)
};
var decoded = MemoryPackSerializer.Deserialize<G2C_CapybaraAdventure>(MemoryPackSerializer.Serialize(response));
Require(decoded.RpcId == response.RpcId && decoded.Revision == response.Revision &&
    decoded.GameDayTicks == day.Ticks && decoded.StateJson == response.StateJson, "MemoryPack snapshot");
var playback = AdventureCatalog.Read<AdventureResult>(decoded.ResultJson).round;
Require(playback.number == previousRound + 1 && playback.actions.Length > 0, "Round playback after serialization");
Require(playback.actions.Any(action => action.targetHp >= 0 && action.amount > 0), "Playback includes strike and health");
if (game.Phase != Phase.Result) Apply(JourneyAction.Abandon);
Require(game.Phase == Phase.Result, "Retreat result");
Apply(JourneyAction.ReturnToCamp);
Require(game.Phase == Phase.Camp, "Return to camp");
Require(game.Execute(new AdventureCommand(JourneyAction.ReturnToCamp), 0) == null, "Settlement cannot run twice");
Console.WriteLine("PASS catalog, command validation, snapshot and turntable recovery, MemoryPack battle report and settlement.");

var operation = new C2G_CapybaraAdventure
{
    Action = (int)JourneyAction.OpenShopChest,
    Id = 2,
    Currency = 9,
    Cost = 15,
    Draws = 3,
    CommandId = Guid.NewGuid().ToString("N"),
    ExpectedRevision = 17
};
string replayKey = CapybaraProgress.ReplayKey(operation, operation.CommandId);
var saved = new CapybaraProgress
{
    PlayerId = 21,
    Revision = operation.ExpectedRevision + 1,
    LastCommandId = replayKey,
    StateJson = decoded.StateJson,
    LastResultJson = decoded.ResultJson
};
var document = saved.ToBsonDocument();
var reloadedProgress = BsonSerializer.Deserialize<CapybaraProgress>(document);
Require(reloadedProgress.StateJson == saved.StateJson && reloadedProgress.LastResultJson == saved.LastResultJson &&
    reloadedProgress.IsReplay(operation.ExpectedRevision, operation.CommandId, replayKey), "Committed retry after BSON reload");
operation.RpcId++;
Require(reloadedProgress.IsReplay(operation.ExpectedRevision, operation.CommandId, CapybaraProgress.ReplayKey(operation, operation.CommandId)),
    "Transport RPC identifier does not affect command identity");
operation.Draws++;
Require(!reloadedProgress.IsReplay(operation.ExpectedRevision, operation.CommandId, CapybaraProgress.ReplayKey(operation, operation.CommandId)) &&
    !reloadedProgress.IsReplay(operation.ExpectedRevision - 1, operation.CommandId, replayKey), "Changed payload or revision cannot replay");
document[nameof(CapybaraProgress.LastCommandId)] = operation.CommandId;
var legacyProgress = BsonSerializer.Deserialize<CapybaraProgress>(document);
string otherCommand = Guid.NewGuid().ToString("N");
Require(legacyProgress.IsReplay(operation.ExpectedRevision, operation.CommandId, replayKey) &&
    !legacyProgress.IsReplay(operation.ExpectedRevision, otherCommand, CapybaraProgress.ReplayKey(operation, otherCommand)),
    "Existing BSON progress remains compatible");
Console.WriteLine("PASS BSON round trip, retry identity, lost response and legacy progress compatibility.");

AdventureResult Apply(JourneyAction action, int id = 0)
{
    var result = game.Execute(new AdventureCommand(action, id), 0);
    Require(result != null, $"Expected {action} to succeed in phase {game.Phase}");

    string state = AdventureCatalog.Write(game.Save);
    var restored = AdventureCatalog.Read<Save>(state);
    game = new Adventure(catalog.Data, restored, null, () => day);
    return AdventureCatalog.Read<AdventureResult>(AdventureCatalog.Write(result));
}

static void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
}
