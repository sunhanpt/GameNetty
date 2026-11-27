using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed class AdventureReply
    {
        public int Error { get; }
        public string Message { get; }
        public long Revision { get; }
        public long GameDayTicks { get; }
        public string StateJson { get; }
        public string ResultJson { get; }

        public AdventureReply(int error, string message, long revision, long gameDayTicks, string stateJson, string resultJson)
        {
            Error = error;
            Message = message;
            Revision = revision;
            GameDayTicks = gameDayTicks;
            StateJson = stateJson;
            ResultJson = resultJson;
        }
    }

    public sealed partial class AdventureScreen
    {
        private AdventureCatalog catalog;
        private Func<AdventureCommand, long, string, Task<AdventureReply>> sendCommand;
        private Action leaveAdventure;
        private long revision;
        private bool syncRequired;
        private AdventureCommand pendingCommand;
        private string pendingCommandId;
        private long pendingRevision;

        public void Bind(AdventureCatalog activeCatalog, AdventureReply snapshot,
            Func<AdventureCommand, long, string, Task<AdventureReply>> send, Action leave)
        {
            if (world == null) throw new InvalidOperationException("Adventure scene is missing its serialized references.");
            catalog = activeCatalog;
            sendCommand = send;
            leaveAdventure = leave;
            ApplySnapshot(snapshot);
            error = null;
            world.Refresh(game);
            Refresh();
        }

        private void ApplySnapshot(AdventureReply reply)
        {
            var save = AdventureCatalog.Read<Save>(reply.StateJson);
            if (save?.version != 1 || save.profile == null)
                throw new InvalidOperationException("Invalid adventure snapshot.");
            var serverDay = new DateTime(reply.GameDayTicks);
            var dayOffset = serverDay - DateTime.UtcNow.AddHours(8).Date;
            game = new Adventure(catalog.Data, save, null, () => DateTime.UtcNow.AddHours(8).Date + dayOffset);
            revision = reply.Revision;
        }

        private IEnumerator RunCommand(AdventureCommand command, Action<AdventureResult> completed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (normalAdventure != null)
            {
                completed(game.Execute(command, Environment.TickCount));
                yield break;
            }
#endif
            if (pendingCommandId == null && command.Action != JourneyAction.Snapshot)
            {
                pendingCommand = command;
                pendingCommandId = Guid.NewGuid().ToString("N");
                pendingRevision = revision;
            }

            var task = sendCommand(pendingCommandId == null ? command : pendingCommand,
                pendingCommandId == null ? revision : pendingRevision, pendingCommandId);
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted || task.IsCanceled)
            {
                error = "连接中断。可重试同步，或返回登录继续旅程。";
                syncRequired = true;
                if (task.Exception != null) Debug.LogWarning(task.Exception.GetBaseException().Message);
                completed(null);
                yield break;
            }

            var reply = task.Result;
            AdventureResult result = null;
            try
            {
                if (!string.IsNullOrEmpty(reply.StateJson))
                {
                    ApplySnapshot(reply);
                    pendingCommandId = null;
                    syncRequired = false;
                }
                else syncRequired = true;
                error = reply.Error == 0 ? null : reply.Message;
                if (reply.Error == 0)
                    result = string.IsNullOrEmpty(reply.ResultJson) ? new AdventureResult() :
                        AdventureCatalog.Read<AdventureResult>(reply.ResultJson);
            }
            catch (Exception exception)
            {
                error = "无法读取服务器进度，请重新登录。";
                syncRequired = true;
                Debug.LogException(exception);
            }
            completed(result);
        }

        private IEnumerator SyncProgress()
        {
            if (busy) yield break;
            busy = true;
            yield return RunCommand(new AdventureCommand(JourneyAction.Snapshot), _ => { });
            busy = false;
            world.Refresh(game);
            Refresh(true);
        }

        private void LeaveAdventure()
        {
            if (busy) return;
            busy = true;
            if (leaveAdventure != null) leaveAdventure();
            else SceneManager.LoadScene("main");
        }

        private void AddSessionButton(RectTransform parent, string label, Action action,
            (float x, float y, float right, float top) anchors)
        {
            var button = InstantiateUi("Widgets/ListAction", parent);
            PlaceUi(button, anchors.x, anchors.y, anchors.right, anchors.top);
            BindUiButton(button, label, action, true);
        }
    }
}
