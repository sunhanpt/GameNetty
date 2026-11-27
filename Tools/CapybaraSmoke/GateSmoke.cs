using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text.Json;
using CapybaraRebuild;
using ET;
using MemoryPack;

internal static class GateSmoke
{
    public static async Task Run(Uri router)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
        var catalog = AdventureCatalog.Server;
        var account = "capybara-smoke-" + Guid.NewGuid().ToString("N");
        var cancellation = timeout.Token;
        int rpcId = 0;

        using var route = JsonDocument.Parse(await http.GetStringAsync(new Uri(router, "/get_router"), cancellation));
        string realmAddress = route.RootElement.EnumerateObject().Single(field =>
            string.Equals(field.Name, "Realms", StringComparison.OrdinalIgnoreCase)).Value[0].GetString();

        G2C_CapybaraAdventure spin;
        using (var gate = await Login())
        {
            var wrongCatalog = Request(JourneyAction.Snapshot, 0);
            wrongCatalog.CatalogVersion = "old";
            var wrongVersion = await Call(gate, wrongCatalog);
            Require(wrongVersion.Error == 210103 && wrongVersion.StateJson == null, "Catalog mismatch");
            var initial = await Call(gate, Request(JourneyAction.Snapshot, 0));
            Require(initial.Error == 0 && initial.Revision == 0 && Read(initial).run == null, "Initial snapshot");

            var head = await Race(gate, JourneyAction.ReplayOpening, initial.Revision, id: 1);
            var firstSave = Read(head);
            int fee = catalog.Data.chapters.Single(chapter => chapter.id == 1).stamina;
            Require(firstSave.run.phase == Phase.Skill && firstSave.profile.stamina == Read(initial).profile.stamina - fee,
                "Concurrent chapter start charges one entry fee");
            var invalid = await Call(gate, Request(JourneyAction.SelectSkill, head.Revision, int.MaxValue));
            Require(invalid.Error == 210101 && invalid.Revision == head.Revision, "Invalid skill leaves the revision intact");

            head = await AdvanceUntil(gate, head, view => view.HasWheel);
            Require(Read(head).run.stage == 6, "Recorded turntable day");
            head = await Apply(gate, JourneyAction.OpenWheel, head);
            spin = await Apply(gate, JourneyAction.SpinWheel, head);
            Require(Read(spin).run.pendingWheelRewardId != 0 && AdventureCatalog.Read<AdventureResult>(spin.ResultJson).wheelIndex >= 0,
                "Persisted turntable result");
        }
        Console.WriteLine("PASS Router, Realm, Gate, catalog check and simultaneous first writes.");

        using (var resumed = await Login())
        {
            var head = await Call(resumed, Request(JourneyAction.Snapshot, 0));
            Require(head.Error == 0 && head.Revision == spin.Revision && head.StateJson == spin.StateJson, "Restore turntable after reconnect");
            head = await Race(resumed, JourneyAction.ResolveWheel, head.Revision);
            Require(Read(head).run.pendingWheelRewardId == 0, "Concurrent turntable claims settle once");
            head = await AdvanceUntil(resumed, head, view => view.Phase == Phase.Battle);
            Require(Read(head).run.stage == 7, "Recorded battle day");

            int previousRound = Read(head).run.round;
            var report = await Race(resumed, JourneyAction.PlayRound, head.Revision);
            var playback = AdventureCatalog.Read<AdventureResult>(report.ResultJson).round;
            Require(playback.number == previousRound + 1 && playback.actions.Length > 0, "Round report and retry");
            head = Read(report).run.phase == Phase.Result ? report : await Apply(resumed, JourneyAction.Abandon, report);
            var settled = await Race(resumed, JourneyAction.ReturnToCamp, head.Revision);
            Require(Read(settled).run == null, "Concurrent settlement returns to camp once");

            using var returned = await Login();
            var persisted = await Call(returned, Request(JourneyAction.Snapshot, 0));
            Require(persisted.Error == 0 && persisted.Revision == settled.Revision && persisted.StateJson == settled.StateJson,
                "Restore settled account after login");
            var demo = await Exchange<C2G_Adventure, G2C_Adventure>(returned, new C2G_Adventure { RpcId = ++rpcId },
                AdventureMessage.C2G_Adventure, AdventureMessage.G2C_Adventure, cancellation);
            Require(demo.Error == 0 && demo.Revision == 0, "Independent framework demo save");
        }
        Console.WriteLine("PASS Reconnect, concurrent turntable and battle commands, settlement and independent demo save.");

        async Task<ClientWebSocket> Login()
        {
            using var realm = new ClientWebSocket();
            await realm.ConnectAsync(new Uri("ws://" + realmAddress + "/"), cancellation);
            var ticket = await Exchange<C2R_Login, R2C_Login>(realm,
                new C2R_Login { Account = account, RpcId = ++rpcId },
                OuterMessage.C2R_Login, OuterMessage.R2C_Login, cancellation);
            Require(ticket.Error == 0 && ticket.Key != 0, "Realm login");
            var gate = new ClientWebSocket();
            try
            {
                await gate.ConnectAsync(new Uri("ws://" + ticket.Address + "/"), cancellation);
                var login = await Exchange<C2G_LoginGate, G2C_LoginGate>(gate,
                    new C2G_LoginGate { RpcId = ++rpcId, Key = ticket.Key, GateId = ticket.GateId },
                    OuterMessage.C2G_LoginGate, OuterMessage.G2C_LoginGate, cancellation);
                Require(login.Error == 0 && login.PlayerId != 0, $"Gate login ({login.Error})");
                return gate;
            }
            catch
            {
                gate.Dispose();
                throw;
            }
        }

