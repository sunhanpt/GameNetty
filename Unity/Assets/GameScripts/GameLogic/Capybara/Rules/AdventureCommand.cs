using System;
using System.Linq;

namespace CapybaraRebuild
{
    public enum JourneyAction
    {
        Snapshot = 0,
        Start = 1,
        ReplayOpening = 2,
        SelectCampChapter = 3,
        StartChallenge = 4,
        Sweep = 5,
        Rest = 6,
        Advance = 7,
        ResolveEvent = 8,
        OpenWheel = 9,
        SkipWheel = 10,
        SpinWheel = 11,
        ResolveWheel = 12,
        SelectSkill = 13,
        RefreshSkills = 14,
        ChallengeAlternative = 15,
        OpenChallengeBox = 16,
        PlayRound = 17,
        Abandon = 18,
        ReturnToCamp = 19,
        SettleDaily = 20,
        ClaimActivity = 21,
        ClaimTowerChest = 22,
        EquipPet = 23,
        EquipRelic = 24,
        BuyRelic = 25,
        ChangeEquipment = 26,
        Forge = 27,
        TrainTalent = 28,
        OpenShopChest = 29
    }

    public readonly struct AdventureCommand
    {
        public readonly JourneyAction Action;
        public readonly int Id, Option, Currency, Cost, Draws;

        public AdventureCommand(JourneyAction action, int id = 0, int option = 0,
            int currency = 0, int cost = 0, int draws = 0)
        {
            Action = action;
            Id = id;
            Option = option;
            Currency = currency;
            Cost = cost;
            Draws = draws;
        }
    }

    public sealed class AdventureResult
    {
        public RoundPlayback round;
        public Reward[] rewards;
        public int wheelIndex = -1;
    }

    public sealed class RoundPlayback
    {
        public int number;
        public BattleRoundOutcome outcome;
        public CombatPlayback[] actions;

        public RoundPlayback() { }

        internal RoundPlayback(BattleRound report)
        {
            number = report.Number;
            outcome = report.Outcome;
            actions = report.Actions.Select(action => new CombatPlayback(action)).ToArray();
        }
    }

    public sealed class CombatPlayback
    {
        public CombatActionKind kind;
        public int source, target, amount, targetHp, absorbed, skillId;
        public bool critical;
        public CombatEffect effect;

        public CombatPlayback() { }

        internal CombatPlayback(CombatAction action)
        {
            kind = action.Kind;
            source = action.Source;
            target = action.Target;
            amount = action.Amount;
            targetHp = action.TargetHp;
            absorbed = action.Absorbed;
            skillId = action.SkillId;
            critical = action.Critical;
            effect = action.Effect;
        }

        internal CombatAction ToAction() =>
            new CombatAction(kind, source, target, amount, targetHp, absorbed, critical, effect, skillId);
    }

    public sealed partial class Adventure
    {
        public AdventureResult Execute(AdventureCommand command, int seed)
        {
            int previousChangeCount = ChangeCount;
            var result = new AdventureResult();
            switch (command.Action)
            {
                case JourneyAction.Start: Start(command.Id, seed); break;
                case JourneyAction.ReplayOpening:
                    if (command.Id == data.recordedOpening?.chapter) Start(command.Id, 48277, true);
                    break;
                case JourneyAction.SelectCampChapter: SelectCampChapter(command.Id); break;
                case JourneyAction.StartChallenge: StartChallenge(command.Option, command.Id, seed); break;
                case JourneyAction.Sweep: Sweep(command.Id, seed); break;
                case JourneyAction.Rest: RestAtCamp(); break;
                case JourneyAction.Advance:
                    if (Phase != Phase.Trail) break;
                    Advance();
                    if (Phase == Phase.Event && !HasWheel && !HasJourneyChoice && !IsBattlePrompt && Run.eventKind != 8)
                        ResolveEvent(0);
                    break;
                case JourneyAction.ResolveEvent:
                    if ((uint)command.Option <= 1 && !HasWheel) ResolveEvent(command.Option);
                    break;
                case JourneyAction.OpenWheel: OpenWheel(); break;
                case JourneyAction.SkipWheel: SkipWheel(); break;
                case JourneyAction.SpinWheel: result.wheelIndex = BeginWheelSpin(); break;
                case JourneyAction.ResolveWheel:
                    if (HasWheel && Run.pendingWheelRewardId != 0) ResolveWheel();
                    break;
                case JourneyAction.SelectSkill: SelectSkill(command.Id); break;
                case JourneyAction.RefreshSkills: RefreshSkillChoices(); break;
                case JourneyAction.ChallengeAlternative: ChooseChallengeAlternative(); break;
                case JourneyAction.OpenChallengeBox: OpenChallengeBox(); break;
                case JourneyAction.PlayRound:
                    if (PlayRound() is BattleRound round) result.round = new RoundPlayback(round);
                    break;
                case JourneyAction.Abandon: Abandon(); break;
                case JourneyAction.ReturnToCamp: ReturnToCamp(); break;
                case JourneyAction.SettleDaily: SettleDaily(); break;
                case JourneyAction.ClaimActivity: ClaimActivity(command.Id); break;
                case JourneyAction.ClaimTowerChest: ClaimTowerChest(command.Id); break;
                case JourneyAction.EquipPet: EquipPet(command.Id); break;
                case JourneyAction.EquipRelic: EquipRelic(command.Option, command.Id); break;
                case JourneyAction.BuyRelic: BuyRelic(command.Option, command.Id); break;
                case JourneyAction.ChangeEquipment: ChangeEquipment(command.Option, command.Id); break;
                case JourneyAction.Forge: Forge(); break;
                case JourneyAction.TrainTalent: TrainTalentAttribute(command.Id); break;
                case JourneyAction.OpenShopChest:
                    var chest = CampStock?.chests?.FirstOrDefault(item => item.id == command.Id);
                    if (chest != null)
                        result.rewards = OpenEquipmentChest(chest, seed, (command.Currency, command.Cost, command.Draws));
                    break;
            }

            // A resumed wheel already has a saved target and can replay its presentation.
            return ChangeCount != previousChangeCount || result.wheelIndex >= 0 ? result : null;
        }
    }
}
