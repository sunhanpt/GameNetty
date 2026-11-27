using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private enum ChapterPickerDetail { None, Milestones, Sweep }

        private int previewChapterId;
        private ChapterPickerDetail chapterPickerDetail;

        private void CloseCampPicker()
        {
            campPicker = false;
            chapterPickerDetail = ChapterPickerDetail.None;
            Refresh();
        }

        private void DrawChapterPicker()
        {
            var chapters = game.Available.OrderByDescending(chapter => chapter.id).ToArray();
            var selected = chapters.FirstOrDefault(chapter => chapter.id == previewChapterId) ?? game.CampChapter;
            var panel = CampCollectionDialog("冒险章节", "ChapterPicker", CloseCampPicker);
            var details = MountPart(panel, "ChapterPicker/DrawChapterPicker/details");
            int best = game.ChapterBestStage(selected);
            MountText(details, "ChapterPicker/DrawChapterPicker/JournalCaption", value: $"{selected.id:00}  {selected.name}", selectedFont: JournalFont());
            MountText(details, "ChapterPicker/DrawChapterPicker/JournalCaption 2", value: $"最高进度  {best}/{selected.totalStage} 天", selectedFont: JournalFont());
            var track = MountPart(details, "ChapterPicker/DrawChapterPicker/track");
            if (best > 0)
                RoundedJournalPanel(track, MountAccent, (0, 0, Mathf.Clamp01(best / (float)selected.totalStage), 1));
            MountText(details, "ChapterPicker/DrawChapterPicker/JournalCaption 3", value: $"每场最多 {selected.maxRound} 回合", selectedFont: JournalFont());
            var rewards = MountButton(details, "ChapterPicker/DrawChapterPicker/里程碑奖励", action: () => ShowChapterPickerDetail(ChapterPickerDetail.Milestones), name: "Open chapter milestones");
            rewards.interactable = selected.rewardStage.Length > 0;

            MountText(panel, "ChapterPicker/DrawChapterPicker/JournalCaption 4", value: $"体力  {game.Profile.stamina}/30", selectedFont: JournalFont());
            var rest = MountButton(panel, "ChapterPicker/DrawChapterPicker/休整", action: () => Change(JourneyAction.Rest, preserveScroll: true), name: "Rest from chapter picker");
            rest.interactable = game.Profile.stamina < 30;
            MountButton(panel, "ChapterPicker/DrawChapterPicker/挑战", action: () =>
            {
                campChallenges = true;
                chapterPickerDetail = ChapterPickerDetail.None;
                Refresh();
            }, name: "Challenges from chapter picker");

            var list = MountList(panel, "ChapterPicker/DrawChapterPicker/list");
            foreach (var chapter in chapters)
            {
                var row = PageRow(list, 112);
                var tile = MountPanel(row, "ChapterPicker/DrawChapterPicker/tile", color: chapter.id == selected.id ? Tint(0x3F626F) : CampDialogCard);
                tile.name = "Preview chapter " + chapter.id;
                BindButton(tile.GetComponent<Image>(), () =>
                {
                    previewChapterId = chapter.id;
                    Refresh(true);
                });
                MountText(tile, "ChapterPicker/DrawChapterPicker/JournalCaption 5", value: $"{chapter.id:00}  {chapter.name}", selectedFont: JournalFont());
                string status = game.Profile.cleared.Contains(chapter.id) ? "已通关" : $"{chapter.totalStage} 天";
                MountText(tile, "ChapterPicker/DrawChapterPicker/JournalCaption 6", value: status, selectedFont: JournalFont());
            }

            bool current = game.CampChapter.id == selected.id;
            var setActive = MountButton(panel, "ChapterPicker/DrawChapterPicker/setActive",
                action: () => Change(JourneyAction.SelectCampChapter, selected.id, onSuccess: _ => CloseCampPicker()),
                label: current ? "当前营地章节" : "设为营地章节", name: "Set camp chapter");
            setActive.interactable = !current;
            if (selected.id == 1)
            {
                var replay = MountButton(panel, "ChapterPicker/DrawChapterPicker/开场演练", action: () => Change(JourneyAction.ReplayOpening, selected.id), name: "Recorded opening");
                replay.interactable = game.CanStartChapter(selected);
            }

            bool cleared = game.Profile.cleared.Contains(selected.id) && selected.journeyStage > 0;
            var startLayout = InstantiateUi("Widgets/ChapterStart", panel);
            var start = UiChild(startLayout, cleared ? "Compact" : "Wide").GetComponent<Button>();
            start.gameObject.SetActive(true);
            start.name = "Start selected chapter";
            start.onClick.AddListener(() => Change(JourneyAction.Start, selected.id));
            BindCampDialogText(start.transform, "Label", $"出发 · {selected.stamina} 体力");
            start.interactable = game.CanStartChapter(selected);
            if (cleared)
            {
                var sweep = MountButton(panel, "ChapterPicker/DrawChapterPicker/sweep", action: () => ShowChapterPickerDetail(ChapterPickerDetail.Sweep), label: $"扫荡 · {selected.sweepCost} 体力", name: "Preview chapter sweep");
                sweep.interactable = game.CanSweep(selected);
            }

            if (chapterPickerDetail == ChapterPickerDetail.Milestones) DrawChapterMilestones(selected);
            if (chapterPickerDetail == ChapterPickerDetail.Sweep) DrawChapterSweep(selected);
        }

        private void ShowChapterPickerDetail(ChapterPickerDetail detail)
        {
            chapterPickerDetail = detail;
            Refresh(true);
        }

        private void DrawChapterMilestones(Chapter chapter)
        {
            var panel = CampCollectionDialog("里程碑奖励", "ChapterMilestones",
                () => ShowChapterPickerDetail(ChapterPickerDetail.None));
            var list = MountPart(panel, "ChapterPicker/DrawChapterMilestones/list").GetComponent<ScrollRect>().content;
            foreach (int stage in chapter.rewardStage)
            {
                var items = game.ChapterRewards(chapter, stage);
                var row = PageRow(list, 183);
                var card = MountPart(row, "ChapterPicker/DrawChapterMilestones/card");
                MountText(card, "ChapterPicker/DrawChapterMilestones/JournalCaption", value: $"第 {stage} 天", selectedFont: JournalFont());
                string summary = string.Join("   ", items.Select(item => $"{game.ItemName(item.id)} ×{item.count}"));
                MountText(card, "ChapterPicker/DrawChapterMilestones/JournalCaption 2", value: summary, selectedFont: JournalFont());
            }
        }

        private void DrawChapterSweep(Chapter chapter)
        {
            var panel = CampCollectionDialog("确认扫荡", "ChapterSweep",
                () => ShowChapterPickerDetail(ChapterPickerDetail.None));
            MountText(panel, "ChapterPicker/DrawChapterSweep/JournalCaption", value: $"{chapter.id:00}  {chapter.name}", selectedFont: JournalFont());
            MountText(panel, "ChapterPicker/DrawChapterSweep/JournalCaption 2", value: $"消耗 {chapter.sweepCost} 体力，结算 {chapter.journeyStage} 天的扫荡金币。", selectedFont: JournalFont());
            var confirm = MountButton(panel, "ChapterPicker/DrawChapterSweep/确认扫荡", action: () => Change(JourneyAction.Sweep, chapter.id), name: "Confirm chapter sweep");
            confirm.interactable = game.CanSweep(chapter);
        }
    }
}
