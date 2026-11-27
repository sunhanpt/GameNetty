using System.Threading;
using MongoDB.Driver;

namespace ET.Server
{
    [FriendOf(typeof(DBComponent))]
    public static class AdventureProgressStore
    {
        private const int OperationTimeoutMs = 5000;

        public static IMongoCollection<AdventureProgress> GetCollection(DBComponent db)
        {
            return db.database.GetCollection<AdventureProgress>("AdventureProgress")
                .WithWriteConcern(WriteConcern.WMajority.With(journal: true));
        }

        public static async ETTask<AdventureProgress> GetOrCreate(IMongoCollection<AdventureProgress> collection, string account, long newPlayerId)
        {
            using CancellationTokenSource timeout = new(OperationTimeoutMs);
            var filter = Builders<AdventureProgress>.Filter.Eq(x => x.Account, account);
            var update = Builders<AdventureProgress>.Update.SetOnInsert(x => x.PlayerId, newPlayerId).SetOnInsert(x => x.Revision, 0);
            var options = new FindOneAndUpdateOptions<AdventureProgress> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await collection.FindOneAndUpdateAsync(filter, update, options, timeout.Token);
        }

        public static async ETTask<AdventureProgress> Read(IMongoCollection<AdventureProgress> collection, string account)
        {
            using CancellationTokenSource timeout = new(OperationTimeoutMs);
            return await collection.Find(x => x.Account == account).FirstOrDefaultAsync(timeout.Token);
        }

        public static async ETTask<bool> TryCommit(IMongoCollection<AdventureProgress> collection, AdventureProgress progress, long expectedRevision)
        {
            using CancellationTokenSource timeout = new(OperationTimeoutMs);
            ReplaceOneResult result = await collection.ReplaceOneAsync(
                x => x.Account == progress.Account && x.PlayerId == progress.PlayerId && x.Revision == expectedRevision,
                progress, cancellationToken: timeout.Token);
            return result.MatchedCount == 1;
        }
    }
}
