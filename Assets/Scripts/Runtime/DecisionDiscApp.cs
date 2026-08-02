using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DecisionDisc
{
    public sealed class DecisionDiscApp : MonoBehaviour
    {
        private static readonly Color Background = Hex("121826");
        private static readonly Color Panel = Hex("1D2939");
        private static readonly Color Accent = Hex("35D0BA");
        private static readonly Color Yes = Hex("22C55E");
        private static readonly Color No = Hex("F43F5E");
        private static Font font;

        private DecisionStore store;
        private AndroidFileBridge files;
        private readonly List<GameObject> pages = new List<GameObject>();
        private InputField questionInput;
        private InputField noteInput;
        private Image disc;
        private Text discText;
        private Text status;
        private Text modeText;
        private Button saveButton;
        private Transform badgeList;
        private Transform historyList;
        private Text badgeStatus;
        private Text logPreview;
        private Text importPreview;
        private GameObject importPanel;
        private HistoryExport pendingImport;
        private BadgeDefinition imageTarget;
        private bool imageTargetIsYes;
        private PendingDecision pending;
        private DecisionMode mode = DecisionMode.Fair5050;
        private Sprite circleSprite;
        private readonly List<PendingDecision> sessionDecisions = new List<PendingDecision>();
        private readonly HashSet<PendingDecision> savedSessionDecisions = new HashSet<PendingDecision>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindObjectOfType<DecisionDiscApp>() == null)
                new GameObject("DecisionDiscApp").AddComponent<DecisionDiscApp>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += CaptureUnityError;
            Screen.orientation = ScreenOrientation.Portrait;
            UnityEngine.Input.multiTouchEnabled = false;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Noto Sans SC", "Droid Sans Fallback", "Arial Unicode MS" }, 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            circleSprite = CreateCircleSprite();
            store = new DecisionStore();
            files = gameObject.AddComponent<AndroidFileBridge>();
            files.TextImported += PreviewImport;
            files.ImageImported += ApplyPickedImage;
            files.Error += OnFilePickerError;
            UserActionLog.Add("应用启动；本地已保存记录数=" + store.History.records.Count + "；徽章数=" + store.Badges.badges.Count);
            BuildUi();
            ShowPage(0);
        }

        private void OnDestroy() { Application.logMessageReceived -= CaptureUnityError; }

        private static void CaptureUnityError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                UserActionLog.Add("运行错误 [" + type + "]：" + condition + "\n" + stackTrace);
        }

        private void BuildUi()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(events);
            }

            var canvasObject = new GameObject("Decision Disc UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            Image background = Image("Background", canvasObject.transform, Background); Stretch(background.rectTransform);

            var safe = Rect("Safe Area", background.transform); Stretch(safe); safe.gameObject.AddComponent<SafeAreaFitter>();
            var pageHost = Rect("Pages", safe); Stretch(pageHost, 0, 94, 0, 0);
            pages.Add(BuildHome(pageHost));
            pages.Add(BuildBadges(pageHost));
            pages.Add(BuildHistory(pageHost));
            pages.Add(BuildSettings(pageHost));
            BuildNavigation(safe);
            BuildImportPanel(safe);
        }

        private GameObject BuildHome(Transform parent)
        {
            var page = Page("ThrowPage", parent);
            Text title = Label("YES / NO 决策徽章", page.transform, 54, TextAnchor.MiddleCenter, Color.white);
            SetHeight(title.rectTransform, 90);
            Text sub = Label("输入问题 · 按住蓄力 · 松开投掷", page.transform, 28, TextAnchor.MiddleCenter, Hex("98A2B3")); SetHeight(sub.rectTransform, 50);

            var discWrap = Rect("DiscWrap", page.transform); SetHeight(discWrap, 480);
            disc = Image("Disc", discWrap, Accent); disc.sprite = circleSprite;
            disc.type = UnityEngine.UI.Image.Type.Simple; disc.preserveAspect = true;
            disc.rectTransform.anchorMin = disc.rectTransform.anchorMax = new Vector2(.5f, .5f);
            disc.rectTransform.sizeDelta = new Vector2(380, 380);
            discText = Label("YES / NO", disc.transform, 58, TextAnchor.MiddleCenter, Background); Stretch(discText.rectTransform);

            questionInput = Input("请输入本次要决定的问题", page.transform, 128, false);
            var modeButton = Button("Mode", page.transform, "公平 50 / 50", ToggleMode, Panel, 80);
            modeText = modeButton.GetComponentInChildren<Text>();

            var chargeObject = new GameObject("Charge", typeof(RectTransform), typeof(Image), typeof(ChargeThrowButton));
            chargeObject.transform.SetParent(page.transform, false); SetHeight((RectTransform)chargeObject.transform, 160);
            chargeObject.GetComponent<Image>().color = Hex("243B53");
            var fill = Image("Fill", chargeObject.transform, Accent); Stretch(fill.rectTransform); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0;
            var chargeLabel = Label("按住蓄力，松开投掷", chargeObject.transform, 40, TextAnchor.MiddleCenter, Color.white); Stretch(chargeLabel.rectTransform);
            var charge = chargeObject.GetComponent<ChargeThrowButton>(); charge.Label = chargeLabel; charge.Fill = fill; charge.Released += Throw;

            status = Label("尚未投掷；结果默认不会保存", page.transform, 30, TextAnchor.MiddleCenter, Hex("D0D5DD")); SetHeight(status.rectTransform, 70);
            noteInput = Input("可选备注（只在保存记录时写入）", page.transform, 100, false);
            saveButton = Button("Save", page.transform, "保存本次记录", SaveCurrent, Accent, 88);
            saveButton.interactable = false;
            return page;
        }

        private GameObject BuildBadges(Transform parent)
        {
            var page = Page("BadgesPage", parent);
            Header(page.transform, "徽章管理", "每个自定义徽章必须分别上传 YES 面和 NO 面");
            Button("AddBadge", page.transform, "+  创建新徽章", CreateBadge, Accent, 88);
            badgeStatus = Label("新建后请依次上传两面图片，图片会复制到应用内部。", page.transform, 24, TextAnchor.MiddleCenter, Hex("98A2B3")); SetHeight(badgeStatus.rectTransform, 62);
            badgeList = ScrollContent("BadgeScroll", page.transform, 0);
            RefreshBadges();
            return page;
        }

        private GameObject BuildHistory(Transform parent)
        {
            var page = Page("HistoryPage", parent);
            Header(page.transform, "历史记录", "本次投掷仅在内存；明确保存后才会永久保留");
            var actions = Horizontal("Actions", page.transform, 92);
            Button("Export", actions, "导出已保存 JSON", () => { UserActionLog.Add("点击导出历史 JSON"); files.ExportJson(store.CreateExportJson()); }, Panel, 82);
            Button("Import", actions, "导入历史 JSON", files.PickJson, Panel, 82);
            historyList = ScrollContent("HistoryScroll", page.transform, 0);
            RefreshHistory();
            return page;
        }

        private GameObject BuildSettings(Transform parent)
        {
            var page = Page("SettingsPage", parent);
            Header(page.transform, "设置", "隐私说明、随机模式与问题排查");
            Transform content = ScrollContent("SettingsScroll", page.transform, 0);
            CardText(content, "隐私\n当前问题、未保存结果和操作日志默认只在内存中。只有明确保存或导出才会写入文件。");
            CardText(content, "随机模式\n公平模式始终为 50/50；力度影响模式会把 YES 概率映射到 25%–75%。");
            CardText(content, "本地存储\n历史记录和徽章图片副本保存在 Application.persistentDataPath。");
            Button("RefreshLog", content, "刷新操作日志预览", RefreshLogPreview, Panel, 76);
            logPreview = Label("暂无操作日志。", content, 21, TextAnchor.UpperLeft, Color.white); SetHeight(logPreview.rectTransform, 300);
            Button("ExportLog", content, "导出操作日志", ExportOperationLog, Accent, 82);
            CardText(content, "版本\nYesNoFilp 1.1 · 历史 JSON 格式 v1");
            return page;
        }

        private void BuildNavigation(Transform safe)
        {
            var nav = Horizontal("Navigation", safe, 94);
            var rt = (RectTransform)nav; rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, 94);
            string[] names = { "投掷", "徽章", "记录", "设置" };
            for (int i = 0; i < names.Length; i++) { int index = i; Button("Nav" + i, nav, names[i], () => ShowPage(index), Panel, 94); }
        }

        private void BuildImportPanel(Transform parent)
        {
            importPanel = Image("ImportPreviewPanel", parent, new Color(0.05f, .07f, .12f, .97f)).gameObject; Stretch((RectTransform)importPanel.transform, 70, 220, 70, 220);
            var layout = importPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(32, 32, 32, 32); layout.spacing = 24;
            Label("导入预览", importPanel.transform, 42, TextAnchor.MiddleCenter, Color.white);
            importPreview = Label("", importPanel.transform, 28, TextAnchor.UpperLeft, Color.white); SetFlexible(importPreview.rectTransform);
            Button("Merge", importPanel.transform, "与已保存记录合并", () => ApplyImport(false), Accent, 86);
            Button("Replace", importPanel.transform, "替换全部已保存记录", () => ApplyImport(true), No, 86);
            Button("Cancel", importPanel.transform, "取消", () => importPanel.SetActive(false), Panel, 76);
            importPanel.SetActive(false);
        }

        private void Throw(float strength, string source)
        {
            string question = questionInput.text.Trim();
            if (string.IsNullOrEmpty(question)) { status.text = "请先输入要决定的问题。"; UserActionLog.Add("投掷被阻止：问题为空"); return; }
            bool yes = DecisionEngine.Decide(strength, mode);
            pending = new PendingDecision { Question = question, IsYes = yes, Strength = strength, StrengthSource = source, Mode = mode, TimestampUtc = DateTime.UtcNow, BadgeId = store.SelectedBadge().id };
            sessionDecisions.Insert(0, pending);
            UserActionLog.Add("开始投掷；问题=" + question + "；力度=" + Mathf.RoundToInt(strength * 100) + "%；来源=" + StrengthSourceLabel(source) + "；模式=" + ModeLabel(mode));
            saveButton.interactable = false;
            StopAllCoroutines(); StartCoroutine(AnimateThrow(pending));
        }

        private IEnumerator AnimateThrow(PendingDecision value)
        {
            float duration = 1.45f + value.Strength * .8f;
            Vector2 start = disc.rectTransform.anchoredPosition;
            BadgeDefinition animationBadge = store.Badges.badges.Find(item => item.id == value.BadgeId) ?? store.SelectedBadge();
            int lastFace = -1;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = t / duration;
                int face = Mathf.FloorToInt(p * (12 + value.Strength * 8)) % 2;
                if (face != lastFace) { RenderDiscFace(face == 0, animationBadge); lastFace = face; }
                float height = Mathf.Sin(p * Mathf.PI) * (260 + 260 * value.Strength);
                disc.rectTransform.anchoredPosition = start + Vector2.up * height;
                disc.rectTransform.localEulerAngles = new Vector3(0, p * (720 + 1080 * value.Strength), p * 80);
                disc.rectTransform.localScale = new Vector3(Mathf.Max(.08f, Mathf.Abs(Mathf.Cos(p * Mathf.PI * (5 + value.Strength * 5)))), 1, 1);
                yield return null;
            }
            disc.rectTransform.anchoredPosition = start;
            disc.rectTransform.localEulerAngles = Vector3.zero; disc.rectTransform.localScale = Vector3.one;
            RenderDiscFace(value.IsYes, animationBadge);
            status.text = (value.IsYes ? "YES" : "NO") + "  ·  力度 " + Mathf.RoundToInt(value.Strength * 100) + "%  ·  " + StrengthSourceLabel(value.StrengthSource) + "\n尚未永久保存，可在记录页查看本次内存记录";
            UserActionLog.Add("投掷完成；结果=" + (value.IsYes ? "YES" : "NO"));
            saveButton.interactable = true;
            RefreshHistory();
        }

        private void SaveCurrent()
        {
            if (pending == null) return;
            store.SaveExplicit(pending, noteInput.text.Trim());
            savedSessionDecisions.Add(pending);
            UserActionLog.Add("明确保存本次记录；结果=" + (pending.IsYes ? "YES" : "NO"));
            pending = null; saveButton.interactable = false; noteInput.text = string.Empty;
            status.text = "本次记录已永久保存。"; RefreshHistory();
        }

        private void ToggleMode()
        {
            mode = mode == DecisionMode.Fair5050 ? DecisionMode.StrengthInfluences : DecisionMode.Fair5050;
            modeText.text = ModeLabel(mode);
            UserActionLog.Add("切换随机模式：" + ModeLabel(mode));
        }

        private void CreateBadge()
        {
            BadgeDefinition badge = store.CreateBadge("新徽章 " + store.Badges.badges.Count);
            imageTarget = badge;
            badgeStatus.text = "已创建“" + badge.name + "”，请分别上传 YES 面和 NO 面，补齐后才能使用。";
            UserActionLog.Add("创建徽章：" + badge.name);
            RefreshBadges();
        }

        private void RefreshBadges()
        {
            if (badgeList == null) return; Clear(badgeList);
            var ordered = new List<BadgeDefinition>();
            if (imageTarget != null && store.Badges.badges.Contains(imageTarget)) ordered.Add(imageTarget);
            foreach (BadgeDefinition item in store.Badges.badges) if (!ordered.Contains(item)) ordered.Add(item);
            foreach (BadgeDefinition badgeItem in ordered)
            {
                BadgeDefinition badge = badgeItem;
                bool complete = DecisionStore.IsBadgeComplete(badge);
                var card = VerticalCard("Badge", badgeList, badge.builtIn ? 300 : 500);
                Text name = Label((badge.id == store.Badges.selectedBadgeId ? "●  当前使用：" : "○  ") + badge.name, card, 32, TextAnchor.MiddleLeft, Color.white); SetHeight(name.rectTransform, 54);
                if (!badge.builtIn)
                {
                    var renameRow = Horizontal("Rename", card, 70);
                    InputField nameInput = Input("徽章名称", renameRow, 68, false); nameInput.text = badge.name;
                    Button("Rename", renameRow, "保存名称", () => { store.RenameBadge(badge, nameInput.text); badgeStatus.text = "名称已修改为“" + badge.name + "”。"; UserActionLog.Add("修改徽章名称：" + badge.name); RefreshBadges(); }, Panel, 68);
                }
                var previews = Horizontal("FacePreviews", card, 150);
                AddFacePreview(previews, badge, true);
                AddFacePreview(previews, badge, false);
                var actions = Horizontal("BadgeActions", card, 72);
                Button use = Button("Use", actions, complete ? "使用此徽章" : "请先补齐两面", () => { store.SelectBadge(badge.id); RenderDiscFace(true, badge); badgeStatus.text = "当前使用：“ + badge.name + ”"; UserActionLog.Add("选择徽章：" + badge.name); RefreshBadges(); }, Accent, 70);
                use.interactable = complete;
                if (!badge.builtIn)
                {
                    Button("YesImage", actions, "上传 YES 面", () => PickBadgeImage(badge, true), Yes, 70);
                    Button("NoImage", actions, "上传 NO 面", () => PickBadgeImage(badge, false), No, 70);
                    Button("Delete", actions, "删除", () => { UserActionLog.Add("删除徽章：" + badge.name); store.DeleteBadge(badge.id); if (imageTarget == badge) imageTarget = null; RefreshBadges(); }, Panel, 70);
                }
                Text paths = Label(badge.builtIn ? "内置经典徽章" : "YES 面：" + Present(badge.yesImagePath) + "\nNO 面：" + Present(badge.noImagePath), card, 21, TextAnchor.UpperLeft, complete ? Accent : Hex("F79009")); SetHeight(paths.rectTransform, 70);
            }
        }

        private void PickBadgeImage(BadgeDefinition badge, bool yesFace) { imageTarget = badge; imageTargetIsYes = yesFace; badgeStatus.text = "正在为“" + badge.name + "”选择 " + (yesFace ? "YES" : "NO") + " 面图片…"; UserActionLog.Add("选择徽章图片：" + badge.name + " / " + (yesFace ? "YES" : "NO")); files.PickImage(); }

        private void ApplyPickedImage(string path)
        {
            try
            {
                if (imageTarget == null) throw new InvalidOperationException("没有正在编辑的徽章。");
                store.CopyBadgeImage(imageTarget, imageTargetIsYes, path);
                bool complete = DecisionStore.IsBadgeComplete(imageTarget);
                badgeStatus.text = "已保存“" + imageTarget.name + "”的 " + (imageTargetIsYes ? "YES" : "NO") + " 面。" + (complete ? " 两面已补齐，可以使用。" : " 还需要上传另一面。");
                UserActionLog.Add("徽章图片已复制到应用目录：" + imageTarget.name + " / " + (imageTargetIsYes ? "YES" : "NO"));
                RefreshBadges(); if (store.Badges.selectedBadgeId == imageTarget.id) RenderDiscFace(imageTargetIsYes, imageTarget);
            }
            catch (Exception exception) { badgeStatus.text = "图片保存失败：" + exception.Message; UserActionLog.Add("徽章图片保存失败：" + exception.Message); Debug.LogWarning(exception); }
        }

        private void RefreshHistory()
        {
            if (historyList == null) return; Clear(historyList);
            Text sessionTitle = Label("本次使用记录（仅内存） · " + sessionDecisions.Count + " 条", historyList, 30, TextAnchor.MiddleLeft, Accent); SetHeight(sessionTitle.rectTransform, 58);
            if (sessionDecisions.Count == 0) CardText(historyList, "本次打开应用后还没有投掷记录。完成一次投掷后会立即显示在这里。");
            foreach (PendingDecision session in sessionDecisions)
            {
                bool saved = savedSessionDecisions.Contains(session);
                var card = VerticalCard("SessionRecord", historyList, 190);
                Text headline = Label((session.IsYes ? "YES" : "NO") + "  ·  力度 " + Mathf.RoundToInt(session.Strength * 100) + "%  ·  " + (saved ? "已永久保存" : "尚未保存"), card, 28, TextAnchor.MiddleLeft, session.IsYes ? Yes : No); SetHeight(headline.rectTransform, 52);
                Text question = Label(session.Question + "\n" + session.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + " · " + ModeLabel(session.Mode), card, 24, TextAnchor.UpperLeft, Color.white); SetHeight(question.rectTransform, 100);
            }
            Text savedTitle = Label("已保存记录 · " + store.History.records.Count + " 条", historyList, 30, TextAnchor.MiddleLeft, Accent); SetHeight(savedTitle.rectTransform, 58);
            if (store.History.records.Count == 0) { CardText(historyList, "暂无永久记录。请在投掷完成后点击“保存本次记录”。"); return; }
            foreach (DecisionRecord recordItem in store.History.records)
            {
                DecisionRecord record = recordItem;
                var card = VerticalCard("Record", historyList, 230);
                Text headline = Label(record.result + "  ·  力度 " + Mathf.RoundToInt(record.strength * 100) + "%  ·  " + StoredModeLabel(record.mode), card, 30, TextAnchor.MiddleLeft, record.result == "YES" ? Yes : No); SetHeight(headline.rectTransform, 52);
                Text question = Label(record.question + (string.IsNullOrEmpty(record.note) ? "" : "\n备注：" + record.note) + "\n" + LocalTime(record.timestampUtc), card, 25, TextAnchor.UpperLeft, Color.white); SetHeight(question.rectTransform, 104);
                Button("Delete", card, "删除这条记录", () => { UserActionLog.Add("删除已保存记录：" + record.question); store.DeleteRecord(record.id); RefreshHistory(); }, Panel, 58);
            }
        }

        private void PreviewImport(string json)
        {
            try
            {
                pendingImport = store.ParseImport(json);
                int yesCount = pendingImport.records.FindAll(r => r.result == "YES").Count;
                importPreview.text = "格式版本：" + pendingImport.version + "\n记录数：" + pendingImport.records.Count + "\nYES：" + yesCount + "  ·  NO：" + (pendingImport.records.Count - yesCount) + "\n\n请选择合并或替换。在你确认前不会修改任何数据。";
                UserActionLog.Add("导入预览成功；记录数=" + pendingImport.records.Count);
                importPanel.SetActive(true);
            }
            catch (Exception exception) { importPreview.text = "导入被拒绝：\n" + exception.Message; UserActionLog.Add("导入失败：" + exception.Message); pendingImport = null; importPanel.SetActive(true); }
        }

        private void ApplyImport(bool replace)
        {
            if (pendingImport == null) { importPanel.SetActive(false); return; }
            int count = pendingImport.records.Count; store.ApplyImport(pendingImport, replace); UserActionLog.Add((replace ? "替换" : "合并") + "导入历史；记录数=" + count); pendingImport = null; importPanel.SetActive(false); RefreshHistory();
        }

        private void AddFacePreview(Transform parent, BadgeDefinition badge, bool yesFace)
        {
            string path = yesFace ? badge.yesImagePath : badge.noImagePath;
            Sprite sprite = LoadSprite(path);
            Image preview = Image(yesFace ? "YesPreview" : "NoPreview", parent, sprite == null ? (yesFace ? Yes : No) : Color.white);
            preview.sprite = sprite ?? circleSprite; preview.preserveAspect = true;
            Text face = Label(sprite == null ? (yesFace ? "YES\n未上传" : "NO\n未上传") : (yesFace ? "YES" : "NO"), preview.transform, 24, TextAnchor.MiddleCenter, sprite == null ? Color.white : new Color(1, 1, 1, .8f)); Stretch(face.rectTransform);
        }

        private void RefreshLogPreview()
        {
            UserActionLog.Add("刷新操作日志预览");
            if (logPreview != null) logPreview.text = UserActionLog.Preview();
        }

        private void ExportOperationLog()
        {
            UserActionLog.Add("点击导出操作日志");
            files.ExportText(UserActionLog.ExportText(), "YesNoFilp-operation-log.txt", "text/plain");
            RefreshLogPreview();
        }

        private void OnFilePickerError(string message)
        {
            if (badgeStatus != null) badgeStatus.text = message == "Cancelled" ? "已取消文件选择。" : "文件选择失败：" + message;
            RefreshLogPreview();
        }

        private static string ModeLabel(DecisionMode value) { return value == DecisionMode.Fair5050 ? "公平 50 / 50" : "力度影响概率"; }
        private static string StoredModeLabel(string value) { return value == DecisionMode.Fair5050.ToString() ? "公平 50 / 50" : "力度影响概率"; }
        private static string StrengthSourceLabel(string value)
        {
            if (value == "pressure") return "真实触摸压力";
            if (value == "hold+area+release") return "按住时间 + 触摸面积 + 松开速度";
            return "按住时间 + 松开速度";
        }

        private static string LocalTime(string utc)
        {
            return DateTime.TryParse(utc, out DateTime value) ? value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : utc;
        }

        private void RenderDiscFace(bool yesFace, BadgeDefinition badge)
        {
            string path = yesFace ? badge.yesImagePath : badge.noImagePath;
            Sprite loaded = LoadSprite(path);
            disc.sprite = loaded ?? circleSprite;
            disc.color = loaded == null ? (yesFace ? Yes : No) : Color.white;
            discText.text = loaded == null ? (yesFace ? "YES" : "NO") : string.Empty;
        }

        private void ShowPage(int index)
        {
            for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == index);
            string[] labels = { "投掷", "徽章", "记录", "设置" };
            UserActionLog.Add("切换页面：" + labels[Mathf.Clamp(index, 0, labels.Length - 1)]);
            if (index == 1) RefreshBadges(); if (index == 2) RefreshHistory();
            if (index == 3) RefreshLogPreview();
        }

        private static GameObject Page(string name, Transform parent)
        {
            var page = Rect(name, parent); Stretch((RectTransform)page.transform, 36, 24, 36, 24);
            var layout = page.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 18; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            return page.gameObject;
        }

        private static void Header(Transform parent, string title, string subtitle)
        {
            Text heading = Label(title, parent, 50, TextAnchor.MiddleCenter, Color.white); SetHeight(heading.rectTransform, 84);
            Text detail = Label(subtitle, parent, 25, TextAnchor.MiddleCenter, Hex("98A2B3")); SetHeight(detail.rectTransform, 54);
        }

        private static Transform ScrollContent(string name, Transform parent, float height)
        {
            var scrollObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect)); scrollObject.transform.SetParent(parent, false);
            scrollObject.GetComponent<Image>().color = Color.clear; scrollObject.GetComponent<Mask>().showMaskGraphic = false;
            if (height > 0) SetHeight((RectTransform)scrollObject.transform, height); else SetFlexible((RectTransform)scrollObject.transform);
            var viewport = (RectTransform)scrollObject.transform;
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 16; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollObject.GetComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        private static Transform VerticalCard(string name, Transform parent, float height)
        {
            var card = Image(name, parent, Panel); SetHeight(card.rectTransform, height);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(24, 24, 14, 14); layout.spacing = 8; layout.childForceExpandHeight = false;
            return card.transform;
        }

        private static void CardText(Transform parent, string value)
        {
            var card = VerticalCard("Info", parent, 170);
            Text text = Label(value, card, 27, TextAnchor.MiddleLeft, Color.white); SetFlexible(text.rectTransform);
        }

        private static InputField Input(string placeholder, Transform parent, float height, bool multiline)
        {
            var root = Image("Input", parent, Panel); SetHeight(root.rectTransform, height);
            var field = root.gameObject.AddComponent<InputField>(); field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            Text value = Label("", root.transform, 30, TextAnchor.MiddleLeft, Color.white); Stretch(value.rectTransform, 24, 10, 24, 10);
            Text hint = Label(placeholder, root.transform, 28, TextAnchor.MiddleLeft, Hex("667085")); Stretch(hint.rectTransform, 24, 10, 24, 10);
            field.textComponent = value; field.placeholder = hint; field.targetGraphic = root; return field;
        }

        private static Button Button(string name, Transform parent, string value, UnityEngine.Events.UnityAction action, Color color, float height)
        {
            var image = Image(name, parent, color); SetHeight(image.rectTransform, height);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Text label = Label(value, image.transform, 26, TextAnchor.MiddleCenter, Color.white); Stretch(label.rectTransform, 8, 4, 8, 4); return button;
        }

        private static Transform Horizontal(string name, Transform parent, float height)
        {
            var root = Rect(name, parent); SetHeight(root, height);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 12; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandWidth = true; return root;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform)); item.transform.SetParent(parent, false); return (RectTransform)item.transform;
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image)); item.transform.SetParent(parent, false); var image = item.GetComponent<Image>(); image.color = color; return image;
        }

        private static Text Label(string value, Transform parent, int size, TextAnchor alignment, Color color)
        {
            var item = new GameObject("Text", typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent, false); var label = item.GetComponent<Text>(); label.font = font; label.text = value; label.fontSize = size; label.alignment = alignment; label.color = color; label.resizeTextForBestFit = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }

        private static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static void SetHeight(RectTransform rect, float height) { var e = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.preferredHeight = height; e.minHeight = height; }
        private static void SetFlexible(RectTransform rect) { var e = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.flexibleHeight = 1; }
        private static void Clear(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject); }
        private static string Present(string path) { return string.IsNullOrEmpty(path) || !File.Exists(path) ? "未上传" : Path.GetFileName(path) + "（应用内部副本）"; }
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color result); return result; }

        private static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try { var texture = new Texture2D(2, 2); return texture.LoadImage(File.ReadAllBytes(path)) ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100) : null; }
            catch { return null; }
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 256; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false); Color[] pixels = new Color[size * size]; Vector2 center = Vector2.one * (size - 1) * .5f; float radius = size * .49f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { float distance = Vector2.Distance(new Vector2(x, y), center); float alpha = Mathf.Clamp01(radius - distance); pixels[y * size + x] = new Color(1, 1, 1, alpha); }
            texture.SetPixels(pixels); texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
        }
    }
}
