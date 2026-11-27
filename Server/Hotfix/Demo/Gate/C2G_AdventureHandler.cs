using System;
using MongoDB.Driver;

namespace ET.Server
{
    [MessageSessionHandler(SceneType.Gate)]
    public sealed class C2G_AdventureHandler : MessageSessionHandler<C2G_Adventure, G2C_Adventure>
    {
        private const int FirstChapterId = 1;
        private const int ErrAdventureActionRejected = 210001;
        private const int ErrAdventureStorageUnavailable = 210002;

        protected override async ETTask Run(Session session, C2G_Adventure request, G2C_Adventure response)
        {
            Player player = session.GetComponent<SessionPlayerComponent>()?.Player;
            if (!IsCurrentSession(session, player))
            {
                RejectSession(response);
                return;
            }

            long sessionInstanceId = session.InstanceId;
            long playerId = player.Id;
            string account = player.Account;
            try
            {
                DBComponent db = session.Root().GetComponent<DBManagerComponent>().GetZoneDB(session.Zone());
                IMongoCollection<AdventureProgress> collection = AdventureProgressStore.GetCollection(db);
                AdventureProgress progress = await AdventureProgressStore.Read(collection, account);
                if (session.InstanceId != sessionInstanceId || !IsCurrentSession(session, player) || progress?.PlayerId != playerId)
                {
                    RejectSession(response);
                    return;
                }

                AdventureChapterConfigCategory chapters = AdventureChapterConfigCategory.Instance;
                int requestedChapterId = request.ChapterId == 0 ? FirstChapterId : request.ChapterId;
                long utcDay = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400;
                if (request.Action != AdventureAction.Snapshot &&
                    !AdventureProgressSystem.IsReplay(progress, request.Action, request.ExpectedRevision, requestedChapterId, request.EquipmentId, request.TargetId))
                {
                    long previousRunId = progress.RunId;
                    AdventureChapterConfig chosenChapter = request.Action switch
                    {
                        AdventureAction.Begin => chapters.GetOrDefault(requestedChapterId),
                        AdventureAction.BeginChallenge => chapters.GetOrDefault(AdventureChallengeConfigCategory.Instance.GetOrDefault(request.TargetId)?.ChapterId ?? 0),
                        _ => null
                    };
                    string error = request.Action == AdventureAction.Begin && chosenChapter == null
                        ? "所选章节已下线，请重新选择。"
                        : AdventureProgressSystem.Apply(progress, request.Action, request.ExpectedRevision,
                            chosenChapter == null ? null : AdventureProgressSystem.CaptureChapter(chosenChapter),
                            chosenChapter == null ? 0 : IdGenerater.Instance.GenerateId(), request.EquipmentId, request.TargetId, utcDay);
                    if (error == null)
                    {
                        if (await AdventureProgressStore.TryCommit(collection, progress, request.ExpectedRevision))
                        {
                            long runId = progress.RunId != 0 ? progress.RunId : previousRunId;
                            Log.Info($"adventure player={playerId} revision={progress.Revision} run={runId} action={request.Action} phase={progress.Phase} equipment={progress.LastEquipmentId}");
                        }
                        else
                        {
                            progress = await AdventureProgressStore.Read(collection, account);
                            if (progress?.PlayerId != playerId)
                            {
                                RejectSession(response);
                                return;
                            }
                            if (!AdventureProgressSystem.IsReplay(progress, request.Action, request.ExpectedRevision, requestedChapterId, request.EquipmentId, request.TargetId))
                            {
                                error = "进度已更新，请确认当前状态后再操作。";
                            }
                        }
                    }

                    if (error != null)
                    {
                        response.Error = ErrAdventureActionRejected;
                        response.Message = error;
                    }
                }

                WriteSnapshot(response, progress, chapters, utcDay);
            }
            catch (Exception error) when (error is MongoException or TimeoutException or OperationCanceledException)
            {
                Log.Error($"adventure storage unavailable for player={playerId}: {error.GetType().Name}");
                response.Error = ErrAdventureStorageUnavailable;
                response.Message = "暂时无法读取或保存进度，请重试。";
            }
        }