        C2G_CapybaraAdventure Request(JourneyAction action, long revision, int id = 0) => new()
        {
            RpcId = ++rpcId,
            CatalogVersion = catalog.Version,
            CommandId = Guid.NewGuid().ToString("N"),
            Action = (int)action,
            ExpectedRevision = revision,
            Id = id
        };

        Task<G2C_CapybaraAdventure> Call(ClientWebSocket gate, C2G_CapybaraAdventure request) =>
            Exchange<C2G_CapybaraAdventure, G2C_CapybaraAdventure>(gate, request,
                CapybaraMessage.C2G_CapybaraAdventure, CapybaraMessage.G2C_CapybaraAdventure, cancellation);

        async Task<G2C_CapybaraAdventure> Apply(ClientWebSocket gate, JourneyAction action, G2C_CapybaraAdventure state, int id = 0)
        {
            var response = await Call(gate, Request(action, state.Revision, id));
            Require(response.Error == 0 && response.Revision == state.Revision + 1, $"{action} ({response.Error})");
            return response;
        }

        async Task<G2C_CapybaraAdventure> AdvanceUntil(ClientWebSocket gate, G2C_CapybaraAdventure head, Func<Adventure, bool> done)
        {
            for (int step = 0; step < 25; step++)
            {
                var view = new Adventure(catalog.Data, Read(head), null);
                if (done(view)) return head;
                head = view.Phase switch
                {
                    Phase.Trail => await Apply(gate, JourneyAction.Advance, head),
                    Phase.Skill => await Apply(gate, JourneyAction.SelectSkill, head, view.Run.choices[0]),
                    Phase.Event => await Apply(gate, JourneyAction.ResolveEvent, head),
                    _ => throw new InvalidOperationException($"Unexpected journey phase {view.Phase}.")
                };
            }
            throw new InvalidOperationException("Journey checkpoint not reached.");
        }

        async Task<G2C_CapybaraAdventure> Race(ClientWebSocket gate, JourneyAction action, long revision, int id = 0)
        {
            var left = Request(action, revision, id);
            var right = Request(action, revision, id);
            await Write(gate, left, CapybaraMessage.C2G_CapybaraAdventure, cancellation);
            await Write(gate, right, CapybaraMessage.C2G_CapybaraAdventure, cancellation);
            var pair = new[]
            {
                await Receive<G2C_CapybaraAdventure>(gate, CapybaraMessage.G2C_CapybaraAdventure, cancellation),
                await Receive<G2C_CapybaraAdventure>(gate, CapybaraMessage.G2C_CapybaraAdventure, cancellation)
            };
            Require(pair.Select(reply => reply.RpcId).ToHashSet().SetEquals(new[] { left.RpcId, right.RpcId }) &&
                pair.Select(reply => reply.Error).Order().SequenceEqual(new[] { 0, 210104 }) &&
                pair.All(reply => reply.Revision == revision + 1), $"Concurrent {action} has one commit");
            var winner = pair.Single(reply => reply.Error == 0);
            Require(pair.Single(reply => reply.Error != 0).StateJson == winner.StateJson, "Conflict reports the committed snapshot");
            var retry = winner.RpcId == left.RpcId ? left : right;
            retry.RpcId = ++rpcId;
            var replay = await Call(gate, retry);
            Require(replay.Error == 0 && replay.Revision == winner.Revision && replay.StateJson == winner.StateJson &&
                replay.ResultJson == winner.ResultJson, $"{action} retry returns the committed result");
            retry.RpcId = ++rpcId;
            retry.Id++;
            var changed = await Call(gate, retry);
            Require(changed.Error == 210104 && changed.Revision == winner.Revision && changed.ResultJson == null,
                $"{action} changed payload cannot replay");
            return winner;
        }
    }

    private static Save Read(G2C_CapybaraAdventure response) => AdventureCatalog.Read<Save>(response.StateJson);

    private static async Task<TResponse> Exchange<TRequest, TResponse>(ClientWebSocket socket, TRequest request,
        ushort opcode, ushort responseOpcode, CancellationToken cancellation) where TRequest : IRequest where TResponse : IResponse
    {
        await Write(socket, request, opcode, cancellation);
        var response = await Receive<TResponse>(socket, responseOpcode, cancellation);
        Require(response.RpcId == request.RpcId, $"Response RPC {request.RpcId}");
        return response;
    }

    private static Task Write<T>(ClientWebSocket socket, T request, ushort opcode, CancellationToken cancellation)
    {
        var body = MemoryPackSerializer.Serialize(request);
        var packet = new byte[body.Length + sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, opcode);
        body.CopyTo(packet, sizeof(ushort));
        return socket.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, cancellation);
    }

    private static async Task<T> Receive<T>(ClientWebSocket socket, ushort opcode, CancellationToken cancellation)
    {
        using var packet = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult frame;
        do
        {
            frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
            Require(frame.MessageType == WebSocketMessageType.Binary, $"Expected binary WebSocket response {opcode}");
            packet.Write(buffer, 0, frame.Count);
        } while (!frame.EndOfMessage);

        var bytes = packet.ToArray();
        Require(bytes.Length >= sizeof(ushort) && BinaryPrimitives.ReadUInt16LittleEndian(bytes) == opcode,
            $"Expected opcode {opcode}");
        return MemoryPackSerializer.Deserialize<T>(bytes.AsSpan(sizeof(ushort)));
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }
}
