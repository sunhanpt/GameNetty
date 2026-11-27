using System;
using System.Security.Cryptography;
using System.Threading;
using CapybaraRebuild;
using MongoDB.Driver;

namespace ET.Server
{
    [MessageSessionHandler(SceneType.Gate)]
    [FriendOf(typeof(DBComponent))]
    public sealed class C2G_CapybaraAdventureHandler : MessageSessionHandler<C2G_CapybaraAdventure, G2C_CapybaraAdventure>
    {
        private const int ActionRejected = 210101;
        private const int StorageUnavailable = 210102;
        private const int CatalogMismatch = 210103;
        private const int RevisionConflict = 210104;

        protected override async ETTask Run(Session session, C2G_CapybaraAdventure request, G2C_CapybaraAdventure response)
        {
            Player player = session.GetComponent<SessionPlayerComponent>()?.Player;
            long sessionInstanceId = session.InstanceId;
            if (!IsCurrentSession(session, player, sessionInstanceId))
            {
                Reject(response, ErrorCore.ERR_UnverifiedSessionSendMessage, "登录会话已失效，请重新登录。");
                return;
            }

            AdventureCatalog catalog = AdventureCatalog.Server;
            if (!string.Equals(request.CatalogVersion, catalog.Version, StringComparison.Ordinal))
            {
                Reject(response, CatalogMismatch, "冒险配置已更新，请更新客户端后登录。");
                return;
            }

            long playerId = player.Id;
            DateTime gameDay = DateTime.UtcNow.AddHours(8).Date;
            response.GameDayTicks = gameDay.Ticks;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var db = session.Root().GetComponent<DBManagerComponent>().GetZoneDB(session.Zone());
                var collection = db.database.GetCollection<CapybaraProgress>("CapybaraProgress")
                    .WithWriteConcern(WriteConcern.WMajority.With(journal: true));
                var filters = Builders<CapybaraProgress>.Filter;
                var account = filters.Eq(row => row.PlayerId, playerId);
                CapybaraProgress progress = await collection.Find(account).FirstOrDefaultAsync(timeout.Token);
                if (!IsCurrentSession(session, player, sessionInstanceId))
                {
                    Reject(response, ErrorCore.ERR_UnverifiedSessionSendMessage, "登录会话已失效，请重新登录。");
                    return;
                }

                if (progress == null)
                {
                    var initial = new Adventure(catalog.Data, null, null, () => gameDay);
                    progress = new CapybaraProgress { PlayerId = playerId, StateJson = AdventureCatalog.Write(initial.Save) };
                }
                WriteSnapshot(response, progress);

                if (request.Action == (int)JourneyAction.Snapshot) return;
                if (!Guid.TryParseExact(request.CommandId, "N", out Guid commandId) || commandId == Guid.Empty)
                {
                    Reject(response, ActionRejected, "冒险操作编号无效。");
                    return;
                }
                string operationId = commandId.ToString("N");
                string replayKey = CapybaraProgress.ReplayKey(request, operationId);
                if (progress.IsReplay(request.ExpectedRevision, operationId, replayKey))
                {
                    response.ResultJson = progress.LastResultJson;
                    return;
                }
                if (request.ExpectedRevision != progress.Revision)
                {
                    Reject(response, RevisionConflict, "冒险进度已同步，请确认后重新操作。");
                    return;
                }

                Save save = AdventureCatalog.Read<Save>(progress.StateJson);
                if (save?.profile == null || save.version != 1)
                    throw new InvalidOperationException($"Invalid Capybara save for player {playerId}");
                var game = new Adventure(catalog.Data, save, null, () => gameDay);
                var command = new AdventureCommand((JourneyAction)request.Action, request.Id, request.Option,
                    request.Currency, request.Cost, request.Draws);
                var result = game.Execute(command, RandomNumberGenerator.GetInt32(int.MaxValue));
                if (result == null)
                {
                    Reject(response, ActionRejected, "当前进度无法执行该操作。");
                    return;
                }

                var next = new CapybaraProgress
                {
                    PlayerId = playerId,
                    Revision = progress.Revision + 1,
                    StateJson = AdventureCatalog.Write(game.Save),
                    LastCommandId = replayKey,
                    LastResultJson = AdventureCatalog.Write(result)
                };
                if (!IsCurrentSession(session, player, sessionInstanceId))
                {
                    Reject(response, ErrorCore.ERR_UnverifiedSessionSendMessage, "登录会话已失效，请重新登录。");
                    return;
                }
                var revision = account & filters.Eq(row => row.Revision, progress.Revision);
                bool committed;
                try
                {
                    var write = await collection.ReplaceOneAsync(revision, next,
                        new ReplaceOptions { IsUpsert = progress.Revision == 0 }, timeout.Token);
                    committed = write.MatchedCount == 1 || write.UpsertedId != null;
                }
                catch (MongoWriteException error) when (error.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    committed = false;
                }
                if (!committed)
                {
                    next = await collection.Find(account).FirstAsync(timeout.Token);
                    if (!next.IsReplay(request.ExpectedRevision, operationId, replayKey))
                        Reject(response, RevisionConflict, "冒险进度已同步，请确认后重新操作。");
                }
                if (!IsCurrentSession(session, player, sessionInstanceId))
                {
                    response.StateJson = null;
                    Reject(response, ErrorCore.ERR_UnverifiedSessionSendMessage, "登录会话已失效，请重新登录。");
                    return;
                }
                WriteSnapshot(response, next);
                if (response.Error == 0) response.ResultJson = next.LastResultJson;
            }
            catch (Exception error) when (error is MongoException or TimeoutException or OperationCanceledException)
            {
                Log.Error($"capybara storage unavailable for player={playerId}: {error.GetType().Name}");
                response.StateJson = null;
                Reject(response, StorageUnavailable, "暂时无法读取或保存进度，请重试。");
            }
        }

        private static bool IsCurrentSession(Session session, Player player, long sessionInstanceId) =>
            player != null && !player.IsDisposed && session.InstanceId == sessionInstanceId &&
            player.GetComponent<PlayerSessionComponent>()?.Session == session;

        private static void WriteSnapshot(G2C_CapybaraAdventure response, CapybaraProgress progress)
        {
            response.Revision = progress.Revision;
            response.StateJson = progress.StateJson;
        }

        private static void Reject(G2C_CapybaraAdventure response, int code, string message)
        {
            response.Error = code;
            response.Message = message;
        }
    }
}