        private static bool IsCurrentSession(Session session, Player player)
        {
            return player != null && player.GetComponent<PlayerSessionComponent>()?.Session == session;
        }

        private static void RejectSession(G2C_Adventure response)
        {
            response.Error = ErrorCore.ERR_UnverifiedSessionSendMessage;
            response.Message = "登录会话已失效，请重新登录。";
        }

        private static void WriteSnapshot(G2C_Adventure response, AdventureProgress progress, AdventureChapterConfigCategory chapters, long utcDay)
        {
            AdventureChapterSnapshot chapter = progress.Chapter ?? AdventureProgressSystem.CaptureChapter(chapters.Get(FirstChapterId));
            AdventureSceneSnapshot scene = chapter.Scene;
            if (scene == null && chapters.GetOrDefault(chapter.Id) is AdventureChapterConfig currentChapter)
            {
                // Older saves did not store presentation. The saved combat values still apply.
                scene = AdventureProgressSystem.CaptureChapter(currentChapter).Scene;
            }
            response.PlayerId = progress.PlayerId;
            response.RunId = progress.RunId;
            response.Revision = progress.Revision;
            response.Phase = progress.Phase;
            response.Coins = progress.Coins;
            response.ChapterId = chapter.Id;
            response.ChallengeId = chapter.ChallengeId;
            response.ConfigVersion = chapter.Version;
            response.ChapterName = chapter.ChapterName;
            response.EnemyName = chapter.EnemyName;
            response.Won = progress.Won;
            response.RewardCoins = progress.Won ? chapter.RewardCoins : 0;
            foreach (AdventureRoundState state in progress.Rounds)
            {
                AdventureRound round = AdventureRound.Create();
                round.Number = state.Number;
                round.PlayerDamage = state.PlayerDamage;
                round.EnemyDamage = state.EnemyDamage;
                round.PlayerHealth = state.PlayerHealth;
                round.EnemyHealth = state.EnemyHealth;
                round.CompanionDamage = state.CompanionDamage;
                response.Rounds.Add(round);
            }
            response.LastSettledRunId = progress.LastSettlement?.RunId ?? 0;
            response.LastSettlementWon = progress.LastSettlement?.Won ?? false;
            response.LastSettlementCoins = progress.LastSettlement?.RewardCoins ?? 0;
            AdventureSceneInfo sceneInfo = AdventureSceneInfo.Create();
            sceneInfo.ResourceKey = scene?.ResourceKey;
            sceneInfo.TravelText = scene?.TravelText;
            sceneInfo.EncounterText = scene?.EncounterText;
            sceneInfo.EventTitle = scene?.EventTitle;
            sceneInfo.EventText = scene?.EventText;
            sceneInfo.TakeLabel = scene?.TakeLabel;
            sceneInfo.SkipLabel = scene?.SkipLabel;
            sceneInfo.PlayerHealth = chapter.PlayerHealth;
            sceneInfo.EnemyHealth = chapter.EnemyHealth;
            sceneInfo.CompanionName = chapter.CompanionName;
            response.Scene = sceneInfo;

            if (progress.Phase == AdventurePhase.Camp)
            {
                foreach (AdventureChapterConfig item in chapters.DataList)
                {
                    AdventureChapterOption option = AdventureChapterOption.Create();
                    option.Id = item.Id;
                    option.Name = item.ChapterName;
                    option.ResourceKey = AdventureMapConfigCategory.Instance.Get(item.MapId).ResourceKey;
                    option.RewardCoins = item.RewardCoins;
                    response.Chapters.Add(option);
                }
                foreach (AdventureEquipmentConfig item in AdventureEquipmentConfigCategory.Instance.DataList)
                {
                    response.Equipment.Add(AdventureEquipmentSystem.Describe(progress, item));
                }
                foreach (AdventureGrowthConfig item in AdventureGrowthConfigCategory.Instance.DataList)
                {
                    response.Growth.Add(AdventureGrowthSystem.Describe(progress, item));
                }
                foreach (AdventureChallengeConfig item in AdventureChallengeConfigCategory.Instance.DataList)
                {
                    response.Challenges.Add(AdventureChallengeSystem.Describe(progress, item, utcDay));
                }
            }
        }
    }
}
