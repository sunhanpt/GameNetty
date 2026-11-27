using System;
using MongoDB.Bson.Serialization.Attributes;

namespace ET.Server
{
    [EnableClass]
    public sealed class CapybaraProgress
    {
        [BsonId]
        public long PlayerId { get; set; }
        public long Revision { get; set; }
        public string StateJson { get; set; }
        public string LastCommandId { get; set; }
        public string LastResultJson { get; set; }

        public static string ReplayKey(C2G_CapybaraAdventure request, string commandId) =>
            FormattableString.Invariant($"{commandId}:{request.Action}/{request.Id}/{request.Option}/{request.Currency}/{request.Cost}/{request.Draws}");

        // Existing progress stores the GUID by itself in this same BSON field.
        public bool IsReplay(long expectedRevision, string commandId, string replayKey) =>
            Revision > 0 && Revision - 1 == expectedRevision && (LastCommandId == replayKey || LastCommandId == commandId);
    }
}
