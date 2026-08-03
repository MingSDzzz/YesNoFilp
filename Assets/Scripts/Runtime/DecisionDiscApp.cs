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
        private sealed class HistoryViewItem
        {
            public PendingDecision session;
            public DecisionRecord record;
            public DateTime time;
            public string BadgeId { get { return session != null ? session.BadgeId : record.badgeId; } }
        }
        private static Color Background = Hex("F4F7FB");
        private static Color Panel = Hex("FFFFFF");
        private static Color Accent = Hex("16A394");
        private static Color Yes = Hex("16A34A");
        private static Color No = Hex("E11D48");
        private static Color PrimaryText = Hex("182230");
        private static Color SecondaryText = Hex("667085");
        private static Font font;

        private DecisionStore store;
        private AndroidFileBridge files;
        private readonly List<GameObject> pages = new List<GameObject>();
        private InputField questionInput;
        private Image disc;
        private Text discText;
        private Transform homeFaces;
        private Text status;
        private Text modeText;
        private Text selectedBadgeText;
        private Text seriesText;
        private Transform badgeList;
        private Transform historyList;
        private Text badgeStatus;
        private Text logPreview;
        private Text importPreview;
        private GameObject importPanel;
        private GameObject createBadgePanel;
        private InputField createBadgeNameInput;
        private GameObject badgeDetailPanel;
        private Text badgeDetailTitle;
        private InputField badgeDetailNameInput;
        private Slider badgeProbabilitySlider;
        private Text badgeProbabilityText;
        private Transform badgeDetailFaces;
        private Button badgeDetailDeleteButton;
        private GameObject savePromptPanel;
        private Text savePromptTitle;
        private InputField savePromptNote;
        private GameObject cropPanel;
        private Image cropPreview;
        private Slider cropZoom;
        private Slider cropX;
        private Slider cropY;
        private string cropSourcePath;
        private Text historyFilterText;
        private string historyFilterBadgeId = string.Empty;
        private GameObject uiRoot;
        private bool lightTheme = true;
        private int seriesLength = 1;
        private float pendingHoldSeconds;
        private BadgeDefinition detailBadge;
        private HistoryExport pendingImport;
        private BadgeDefinition imageTarget;
        private bool imageTargetIsYes;
        private PendingDecision pending;
        private DecisionMode mode = DecisionMode.Fair5050;
        private Sprite circleSprite;
        private readonly List<PendingDecision> sessionDecisions = new List<PendingDecision>();

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
            ApplyThemePalette();
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(events);
            }

            var canvasObject = new GameObject("Decision Disc UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasObject;
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
            BuildBadgeCreatePanel(safe);
            BuildBadgeDetailPanel(safe);
            BuildSavePromptPanel(safe);
            BuildCropPanel(safe);
        }

        private GameObject BuildHome(Transform parent)
        {
            var page = Page("ThrowPage", parent);
            Text title = Label("YES / NO 决策徽章", page.transform, 54, TextAnchor.MiddleCenter, PrimaryText);
            SetHeight(title.rectTransform, 90);
            Text sub = Label("问题可留空 · 按住越久飞得越高", page.transform, 28, TextAnchor.MiddleCenter, SecondaryText); SetHeight(sub.rectTransform, 50);

            homeFaces = Horizontal("HomeFaces", page.transform, 230);
            RefreshHomeFaces();

            var discWrap = Rect("DiscWrap", page.transform); SetHeight(discWrap, 360);
            disc = Image("Disc", discWrap, Accent); disc.sprite = circleSprite;
            disc.type = UnityEngine.UI.Image.Type.Simple; disc.preserveAspect = true;
            disc.rectTransform.anchorMin = disc.rectTransform.anchorMax = new Vector2(.5f, .5f);
            disc.rectTransform.sizeDelta = new Vector2(330, 330);
            discText = Label("YES / NO", disc.transform, 92, TextAnchor.MiddleCenter, Color.white); Stretch(discText.rectTransform);

            var badgeSwitch = Horizontal("BadgeSwitch", page.transform, 76);
            selectedBadgeText = Label("当前徽章：" + store.SelectedBadge().name, badgeSwitch, 27, TextAnchor.MiddleLeft, PrimaryText);
            Button("SwitchBadge", badgeSwitch, "切换徽章", () => ShowPage(1), Panel, 72);

            questionInput = Input("可选：输入本次要决定的问题", page.transform, 104, false);
            var modeButton = Button("Mode", page.transform, "公平 50 / 50", ToggleMode, Panel, 80);
            modeText = modeButton.GetComponentInChildren<Text>();
            var seriesButton = Button("Series", page.transform, "1 次决定", ToggleSeries, Panel, 76);
            seriesText = seriesButton.GetComponentInChildren<Text>();

            var chargeObject = new GameObject("Charge", typeof(RectTransform), typeof(Image), typeof(ChargeThrowButton));
            chargeObject.transform.SetParent(page.transform, false); SetHeight((RectTransform)chargeObject.transform, 140);
            chargeObject.GetComponent<Image>().color = lightTheme ? Hex("DDE7F2") : Hex("243B53");
            var fill = Image("Fill", chargeObject.transform, Accent); Stretch(fill.rectTransform); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0;
            var chargeLabel = Label("按住蓄力，松开投掷", chargeObject.transform, 40, TextAnchor.MiddleCenter, PrimaryText); Stretch(chargeLabel.rectTransform);
            var charge = chargeObject.GetComponent<ChargeThrowButton>(); charge.Label = chargeLabel; charge.Fill = fill; charge.Released += Throw;

            status = Label("尚未投掷；结果默认不会保存", page.transform, 30, TextAnchor.MiddleCenter, SecondaryText); SetHeight(status.rectTransform, 70);
            return page;
        }

        private GameObject BuildBadges(Transform parent)
        {
            var page = Page("BadgesPage", parent);
            Header(page.transform, "徽章管理", "新徽章默认使用 YES/NO 文字面，也可分别上传图片替换");
            Button("AddBadge", page.transform, "+  创建新徽章", CreateBadge, Accent, 88);
            badgeStatus = Label("默认徽章与新徽章都可直接使用；点击徽章图片即可替换。", page.transform, 24, TextAnchor.MiddleCenter, SecondaryText); SetHeight(badgeStatus.rectTransform, 62);
            badgeList = ScrollContent("BadgeScroll", page.transform, 0);
            RefreshBadges();
            return page;
        }

        private GameObject BuildHistory(Transform parent)
        {
            var page = Page("HistoryPage", parent);
            Header(page.transform, "历史记录", "按时间排列；未保存内容只保留在本次运行中");
            Button filter = Button("BadgeFilter", page.transform, "筛选徽章：全部", CycleHistoryFilter, Panel, 72);
            historyFilterText = filter.GetComponentInChildren<Text>();
            historyList = ScrollContent("HistoryScroll", page.transform, 0);
            RefreshHistory();
            return page;
        }

        private GameObject BuildSettings(Transform parent)
        {
            var page = Page("SettingsPage", parent);
            Header(page.transform, "设置", "隐私说明、随机模式与问题排查");
            Transform content = ScrollContent("SettingsScroll", page.transform, 0);
            Text appearanceTitle = Label("外观与数据", content, 31, TextAnchor.MiddleLeft, Accent); SetHeight(appearanceTitle.rectTransform, 54);
            Button("Theme", content, lightTheme ? "切换为夜间主题" : "切换为日间主题", ToggleTheme, Panel, 82);
            var historyActions = Horizontal("HistoryDataActions", content, 86);
            Button("Export", historyActions, "导出历史 JSON", () => { UserActionLog.Add("点击导出历史 JSON"); files.ExportJson(store.CreateExportJson()); }, Panel, 82);
            Button("Import", historyActions, "导入历史 JSON", files.PickJson, Panel, 82);
            Text diagnosticsTitle = Label("问题排查", content, 31, TextAnchor.MiddleLeft, Accent); SetHeight(diagnosticsTitle.rectTransform, 54);
            Text diagnosticsHint = Label("遇到问题时，先点“刷新日志预览”；需要发给开发者时，再点“导出操作日志”。", content, 24, TextAnchor.MiddleLeft, SecondaryText); SetHeight(diagnosticsHint.rectTransform, 82);
            Button("RefreshLog", content, "刷新操作日志预览", RefreshLogPreview, Panel, 82);
            Button("ExportLog", content, "导出操作日志", ExportOperationLog, Accent, 88);
            logPreview = Label("暂无操作日志。", content, 21, TextAnchor.UpperLeft, PrimaryText); SetHeight(logPreview.rectTransform, 260);
            CardText(content, "隐私\n当前问题、未保存结果和操作日志默认只在内存中。只有明确保存或导出才会写入文件。");
            CardText(content, "随机模式\n每个徽章可设置 0%–100% YES 基础概率。公平模式始终为 50/50；力度影响模式会围绕基础概率调整，0% 必定 NO、100% 必定 YES。");
            CardText(content, "本地存储\n历史记录和徽章图片副本保存在 Application.persistentDataPath。");
            CardText(content, "版本\nYesNoFilp 1.3.0 · 历史 JSON 格式 v1");
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

        private void BuildBadgeCreatePanel(Transform parent)
        {
            createBadgePanel = Image("CreateBadgePanel", parent, new Color(0.05f, .07f, .12f, .98f)).gameObject;
            Stretch((RectTransform)createBadgePanel.transform, 90, 470, 90, 470);
            var layout = createBadgePanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(36, 36, 36, 36); layout.spacing = 28; layout.childForceExpandHeight = false;
            Text title = Label("创建新徽章", createBadgePanel.transform, 42, TextAnchor.MiddleCenter, Color.white); SetHeight(title.rectTransform, 76);
            Text hint = Label("先输入名称。创建后会立即出现在列表顶部，默认使用 YES/NO 文字面和 50% 概率。", createBadgePanel.transform, 26, TextAnchor.MiddleCenter, Hex("D0D5DD")); SetHeight(hint.rectTransform, 110);
            createBadgeNameInput = Input("请输入徽章名称", createBadgePanel.transform, 100, false);
            Button("ConfirmCreate", createBadgePanel.transform, "创建徽章", ConfirmCreateBadge, Accent, 88);
            Button("CancelCreate", createBadgePanel.transform, "取消", () => createBadgePanel.SetActive(false), Panel, 78);
            createBadgePanel.SetActive(false);
        }

        private void BuildBadgeDetailPanel(Transform parent)
        {
            badgeDetailPanel = Image("BadgeDetailPanel", parent, new Color(0.05f, .07f, .12f, .98f)).gameObject;
            Stretch((RectTransform)badgeDetailPanel.transform, 54, 160, 54, 130);
            var layout = badgeDetailPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(34, 34, 30, 30); layout.spacing = 18; layout.childForceExpandHeight = false;
            badgeDetailTitle = Label("徽章设置", badgeDetailPanel.transform, 42, TextAnchor.MiddleCenter, Color.white); SetHeight(badgeDetailTitle.rectTransform, 70);
            badgeDetailNameInput = Input("徽章名称", badgeDetailPanel.transform, 88, false);
            badgeDetailFaces = Horizontal("DetailFaces", badgeDetailPanel.transform, 260);
            Text imageHint = Label("点击 YES 或 NO 图片即可上传、替换并裁切", badgeDetailPanel.transform, 23, TextAnchor.MiddleCenter, Hex("98A2B3")); SetHeight(imageHint.rectTransform, 52);
            badgeProbabilityText = Label("YES 基础概率：50%", badgeDetailPanel.transform, 30, TextAnchor.MiddleCenter, Color.white); SetHeight(badgeProbabilityText.rectTransform, 58);
            badgeProbabilitySlider = SliderControl("BadgeProbability", badgeDetailPanel.transform, 0f, 1f, value => badgeProbabilityText.text = "YES 基础概率：" + Mathf.RoundToInt(value * 100) + "%");
            Text explanation = Label("可设置 0%–100%。仅在“力度影响概率”模式生效；0% 必定 NO，100% 必定 YES。公平模式始终保持 50/50。", badgeDetailPanel.transform, 24, TextAnchor.MiddleCenter, Hex("98A2B3")); SetHeight(explanation.rectTransform, 100);
            Button("SaveDetail", badgeDetailPanel.transform, "保存徽章设置", SaveBadgeDetail, Accent, 84);
            badgeDetailDeleteButton = Button("DeleteDetail", badgeDetailPanel.transform, "删除此徽章", DeleteDetailBadge, No, 76);
            Button("CloseDetail", badgeDetailPanel.transform, "返回徽章列表", () => badgeDetailPanel.SetActive(false), Panel, 76);
            badgeDetailPanel.SetActive(false);
        }

        private void BuildSavePromptPanel(Transform parent)
        {
            savePromptPanel = Image("SavePromptPanel", parent, new Color(.05f, .07f, .12f, .97f)).gameObject;
            Stretch((RectTransform)savePromptPanel.transform, 90, 430, 90, 430);
            var layout = savePromptPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(38, 38, 38, 38); layout.spacing = 24; layout.childForceExpandHeight = false;
            savePromptTitle = Label("是否保存本次结果？", savePromptPanel.transform, 40, TextAnchor.MiddleCenter, Color.white); SetHeight(savePromptTitle.rectTransform, 100);
            savePromptNote = Input("可选：添加备注", savePromptPanel.transform, 100, false);
            Button("ConfirmSave", savePromptPanel.transform, "保存本次记录", SaveCurrent, Accent, 88);
            Button("KeepMemoryOnly", savePromptPanel.transform, "暂不保存", () => { savePromptPanel.SetActive(false); pending = null; }, Panel, 82);
            savePromptPanel.SetActive(false);
        }

        private void BuildCropPanel(Transform parent)
        {
            cropPanel = Image("CropPanel", parent, new Color(.05f, .07f, .12f, .98f)).gameObject;
            Stretch((RectTransform)cropPanel.transform, 70, 160, 70, 130);
            var layout = cropPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(34, 34, 30, 30); layout.spacing = 16; layout.childForceExpandHeight = false;
            Text title = Label("裁切圆形徽章", cropPanel.transform, 40, TextAnchor.MiddleCenter, Color.white); SetHeight(title.rectTransform, 66);
            cropPreview = Image("CropPreview", cropPanel.transform, Color.white); cropPreview.preserveAspect = true; SetHeight(cropPreview.rectTransform, 520);
            Text hint = Label("调整缩放与位置；YES/NO 均输出为 512×512 圆形 PNG", cropPanel.transform, 23, TextAnchor.MiddleCenter, Hex("D0D5DD")); SetHeight(hint.rectTransform, 60);
            Text zoomLabel = Label("缩放", cropPanel.transform, 22, TextAnchor.MiddleLeft, Color.white); SetHeight(zoomLabel.rectTransform, 36);
            cropZoom = SliderControl("CropZoom", cropPanel.transform, 1f, 3f, _ => UpdateCropPreview());
            Text xLabel = Label("左右位置", cropPanel.transform, 22, TextAnchor.MiddleLeft, Color.white); SetHeight(xLabel.rectTransform, 36);
            cropX = SliderControl("CropX", cropPanel.transform, -1f, 1f, _ => UpdateCropPreview());
            Text yLabel = Label("上下位置", cropPanel.transform, 22, TextAnchor.MiddleLeft, Color.white); SetHeight(yLabel.rectTransform, 36);
            cropY = SliderControl("CropY", cropPanel.transform, -1f, 1f, _ => UpdateCropPreview());
            Button("ConfirmCrop", cropPanel.transform, "确认裁切并保存", ConfirmCrop, Accent, 84);
            Button("CancelCrop", cropPanel.transform, "取消", () => cropPanel.SetActive(false), Panel, 74);
            cropPanel.SetActive(false);
        }

        private void Throw(float strength, string source, float heldSeconds)
        {
            string question = questionInput.text.Trim();
            BadgeDefinition selectedBadge = store.SelectedBadge();
            float effectiveProbability = DecisionEngine.EffectiveYesProbability(strength, mode, selectedBadge.yesProbability);
            int targetWins = seriesLength / 2 + 1;
            int yesWins = 0, noWins = 0;
            while (yesWins < targetWins && noWins < targetWins)
            {
                if (DecisionEngine.Decide(strength, mode, selectedBadge.yesProbability)) yesWins++; else noWins++;
            }
            bool yes = yesWins > noWins;
            pendingHoldSeconds = heldSeconds;
            pending = new PendingDecision { Question = question, IsYes = yes, Strength = strength, StrengthSource = source, Mode = mode, TimestampUtc = DateTime.UtcNow, BadgeId = selectedBadge.id, YesProbabilityUsed = effectiveProbability, SeriesLength = seriesLength, YesWins = yesWins, NoWins = noWins };
            sessionDecisions.Insert(0, pending);
            UserActionLog.Add("开始投掷；问题=" + (string.IsNullOrEmpty(question) ? "（未填写）" : question) + "；赛制=" + SeriesLabel(seriesLength) + "；徽章=" + selectedBadge.name + "；力度=" + Mathf.RoundToInt(strength * 100) + "%");
            StopAllCoroutines(); StartCoroutine(AnimateThrow(pending));
        }

        private IEnumerator AnimateThrow(PendingDecision value)
        {
            float duration = Mathf.Clamp(pendingHoldSeconds, 1f, 5f);
            Vector2 start = disc.rectTransform.anchoredPosition;
            BadgeDefinition animationBadge = store.Badges.badges.Find(item => item.id == value.BadgeId) ?? store.SelectedBadge();
            int lastFace = -1;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = t / duration;
                int face = Mathf.FloorToInt(p * (12 + value.Strength * 8)) % 2;
                if (face != lastFace) { RenderDiscFace(face == 0, animationBadge); lastFace = face; }
                float holdFactor = Mathf.InverseLerp(0f, 5f, Mathf.Clamp(pendingHoldSeconds, 0f, 5f));
                float height = Mathf.Sin(p * Mathf.PI) * Mathf.Lerp(220f, 620f, holdFactor);
                disc.rectTransform.anchoredPosition = start + Vector2.up * height;
                disc.rectTransform.localEulerAngles = new Vector3(0, p * (720 + 1080 * value.Strength), p * 80);
                disc.rectTransform.localScale = new Vector3(Mathf.Max(.08f, Mathf.Abs(Mathf.Cos(p * Mathf.PI * (5 + value.Strength * 5)))), 1, 1);
                yield return null;
            }
            disc.rectTransform.anchoredPosition = start;
            disc.rectTransform.localEulerAngles = Vector3.zero; disc.rectTransform.localScale = Vector3.one;
            RenderDiscFace(value.IsYes, animationBadge);
            status.text = (value.IsYes ? "YES" : "NO") + "  ·  " + SeriesScore(value) + "  ·  力度 " + Mathf.RoundToInt(value.Strength * 100) + "%\n尚未保存";
            UserActionLog.Add("投掷完成；结果=" + (value.IsYes ? "YES" : "NO"));
            RefreshHistory();
            savePromptNote.text = string.Empty;
            savePromptTitle.text = (value.IsYes ? "YES" : "NO") + " · " + SeriesScore(value) + "\n是否保存本次结果？";
            savePromptPanel.SetActive(true);
        }

        private void SaveCurrent()
        {
            if (pending == null) return;
            DecisionRecord record = store.SaveExplicit(pending, savePromptNote.text.Trim());
            pending.Note = record.note;
            pending.SavedRecordId = record.id;
            UserActionLog.Add("明确保存本次记录；结果=" + (pending.IsYes ? "YES" : "NO"));
            pending = null; savePromptPanel.SetActive(false);
            status.text = "本次记录已永久保存。"; RefreshHistory();
        }

        private void ToggleMode()
        {
            mode = mode == DecisionMode.Fair5050 ? DecisionMode.StrengthInfluences : DecisionMode.Fair5050;
            modeText.text = ModeLabel(mode);
            UserActionLog.Add("切换随机模式：" + ModeLabel(mode));
        }

        private void ToggleSeries()
        {
            seriesLength = seriesLength == 1 ? 3 : (seriesLength == 3 ? 5 : 1);
            seriesText.text = SeriesLabel(seriesLength);
            UserActionLog.Add("切换赛制：" + SeriesLabel(seriesLength));
        }

        private void CreateBadge()
        {
            createBadgeNameInput.text = string.Empty;
            createBadgePanel.SetActive(true);
            UserActionLog.Add("打开创建徽章弹窗");
        }

        private void ConfirmCreateBadge()
        {
            string name = createBadgeNameInput.text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                createBadgeNameInput.placeholder.GetComponent<Text>().text = "名称不能为空，请输入徽章名称";
                UserActionLog.Add("创建徽章被阻止：名称为空");
                return;
            }
            BadgeDefinition badge = store.CreateBadge(name);
            imageTarget = badge;
            badgeStatus.text = "已创建“" + badge.name + "”，默认 YES/NO 两面、YES 概率 50%，可以直接使用。";
            UserActionLog.Add("创建徽章：" + badge.name);
            createBadgePanel.SetActive(false);
            RefreshBadges();
        }

        private void RefreshBadges()
        {
            if (badgeList == null) return; Clear(badgeList);
            UpdateSelectedBadgeText();
            var ordered = new List<BadgeDefinition>();
            if (imageTarget != null && store.Badges.badges.Contains(imageTarget)) ordered.Add(imageTarget);
            foreach (BadgeDefinition item in store.Badges.badges) if (!ordered.Contains(item)) ordered.Add(item);
            foreach (BadgeDefinition badgeItem in ordered)
            {
                BadgeDefinition badge = badgeItem;
                bool complete = DecisionStore.IsBadgeComplete(badge);
                var card = VerticalCard("Badge", badgeList, 400);
                Text name = Label((badge.id == store.Badges.selectedBadgeId ? "●  当前使用：" : "○  ") + badge.name, card, 32, TextAnchor.MiddleLeft, PrimaryText); SetHeight(name.rectTransform, 54);
                var previews = Horizontal("FacePreviews", card, 150);
                AddFacePreview(previews, badge, true, !badge.builtIn);
                AddFacePreview(previews, badge, false, !badge.builtIn);
                var actions = Horizontal("BadgeActions", card, 68);
                Button use = Button("Use", actions, complete ? "使用此徽章" : "两面未补齐", () => SelectBadgeForUse(badge), Accent, 66);
                use.interactable = complete;
                Button("OpenDetail", actions, "进入设置  ›", () => OpenBadgeDetail(badge), Panel, 66);
                Text probability = Label("YES 基础概率：" + Mathf.RoundToInt(badge.yesProbability * 100) + "%  ·  " + (badge.builtIn ? "默认徽章" : "点击图片可替换"), card, 21, TextAnchor.MiddleLeft, Accent); SetHeight(probability.rectTransform, 42);
                GetBadgeStats(badge.id, out int total, out int yesCount, out int noCount);
                float yesPercent = total == 0 ? 0f : yesCount * 100f / total;
                Text stats = Label("使用 " + total + " 次  ·  YES " + yesCount + "（" + yesPercent.ToString("0.#") + "%）  ·  NO " + noCount + "（" + (total == 0 ? 0f : 100f - yesPercent).ToString("0.#") + "%）", card, 21, TextAnchor.MiddleLeft, SecondaryText); SetHeight(stats.rectTransform, 42);
            }
            badgeStatus.text = "当前共有 " + store.Badges.badges.Count + " 个徽章。新创建的徽章会显示在列表顶部。";
            StartCoroutine(RefreshBadgeListLayout());
        }

        private IEnumerator RefreshBadgeListLayout()
        {
            yield return null;
            if (badgeList == null) yield break;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)badgeList);
            ScrollRect scroll = badgeList.parent.GetComponent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void SelectBadgeForUse(BadgeDefinition badge)
        {
            if (!DecisionStore.IsBadgeComplete(badge)) return;
            store.SelectBadge(badge.id);
            RenderDiscFace(true, badge);
            RefreshHomeFaces();
            UpdateSelectedBadgeText();
            badgeStatus.text = "已切换到“" + badge.name + "”。";
            UserActionLog.Add("切换当前徽章：" + badge.name);
            RefreshBadges();
            ShowPage(0);
        }

        private void UpdateSelectedBadgeText()
        {
            if (selectedBadgeText == null) return;
            BadgeDefinition badge = store.SelectedBadge();
            selectedBadgeText.text = "当前徽章：" + badge.name + "  ·  YES 基础概率 " + Mathf.RoundToInt(badge.yesProbability * 100) + "%";
        }

        private void OpenBadgeDetail(BadgeDefinition badge)
        {
            detailBadge = badge;
            badgeDetailTitle.text = "徽章设置 · " + badge.name;
            badgeDetailNameInput.text = badge.name;
            badgeDetailNameInput.interactable = !badge.builtIn;
            badgeProbabilitySlider.value = badge.yesProbability;
            badgeProbabilityText.text = "YES 基础概率：" + Mathf.RoundToInt(badge.yesProbability * 100) + "%";
            badgeDetailDeleteButton.interactable = !badge.builtIn;
            RefreshBadgeDetailFaces();
            badgeDetailPanel.SetActive(true);
            UserActionLog.Add("进入徽章设置：" + badge.name);
        }

        private void RefreshBadgeDetailFaces()
        {
            if (detailBadge == null || badgeDetailFaces == null) return;
            Clear(badgeDetailFaces);
            AddFacePreview(badgeDetailFaces, detailBadge, true, !detailBadge.builtIn);
            AddFacePreview(badgeDetailFaces, detailBadge, false, !detailBadge.builtIn);
        }

        private void SaveBadgeDetail()
        {
            if (detailBadge == null) return;
            if (!detailBadge.builtIn)
            {
                string name = badgeDetailNameInput.text.Trim();
                if (string.IsNullOrEmpty(name)) { badgeStatus.text = "徽章名称不能为空。"; return; }
                store.RenameBadge(detailBadge, name);
            }
            store.SetBadgeProbability(detailBadge, badgeProbabilitySlider.value);
            badgeStatus.text = "已保存“" + detailBadge.name + "”的设置，YES 基础概率为 " + Mathf.RoundToInt(detailBadge.yesProbability * 100) + "% 。";
            UserActionLog.Add("保存徽章设置：" + detailBadge.name + "；YES 基础概率=" + Mathf.RoundToInt(detailBadge.yesProbability * 100) + "%");
            badgeDetailTitle.text = "徽章设置 · " + detailBadge.name;
            UpdateSelectedBadgeText();
            RefreshBadges();
        }

        private void DeleteDetailBadge()
        {
            if (detailBadge == null || detailBadge.builtIn) return;
            UserActionLog.Add("删除徽章：" + detailBadge.name);
            store.DeleteBadge(detailBadge.id);
            if (imageTarget == detailBadge) imageTarget = null;
            detailBadge = null; badgeDetailPanel.SetActive(false); UpdateSelectedBadgeText(); RefreshBadges();
        }

        private void PickBadgeImage(BadgeDefinition badge, bool yesFace) { imageTarget = badge; imageTargetIsYes = yesFace; badgeStatus.text = "正在为“" + badge.name + "”选择 " + (yesFace ? "YES" : "NO") + " 面图片…"; UserActionLog.Add("选择徽章图片：" + badge.name + " / " + (yesFace ? "YES" : "NO")); files.PickImage(); }

        private void ApplyPickedImage(string path)
        {
            try
            {
                if (imageTarget == null) throw new InvalidOperationException("没有正在编辑的徽章。");
                cropSourcePath = path;
                cropZoom.value = 1f; cropX.value = 0f; cropY.value = 0f;
                UpdateCropPreview();
                cropPanel.SetActive(true);
            }
            catch (Exception exception) { badgeStatus.text = "图片保存失败：" + exception.Message; UserActionLog.Add("徽章图片保存失败：" + exception.Message); Debug.LogWarning(exception); }
        }

        private void RefreshHistory()
        {
            if (historyList == null) return;
            Clear(historyList);
            var items = new List<HistoryViewItem>();
            var representedSavedIds = new HashSet<string>();
            foreach (PendingDecision session in sessionDecisions)
            {
                if (!string.IsNullOrEmpty(session.SavedRecordId)) representedSavedIds.Add(session.SavedRecordId);
                items.Add(new HistoryViewItem { session = session, record = string.IsNullOrEmpty(session.SavedRecordId) ? null : store.History.records.Find(r => r.id == session.SavedRecordId), time = session.TimestampUtc });
            }
            foreach (DecisionRecord record in store.History.records)
            {
                if (representedSavedIds.Contains(record.id)) continue;
                DateTime.TryParse(record.timestampUtc, out DateTime timestamp);
                items.Add(new HistoryViewItem { record = record, time = timestamp });
            }
            items.Sort((a, b) => b.time.CompareTo(a.time));
            int visible = 0;
            foreach (HistoryViewItem item in items)
            {
                if (!string.IsNullOrEmpty(historyFilterBadgeId) && item.BadgeId != historyFilterBadgeId) continue;
                AddHistoryCard(item); visible++;
            }
            if (visible == 0) CardText(historyList, "当前筛选条件下暂无记录。投掷后的结果会按时间显示在这里。");
        }

        private void AddHistoryCard(HistoryViewItem item)
        {
            PendingDecision session = item.session;
            DecisionRecord record = item.record;
            bool saved = record != null;
            bool isYes = saved ? record.result == "YES" : session.IsYes;
            string question = saved ? record.question : session.Question;
            string badgeId = saved ? record.badgeId : session.BadgeId;
            BadgeDefinition badge = store.Badges.badges.Find(b => b.id == badgeId);
            string badgeName = badge == null ? "未知徽章" : badge.name;
            string result = isYes ? "YES" : "NO";
            int rounds = saved ? Mathf.Max(1, record.seriesLength) : Mathf.Max(1, session.SeriesLength);
            int yesWins = saved ? record.yesWins : session.YesWins;
            int noWins = saved ? record.noWins : session.NoWins;
            if (yesWins + noWins == 0) { yesWins = isYes ? 1 : 0; noWins = isYes ? 0 : 1; }

            Transform card = HorizontalCard("HistoryRecord", historyList, saved ? 270 : 220);
            Transform details = VerticalContainer("Details", card, true);
            Text headline = Label(result + "  ·  " + (rounds == 1 ? "单次决定" : "比分 " + yesWins + ":" + noWins) + "  ·  " + badgeName + "  ·  " + (saved ? "已保存" : "未保存"), details, 27, TextAnchor.MiddleLeft, isYes ? Yes : No); SetHeight(headline.rectTransform, 54);
            string displayQuestion = string.IsNullOrEmpty(question) ? "（未填写问题）" : question;
            string time = saved ? LocalTime(record.timestampUtc) : session.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            Text body = Label(displayQuestion + "\n" + time + "  ·  " + (saved ? StoredModeLabel(record.mode) : ModeLabel(session.Mode)), details, 23, TextAnchor.UpperLeft, PrimaryText); SetHeight(body.rectTransform, 92);
            Transform actions = VerticalContainer("Actions", card, false); SetWidth((RectTransform)actions, 220);
            if (!saved)
            {
                Button("SaveOne", actions, "保存", () => OpenSavePromptFor(session), Accent, 70);
                Button("DeleteOne", actions, "删除", () => { sessionDecisions.Remove(session); if (pending == session) pending = null; RefreshHistory(); }, No, 70);
            }
            else
            {
                InputField note = Input("备注", details, 64, false); note.text = record.note ?? string.Empty;
                Button("SaveNote", actions, "保存备注", () => { store.UpdateRecordNote(record.id, note.text.Trim()); UserActionLog.Add("修改历史备注：" + record.id); RefreshHistory(); }, Accent, 70);
                Button("DeleteOne", actions, "删除", () =>
                {
                    store.DeleteRecord(record.id);
                    PendingDecision linked = sessionDecisions.Find(s => s.SavedRecordId == record.id);
                    if (linked != null) linked.SavedRecordId = null;
                    UserActionLog.Add("删除已保存记录：" + displayQuestion); RefreshHistory();
                }, No, 70);
            }
        }

        private void OpenSavePromptFor(PendingDecision decision)
        {
            pending = decision;
            savePromptNote.text = decision.Note ?? string.Empty;
            savePromptTitle.text = (decision.IsYes ? "YES" : "NO") + " · " + SeriesScore(decision) + "\n是否保存这条记录？";
            savePromptPanel.SetActive(true);
        }

        private void CycleHistoryFilter()
        {
            int index = string.IsNullOrEmpty(historyFilterBadgeId) ? -1 : store.Badges.badges.FindIndex(b => b.id == historyFilterBadgeId);
            index++;
            if (index >= store.Badges.badges.Count) { historyFilterBadgeId = string.Empty; historyFilterText.text = "筛选徽章：全部"; }
            else { BadgeDefinition badge = store.Badges.badges[index]; historyFilterBadgeId = badge.id; historyFilterText.text = "筛选徽章：" + badge.name; }
            RefreshHistory();
        }

        private void GetBadgeStats(string badgeId, out int total, out int yesCount, out int noCount)
        {
            total = yesCount = noCount = 0;
            var savedIds = new HashSet<string>();
            foreach (DecisionRecord record in store.History.records)
            {
                if (record.badgeId != badgeId) continue;
                total++; if (record.result == "YES") yesCount++; else noCount++;
                savedIds.Add(record.id);
            }
            foreach (PendingDecision session in sessionDecisions)
            {
                if (session.BadgeId != badgeId || (!string.IsNullOrEmpty(session.SavedRecordId) && savedIds.Contains(session.SavedRecordId))) continue;
                total++; if (session.IsYes) yesCount++; else noCount++;
            }
        }

        private void UpdateCropPreview()
        {
            if (cropPreview == null || string.IsNullOrEmpty(cropSourcePath) || !File.Exists(cropSourcePath)) return;
            if (cropPreview.sprite != null)
            {
                Texture2D oldTexture = cropPreview.sprite.texture;
                Destroy(cropPreview.sprite); Destroy(oldTexture);
            }
            cropPreview.sprite = CreateCroppedSprite(cropSourcePath, cropZoom.value, cropX.value, cropY.value, 256);
        }

        private void ConfirmCrop()
        {
            try
            {
                store.CopyBadgeImage(imageTarget, imageTargetIsYes, cropSourcePath, cropZoom.value, cropX.value, cropY.value);
                cropPanel.SetActive(false);
                badgeStatus.text = "已将“" + imageTarget.name + "”的 " + (imageTargetIsYes ? "YES" : "NO") + " 面裁切为 512×512 圆形图片。";
                UserActionLog.Add("裁切并保存徽章图片：" + imageTarget.name + " / " + (imageTargetIsYes ? "YES" : "NO"));
                RefreshBadges(); RefreshHomeFaces();
                if (detailBadge == imageTarget) RefreshBadgeDetailFaces();
                if (store.Badges.selectedBadgeId == imageTarget.id) RenderDiscFace(imageTargetIsYes, imageTarget);
            }
            catch (Exception exception) { badgeStatus.text = "图片裁切失败：" + exception.Message; UserActionLog.Add("图片裁切失败：" + exception.Message); }
        }

        private static Sprite CreateCroppedSprite(string path, float zoom, float offsetX, float offsetY, int size)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(path))) return null;
            var output = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cropSize = Mathf.Min(source.width, source.height) / Mathf.Clamp(zoom, 1f, 3f);
            float startX = Mathf.Max(0f, source.width - cropSize) * Mathf.Clamp01((offsetX + 1f) * .5f);
            float startY = Mathf.Max(0f, source.height - cropSize) * Mathf.Clamp01((offsetY + 1f) * .5f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f) / size, ny = (y + .5f) / size;
                Color pixel = source.GetPixelBilinear((startX + nx * cropSize) / source.width, (startY + ny * cropSize) / source.height);
                float dx = nx - .5f, dy = ny - .5f; if (dx * dx + dy * dy > .25f) pixel.a = 0f;
                output.SetPixel(x, y, pixel);
            }
            output.Apply(); Destroy(source);
            return Sprite.Create(output, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
        }

        private void RefreshHomeFaces()
        {
            if (homeFaces == null) return;
            Clear(homeFaces);
            BadgeDefinition badge = store.SelectedBadge();
            AddFacePreview(homeFaces, badge, true, false);
            AddFacePreview(homeFaces, badge, false, false);
        }

        private void ToggleTheme()
        {
            lightTheme = !lightTheme;
            UserActionLog.Add("切换主题：" + (lightTheme ? "日间" : "夜间"));
            if (uiRoot != null) { uiRoot.SetActive(false); Destroy(uiRoot); }
            pages.Clear();
            BuildUi(); ShowPage(3);
        }

        private void ApplyThemePalette()
        {
            Background = lightTheme ? Hex("F4F7FB") : Hex("121826");
            Panel = lightTheme ? Hex("FFFFFF") : Hex("1D2939");
            Accent = lightTheme ? Hex("16A394") : Hex("35D0BA");
            PrimaryText = lightTheme ? Hex("182230") : Color.white;
            SecondaryText = lightTheme ? Hex("667085") : Hex("98A2B3");
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

        private void AddFacePreview(Transform parent, BadgeDefinition badge, bool yesFace, bool clickable)
        {
            string path = yesFace ? badge.yesImagePath : badge.noImagePath;
            Sprite sprite = LoadSprite(path);
            Image preview = Image(yesFace ? "YesPreview" : "NoPreview", parent, sprite == null ? (yesFace ? Yes : No) : Color.white);
            preview.sprite = sprite ?? circleSprite; preview.preserveAspect = true;
            Text face = Label(sprite == null ? (yesFace ? "YES\n默认面" : "NO\n默认面") : (yesFace ? "YES" : "NO"), preview.transform, 40, TextAnchor.MiddleCenter, sprite == null ? Color.white : new Color(1, 1, 1, .85f)); Stretch(face.rectTransform);
            face.raycastTarget = false;
            if (clickable)
            {
                Button button = preview.gameObject.AddComponent<Button>();
                button.targetGraphic = preview;
                button.onClick.AddListener(() => PickBadgeImage(badge, yesFace));
            }
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
        private static string SeriesLabel(int value) { return value == 3 ? "3 局 2 胜" : value == 5 ? "5 局 3 胜" : "1 次决定"; }
        private static string SeriesScore(PendingDecision value) { return value.SeriesLength <= 1 ? "单次决定" : "比分 " + value.YesWins + ":" + value.NoWins; }
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
            Text heading = Label(title, parent, 50, TextAnchor.MiddleCenter, PrimaryText); SetHeight(heading.rectTransform, 84);
            Text detail = Label(subtitle, parent, 25, TextAnchor.MiddleCenter, SecondaryText); SetHeight(detail.rectTransform, 54);
        }

        private static Transform ScrollContent(string name, Transform parent, float height)
        {
            // RectMask2D clips by geometry and does not depend on a visible mask graphic.
            // A fully transparent Image + Mask caused every child in Android scroll views
            // to be clipped, which made badge cards and settings actions appear missing.
            var scrollObject = new GameObject(name, typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect)); scrollObject.transform.SetParent(parent, false);
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
            Text text = Label(value, card, 27, TextAnchor.MiddleLeft, PrimaryText); SetFlexible(text.rectTransform);
        }

        private static InputField Input(string placeholder, Transform parent, float height, bool multiline)
        {
            var root = Image("Input", parent, Panel); SetHeight(root.rectTransform, height);
            var field = root.gameObject.AddComponent<InputField>(); field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            Text value = Label("", root.transform, 30, TextAnchor.MiddleLeft, PrimaryText); Stretch(value.rectTransform, 24, 10, 24, 10);
            Text hint = Label(placeholder, root.transform, 28, TextAnchor.MiddleLeft, Hex("667085")); Stretch(hint.rectTransform, 24, 10, 24, 10);
            field.textComponent = value; field.placeholder = hint; field.targetGraphic = root; return field;
        }

        private static Button Button(string name, Transform parent, string value, UnityEngine.Events.UnityAction action, Color color, float height)
        {
            var image = Image(name, parent, color); SetHeight(image.rectTransform, height);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Text label = Label(value, image.transform, 26, TextAnchor.MiddleCenter, Approximately(color, Panel) ? PrimaryText : Color.white); Stretch(label.rectTransform, 8, 4, 8, 4); return button;
        }

        private Slider SliderControl(string name, Transform parent, float min, float max, UnityEngine.Events.UnityAction<float> changed)
        {
            Image root = Image(name, parent, Panel); SetHeight(root.rectTransform, 76);
            Image fill = Image("Fill", root.transform, Accent);
            fill.rectTransform.anchorMin = new Vector2(0, .28f); fill.rectTransform.anchorMax = new Vector2(1, .72f); fill.rectTransform.offsetMin = new Vector2(24, 0); fill.rectTransform.offsetMax = new Vector2(-24, 0);
            Image handle = Image("Handle", root.transform, Color.white); handle.sprite = circleSprite; handle.preserveAspect = true; handle.rectTransform.sizeDelta = new Vector2(58, 58);
            var slider = root.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(changed); return slider;
        }

        private static Transform Horizontal(string name, Transform parent, float height)
        {
            var root = Rect(name, parent); SetHeight(root, height);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 12; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandWidth = true; return root;
        }

        private static Transform HorizontalCard(string name, Transform parent, float height)
        {
            Image root = Image(name, parent, Panel); SetHeight(root.rectTransform, height);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 18); layout.spacing = 16;
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            return root.transform;
        }

        private static Transform VerticalContainer(string name, Transform parent, bool flexibleWidth)
        {
            RectTransform root = Rect(name, parent);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            if (flexibleWidth) { var element = root.gameObject.AddComponent<LayoutElement>(); element.flexibleWidth = 1f; }
            return root;
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
        private static void SetWidth(RectTransform rect, float width) { var e = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.preferredWidth = width; e.minWidth = width; }
        private static void SetFlexible(RectTransform rect) { var e = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.flexibleHeight = 1; }
        private static void Clear(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) { GameObject child = parent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); } }
        private static string Present(string path) { return string.IsNullOrEmpty(path) || !File.Exists(path) ? "默认文字面" : Path.GetFileName(path) + "（应用内部副本）"; }
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color result); return result; }
        private static bool Approximately(Color a, Color b) { return Mathf.Abs(a.r - b.r) < .001f && Mathf.Abs(a.g - b.g) < .001f && Mathf.Abs(a.b - b.b) < .001f && Mathf.Abs(a.a - b.a) < .001f; }

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
