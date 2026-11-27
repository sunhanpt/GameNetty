using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private static readonly Color StatisticsPaper=Tint(0xFAF1DD);
        private static readonly Color StatisticsMuted=Tint(0xA4BDBB);
        private static readonly Color StatisticsDamage=Tint(0xFFB65F);
        private static readonly Color StatisticsRecovery=Tint(0x7DE0BC);
        private List<BattleStatistic> displayedBattleStatistics;
        private RectTransform damageStatisticsPopup;
        private Run damageStatisticsRun;

        private void AddDamageStatisticsEntry(RectTransform parent,bool result)
        {
            var button=InstantiateUi(result?"Widgets/DamageResultEntry":"Widgets/DamageJourneyEntry",parent);
            button.GetComponent<Button>().onClick.AddListener(DrawDamageStatistics);
        }

        private void DrawDamageStatistics()
        {
            if(damageStatisticsPopup!=null || game.Run==null) return;
            var shade=LoadView("DamageStatistics",portrait);
            damageStatisticsPopup=shade;
            damageStatisticsRun=game.Run;
            var backdrop=UiChild(shade,"Backdrop");
            BindButton(backdrop.GetComponent<Image>(),CloseDamageStatistics);

            var content=UiChild(shade,"Content");
            content.GetComponent<Image>().raycastTarget=true;
            MountText(content, "DamageStatistics/DrawDamageStatistics/战斗统计", selectedFont: JournalFont());
            var close=MountPart(content, "DamageStatistics/DrawDamageStatistics/close");
            close.name="Close battle statistics";
            MountText(close, "DamageStatistics/DrawDamageStatistics/×", selectedFont: JournalFont());
            BindButton(close.GetComponent<Image>(),CloseDamageStatistics);

            var tabs=MountPart(content, "DamageStatistics/DrawDamageStatistics/tabs");
            var damageTab=MountPart(tabs, "DamageStatistics/DrawDamageStatistics/damageTab");
            var recoveryTab=MountPart(tabs, "DamageStatistics/DrawDamageStatistics/recoveryTab");
            damageTab.name="Damage tab";
            recoveryTab.name="Recovery tab";
            var damageLabel=MountText(damageTab, "DamageStatistics/DrawDamageStatistics/伤害", selectedFont: JournalFont());
            var recoveryLabel=MountText(recoveryTab, "DamageStatistics/DrawDamageStatistics/治疗", selectedFont: JournalFont());

            var summary=MountPart(content, "DamageStatistics/DrawDamageStatistics/summary");
            var summaryLabel=MountText(summary, "DamageStatistics/DrawDamageStatistics/总伤害", selectedFont: JournalFont());
            var total=MountText(summary, "DamageStatistics/DrawDamageStatistics/JournalCaption", selectedFont: JournalFont());
            MountText(content, "DamageStatistics/DrawDamageStatistics/技能", selectedFont: JournalFont());
            MountText(content, "DamageStatistics/DrawDamageStatistics/数值", selectedFont: JournalFont());
            MountText(content, "DamageStatistics/DrawDamageStatistics/100% 为该页最高值", selectedFont: JournalFont());

            var scroll=MountPart(content, "DamageStatistics/DrawDamageStatistics/scroll").GetComponent<ScrollRect>();
            scroll.name="Battle statistics rows";
            scroll.content.GetComponent<VerticalLayoutGroup>().spacing=5;
            bool recovery=false;
            BattleStatistic[] last=null;

            void UpdateRows(bool resetScroll)
            {
                var stats=displayedBattleStatistics??damageStatisticsRun.battleStatistics;
                var rows=stats.Where(row=>row.value>0 && row.IsRecovery==recovery).OrderByDescending(row=>row.value).ToArray();
                if(!resetScroll && last!=null && last.SequenceEqual(rows)) return;
                last=rows;
                total.text=FormatStatistic(rows.Sum(row=>(decimal)row.value));
                float position=resetScroll?1:scroll.verticalNormalizedPosition;
                scroll.StopMovement();
                foreach(Transform row in scroll.content) CloseSkillLayer((RectTransform)row);
                if(rows.Length==0)
                {
                    var empty=MountPart(scroll.content, "DamageStatistics/DrawDamageStatistics/Empty");
                    empty.GetComponent<LayoutElement>().preferredHeight=scroll.viewport.rect.height;
                    MountText(empty, "DamageStatistics/DrawDamageStatistics/JournalCaption 2", value: recovery?"暂无治疗记录":"暂无伤害记录", selectedFont: JournalFont());
                }
                else foreach(var row in rows) DrawDamageStatisticRow(scroll.content,row,rows[0].value);
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.verticalNormalizedPosition=position;
            }

            void SelectTab(bool showRecovery)
            {
                recovery=showRecovery;
                damageTab.GetComponent<Image>().color=recovery?Color.clear:StatisticsDamage;
                recoveryTab.GetComponent<Image>().color=recovery?StatisticsRecovery:Color.clear;
                damageLabel.color=recovery?StatisticsMuted:Ink;
                recoveryLabel.color=recovery?Ink:StatisticsMuted;
                summaryLabel.text=recovery?"总治疗":"总伤害";
                total.color=recovery?StatisticsRecovery:StatisticsDamage;
                UpdateRows(true);
            }

            BindButton(damageTab.GetComponent<Image>(),()=>SelectTab(false));
            BindButton(recoveryTab.GetComponent<Image>(),()=>SelectTab(true));
            SelectTab(false);
            StartCoroutine(AnimateDamageStatistics(content));
            StartCoroutine(RefreshDamageStatistics(shade,()=>UpdateRows(false)));
        }

        private void CloseDamageStatistics()
        {
            if(damageStatisticsPopup!=null) CloseSkillLayer(damageStatisticsPopup);
            damageStatisticsPopup=null;
            damageStatisticsRun=null;
        }

        private void UpdateDamageStatisticsLayer()
        {
            if(damageStatisticsPopup==null) return;
            if(game?.Run!=damageStatisticsRun || game.Phase==Phase.Skill) CloseDamageStatistics();
            else damageStatisticsPopup.SetAsLastSibling();
        }

        private static IEnumerator AnimateDamageStatistics(RectTransform content)
        {
            var group=content.GetComponent<CanvasGroup>();
            const float duration=.20f;
            for(float elapsed=0; elapsed<duration && content!=null; elapsed+=Time.unscaledDeltaTime)
            {
                float progress=Mathf.SmoothStep(0,1,elapsed/duration);
                group.alpha=progress;
                content.anchoredPosition=Vector2.down*(1-progress)*16;
                yield return null;
            }
            if(content==null) yield break;
            group.alpha=1;
            content.anchoredPosition=Vector2.zero;
        }

        private IEnumerator RefreshDamageStatistics(RectTransform popup,System.Action refresh)
        {
            while(popup!=null && popup.gameObject.activeInHierarchy)
            {
                yield return new WaitForSecondsRealtime(.5f);
                if(popup!=null && popup.gameObject.activeInHierarchy && game?.Run==damageStatisticsRun) refresh();
            }
        }

        private void DrawDamageStatisticRow(RectTransform list,BattleStatistic statistic,long maximum)
        {
            var row=MountPart(list, "DamageStatistics/DrawDamageStatisticRow/row");
            row.name="Statistic";
            var label=MountText(row, "DamageStatistics/DrawDamageStatisticRow/label", value: StatisticsName(statistic), selectedFont: JournalFont());
            label.resizeTextForBestFit=true;
            label.resizeTextMinSize=13;
            MountText(row, "DamageStatistics/DrawDamageStatisticRow/JournalCaption", value: FormatStatistic(statistic.value), selectedFont: JournalFont());
            var track=MountPart(row, "DamageStatistics/DrawDamageStatisticRow/track");
            float fraction=(float)((decimal)statistic.value/maximum);
            Color accent=statistic.IsRecovery?StatisticsRecovery:StatisticsDamage;
            RoundedJournalPanel(track,accent,(0,0,fraction,1));
            MountText(row, "DamageStatistics/DrawDamageStatisticRow/JournalCaption 2", value: statistic.RelativePercent(maximum)+"%", color: accent, selectedFont: JournalFont());
        }

        private string StatisticsName(BattleStatistic row) => game.BattleSkillName(row.skillId) ?? row.kind switch
        {
            CombatActionKind.Attack=>"普通攻击",CombatActionKind.Counter=>"反击",
            CombatActionKind.PetStrike=>"宠物",CombatActionKind.ArtifactStrike=>"神器",
            CombatActionKind.Poison=>"中毒",CombatActionKind.Burn=>"灼烧",CombatActionKind.Bleed=>"流血",
            CombatActionKind.Heal=>"恢复",CombatActionKind.SkillStrike when row.effect==CombatEffect.Lightning=>"雷电",
            _=>"技能"
        };

        private static string FormatStatistic(decimal value)
        {
            if(value<1000) return value.ToString("0",CultureInfo.InvariantCulture);
            if(value<1000000) return (value/1000m).ToString("0.#",CultureInfo.InvariantCulture)+"k";
            if(value<1000000000) return (value/1000000m).ToString("0.#",CultureInfo.InvariantCulture)+"m";
            return (value/1000000000m).ToString("0.#",CultureInfo.InvariantCulture)+"b";
        }

    }
}
