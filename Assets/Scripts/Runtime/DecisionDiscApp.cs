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
        private static Sprite softRectSprite;

        private DecisionStore store;
        private AndroidFileBridge files;
        private readonly List<GameObject> pages = new List<GameObject>();
        private InputField questionInput;
        private Transform homeFaces;
        private Transform throwStage;
        private readonly List<CoinRenderView> throwDiscs = new List<CoinRenderView>();
        private readonly List<Text> throwDiscLabels = new List<Text>();
        private readonly List<Vector2> throwDiscBasePositions = new List<Vector2>();
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
        private InputField badgeProbabilityInput;
        private Text badgeProbabilityText;
        private Transform badgeDetailFaces;
        private Button badgeDetailDeleteButton;
        private GameObject savePromptPanel;
        private Text savePromptTitle;
        private InputField savePromptNote;
        private Transform savePromptFaces;
        private GameObject seriesPanel;
        private GameObject modalBackdrop;
        private GameObject cropPanel;
        private Image cropPreview;
        private CropGestureHandler cropGesture;
        private string cropSourcePath;
        private Text historyFilterText;
        private string historyFilterBadgeId = string.Empty;
        private GameObject uiRoot;
        private static bool lightTheme = true;
        private int seriesLength = 1;
        private float pendingHoldSeconds;
        private BadgeDefinition detailBadge;
        private HistoryExport pendingImport;
        private BadgeDefinition imageTarget;
        private bool imageTargetIsYes;
        private PendingDecision pending;
        private DecisionMode mode = DecisionMode.Fair5050;
        private Sprite circleSprite;
        private Texture2D defaultYesTexture;
        private Texture2D defaultNoTexture;
        private Sprite defaultYesSprite;
        private Sprite defaultNoSprite;
        private Texture2D throwButtonIconTexture;
        private Sprite throwButtonIconSprite;
        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
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
            UnityEngine.Input.multiTouchEnabled = true;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Noto Sans SC", "Droid Sans Fallback", "Arial Unicode MS" }, 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            circleSprite = CreateCircleSprite();
            softRectSprite = CreateRoundedRectSprite();
            defaultYesTexture = Resources.Load<Texture2D>("Theme/default-yes-symbol");
            defaultNoTexture = Resources.Load<Texture2D>("Theme/default-no-symbol");
            throwButtonIconTexture = Resources.Load<Texture2D>("Theme/throw-button-icon");
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
            if (lightTheme)
            {
                Texture2D themeArt = Resources.Load<Texture2D>("Theme/resonance-day-background");
                if (themeArt != null)
                {
                    GameObject artObject = new GameObject("ResonanceThemeArt", typeof(RectTransform), typeof(RawImage));
                    artObject.transform.SetParent(background.transform, false);
                    RawImage art = artObject.GetComponent<RawImage>(); art.texture = themeArt; art.color = new Color(1f, 1f, 1f, .72f); art.raycastTarget = false;
                    Stretch(art.rectTransform);
                    Image veil = Image("ReadabilityVeil", background.transform, new Color(.97f, .99f, 1f, .20f)); Stretch(veil.rectTransform); veil.raycastTarget = false;
                }
            }

            var safe = Rect("Safe Area", background.transform); Stretch(safe); safe.gameObject.AddComponent<SafeAreaFitter>();
            var pageHost = Rect("Pages", safe); Stretch(pageHost, 0, 94, 0, 0);
            pages.Add(BuildHome(pageHost));
            pages.Add(BuildBadges(pageHost));
            pages.Add(BuildHistory(pageHost));
            pages.Add(BuildSettings(pageHost));
            BuildNavigation(safe);
            BuildModalBackdrop(safe);
            BuildImportPanel(safe);
            BuildBadgeCreatePanel(safe);
            BuildBadgeDetailPanel(safe);
            BuildSavePromptPanel(safe);
            BuildSeriesPanel(safe);
            BuildCropPanel(safe);
        }

        private GameObject BuildHome(Transform parent)
        {
            var page = Page("ThrowPage", parent);
            Text title = Label("YES / NO 决策", page.transform, 46, TextAnchor.MiddleCenter, PrimaryText);
            SetHeight(title.rectTransform, 62);
            Text sub = Label("按住蓄力，松开投掷", page.transform, 24, TextAnchor.MiddleCenter, SecondaryText); SetHeight(sub.rectTransform, 38);

            RectTransform visualStage = Rect("VisualStage", page.transform); SetHeight(visualStage, 520);
            homeFaces = Rect("HomeFaces", visualStage); Stretch((RectTransform)homeFaces);
            var homeLayout = homeFaces.gameObject.AddComponent<HorizontalLayoutGroup>(); homeLayout.spacing = 34; homeLayout.padding = new RectOffset(18, 18, 10, 10); homeLayout.childControlHeight = true; homeLayout.childControlWidth = true; homeLayout.childForceExpandWidth = true;
            throwStage = Rect("ThrowStage", visualStage); Stretch((RectTransform)throwStage);
            var throwLayout = throwStage.gameObject.AddComponent<HorizontalLayoutGroup>(); throwLayout.spacing = 12; throwLayout.padding = new RectOffset(12, 12, 30, 30); throwLayout.childControlHeight = true; throwLayout.childControlWidth = true; throwLayout.childForceExpandWidth = true;
            throwStage.gameObject.SetActive(false);
            RefreshHomeFaces();

            RectTransform badgeSwitch = Rect("BadgeSwitch", page.transform); SetHeight(badgeSwitch, 50);
            selectedBadgeText = Label("使用中 · " + store.SelectedBadge().name, badgeSwitch, 22, TextAnchor.MiddleLeft, SecondaryText);
            Stretch(selectedBadgeText.rectTransform, 0, 0, 170, 0);
            Button switchBadge = Button("SwitchBadge", badgeSwitch, "更换", () => ShowPage(1), Panel, 50);
            RectTransform switchBadgeRect = switchBadge.GetComponent<RectTransform>();
            switchBadgeRect.anchorMin = new Vector2(1f, 0f); switchBadgeRect.anchorMax = Vector2.one;
            switchBadgeRect.pivot = new Vector2(1f, .5f); switchBadgeRect.anchoredPosition = Vector2.zero; switchBadgeRect.sizeDelta = new Vector2(150, 0);

            questionInput = Input("可选：输入本次要决定的问题", page.transform, 104, false);
            var modeButton = Button("Mode", page.transform, "公平 50 / 50", ToggleMode, Panel, 80);
            modeText = modeButton.GetComponentInChildren<Text>();
            var seriesButton = Button("Series", page.transform, "赛制：1 次决定  ›", () => OpenModal(seriesPanel), Panel, 76);
            seriesText = seriesButton.GetComponentInChildren<Text>();

            var chargeObject = new GameObject("Charge", typeof(RectTransform), typeof(Image), typeof(ChargeThrowButton));
            chargeObject.transform.SetParent(page.transform, false); SetHeight((RectTransform)chargeObject.transform, 140);
            Image chargeBackground = chargeObject.GetComponent<Image>();
            chargeBackground.color = lightTheme ? new Color(.93f, .985f, 1f, .88f) : Hex("243B53");
            chargeBackground.sprite = softRectSprite; chargeBackground.type = UnityEngine.UI.Image.Type.Sliced;
            Outline chargeEdge = chargeObject.AddComponent<Outline>(); chargeEdge.effectColor = new Color(Accent.r, Accent.g, Accent.b, .35f); chargeEdge.effectDistance = new Vector2(2f, -2f);
            var fill = Image("Fill", chargeObject.transform, Accent); Stretch(fill.rectTransform); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0;
            Image launchIcon = Image("ThrowIcon", chargeObject.transform, Color.white);
            if (throwButtonIconTexture != null)
            {
                throwButtonIconSprite = Sprite.Create(throwButtonIconTexture, new Rect(0, 0, throwButtonIconTexture.width, throwButtonIconTexture.height), new Vector2(.5f, .5f), 100f);
                launchIcon.sprite = throwButtonIconSprite;
            }
            launchIcon.preserveAspect = true; launchIcon.raycastTarget = false;
            launchIcon.rectTransform.anchorMin = launchIcon.rectTransform.anchorMax = new Vector2(0f, .5f);
            launchIcon.rectTransform.pivot = new Vector2(0f, .5f); launchIcon.rectTransform.anchoredPosition = new Vector2(18f, 0f); launchIcon.rectTransform.sizeDelta = new Vector2(104f, 104f);
            var chargeLabel = Label("按住蓄力，松开投掷", chargeObject.transform, 40, TextAnchor.MiddleCenter, PrimaryText); Stretch(chargeLabel.rectTransform, 116, 0, 18, 0);
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
            Header(page.transform, "历史记录", "仅显示你在结果弹窗中明确保存的记录");
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
            CardText(content, "隐私\n投掷结果只在确认弹窗中临时存在；选择不保存会立即删除。只有明确保存或导出才会写入文件。");
            CardText(content, "随机模式\n每个徽章可设置 0%–100% YES 基础概率。公平模式始终为 50/50；力度影响模式会围绕基础概率调整，0% 必定 NO、100% 必定 YES。");
            CardText(content, "本地存储\n历史记录和徽章图片副本保存在 Application.persistentDataPath。");
            CardText(content, "版本\nYesNoFilp 1.3.6 · 历史 JSON 格式 v1");
            return page;
        }

        private void BuildNavigation(Transform safe)
        {
            var nav = Horizontal("Navigation", safe, 94);
            var rt = (RectTransform)nav; rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, 94);
            string[] names = { "◆  投掷", "◉  徽章", "▤  记录", "⚙  设置" };
            for (int i = 0; i < names.Length; i++) { int index = i; Button("Nav" + i, nav, names[i], () => ShowPage(index), Panel, 94); }
        }

        private void BuildModalBackdrop(Transform parent)
        {
            Image backdrop = Image("ModalBackdrop", parent, new Color(.02f, .05f, .10f, .38f));
            Stretch(backdrop.rectTransform);
            Button dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.onClick.AddListener(CloseAllModals);
            modalBackdrop = backdrop.gameObject;
            modalBackdrop.SetActive(false);
        }

        private void OpenModal(GameObject panel)
        {
            if (panel == null) return;
            if (modalBackdrop != null) modalBackdrop.SetActive(true);
            panel.SetActive(true);
        }

        private void CloseModal(GameObject panel)
        {
            if (panel != null) panel.SetActive(false);
            if (modalBackdrop != null) modalBackdrop.SetActive(AnyModalOpen());
        }

        private bool AnyModalOpen()
        {
            return IsOpen(importPanel) || IsOpen(createBadgePanel) || IsOpen(badgeDetailPanel) || IsOpen(savePromptPanel) || IsOpen(seriesPanel) || IsOpen(cropPanel);
        }

        private static bool IsOpen(GameObject panel) { return panel != null && panel.activeSelf; }

        private void CloseAllModals()
        {
            if (IsOpen(savePromptPanel) && pending != null) { DiscardCurrent(); return; }
            if (importPanel != null) importPanel.SetActive(false);
            if (createBadgePanel != null) createBadgePanel.SetActive(false);
            if (badgeDetailPanel != null) badgeDetailPanel.SetActive(false);
            if (savePromptPanel != null) savePromptPanel.SetActive(false);
            if (seriesPanel != null) seriesPanel.SetActive(false);
            if (cropPanel != null) cropPanel.SetActive(false);
            if (modalBackdrop != null) modalBackdrop.SetActive(false);
        }

        private void BuildImportPanel(Transform parent)
        {
            importPanel = Image("ImportPreviewPanel", parent, new Color(0.05f, .07f, .12f, .97f)).gameObject; Stretch((RectTransform)importPanel.transform, 70, 220, 70, 220);
            var layout = importPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(32, 32, 32, 32); layout.spacing = 24;
            Label("导入预览", importPanel.transform, 42, TextAnchor.MiddleCenter, Color.white);
            importPreview = Label("", importPanel.transform, 28, TextAnchor.UpperLeft, Color.white); SetFlexible(importPreview.rectTransform);
            Button("Merge", importPanel.transform, "与已保存记录合并", () => ApplyImport(false), Accent, 86);
            Button("Replace", importPanel.transform, "替换全部已保存记录", () => ApplyImport(true), No, 86);
            Button("Cancel", importPanel.transform, "取消", () => CloseModal(importPanel), Panel, 76);
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
            Button("CancelCreate", createBadgePanel.transform, "取消", () => CloseModal(createBadgePanel), Panel, 78);
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
            badgeProbabilityText = Label("YES 概率  50%", badgeDetailPanel.transform, 30, TextAnchor.MiddleCenter, Color.white); SetHeight(badgeProbabilityText.rectTransform, 52);
            Transform probabilityRow = Horizontal("ProbabilityControls", badgeDetailPanel.transform, 82);
            badgeProbabilitySlider = SliderControl("BadgeProbability", probabilityRow, 0f, 1f, OnProbabilitySliderChanged);
            badgeProbabilityInput = Input("0–100", probabilityRow, 76, false); SetWidth(badgeProbabilityInput.GetComponent<RectTransform>(), 170);
            badgeProbabilityInput.contentType = InputField.ContentType.IntegerNumber;
            badgeProbabilityInput.onEndEdit.AddListener(ApplyProbabilityInput);
            Text explanation = Label("可设置 0%–100%。仅在“力度影响概率”模式生效；0% 必定 NO，100% 必定 YES。公平模式始终保持 50/50。", badgeDetailPanel.transform, 24, TextAnchor.MiddleCenter, Hex("98A2B3")); SetHeight(explanation.rectTransform, 100);
            Button("SaveDetail", badgeDetailPanel.transform, "保存徽章设置", SaveBadgeDetail, Accent, 84);
            badgeDetailDeleteButton = Button("DeleteDetail", badgeDetailPanel.transform, "删除此徽章", DeleteDetailBadge, No, 76);
            Button("CloseDetail", badgeDetailPanel.transform, "返回徽章列表", () => CloseModal(badgeDetailPanel), Panel, 76);
            badgeDetailPanel.SetActive(false);
        }

        private void BuildSavePromptPanel(Transform parent)
        {
            savePromptPanel = Image("SavePromptPanel", parent, new Color(.05f, .07f, .12f, .97f)).gameObject;
            RectTransform promptRect = (RectTransform)savePromptPanel.transform;
            promptRect.anchorMin = new Vector2(0, 0); promptRect.anchorMax = new Vector2(1, 0); promptRect.pivot = new Vector2(.5f, 0);
            promptRect.offsetMin = new Vector2(50, 115); promptRect.offsetMax = new Vector2(-50, 1015);
            var layout = savePromptPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(38, 38, 38, 38); layout.spacing = 24; layout.childForceExpandHeight = false;
            savePromptTitle = Label("是否保存本次结果？", savePromptPanel.transform, 40, TextAnchor.MiddleCenter, Color.white); SetHeight(savePromptTitle.rectTransform, 100);
            savePromptFaces = Horizontal("SavePromptFaces", savePromptPanel.transform, 220);
            Text noteTitle = Label("备注（可选，可在历史记录中继续修改）", savePromptPanel.transform, 25, TextAnchor.MiddleLeft, Color.white); SetHeight(noteTitle.rectTransform, 46);
            savePromptNote = NoteInput("输入本次备注", savePromptPanel.transform, 110);
            Button("ConfirmSave", savePromptPanel.transform, "保存本次记录", SaveCurrent, Accent, 88);
            Button("DiscardResult", savePromptPanel.transform, "不保存并删除本次结果", DiscardCurrent, Panel, 82);
            savePromptPanel.SetActive(false);
        }

        private void BuildSeriesPanel(Transform parent)
        {
            seriesPanel = Image("SeriesPanel", parent, new Color(.05f, .07f, .12f, .97f)).gameObject;
            Stretch((RectTransform)seriesPanel.transform, 110, 520, 110, 520);
            var layout = seriesPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(40, 40, 40, 40); layout.spacing = 24; layout.childForceExpandHeight = false;
            Text title = Label("选择投掷赛制", seriesPanel.transform, 42, TextAnchor.MiddleCenter, Color.white); SetHeight(title.rectTransform, 90);
            Text hint = Label("多局赛制会同时投出对应数量的徽章\n公平模式中，单边出现 0:3 的理论概率为 12.5%", seriesPanel.transform, 24, TextAnchor.MiddleCenter, Hex("D0D5DD")); SetHeight(hint.rectTransform, 86);
            Button("One", seriesPanel.transform, "1 次决定", () => SelectSeries(1), Panel, 88);
            Button("Three", seriesPanel.transform, "3 局 2 胜", () => SelectSeries(3), Panel, 88);
            Button("Five", seriesPanel.transform, "5 局 3 胜", () => SelectSeries(5), Panel, 88);
            Button("Close", seriesPanel.transform, "取消", () => CloseModal(seriesPanel), No, 78);
            seriesPanel.SetActive(false);
        }

        private void BuildCropPanel(Transform parent)
        {
            cropPanel = Image("CropPanel", parent, new Color(.05f, .07f, .12f, .98f)).gameObject;
            Stretch((RectTransform)cropPanel.transform, 70, 160, 70, 130);
            var layout = cropPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(34, 34, 30, 30); layout.spacing = 20; layout.childForceExpandHeight = false;
            Text title = Label("裁切圆形徽章", cropPanel.transform, 40, TextAnchor.MiddleCenter, Color.white); SetHeight(title.rectTransform, 66);
            RectTransform cropStage = Rect("CropStage", cropPanel.transform); SetHeight(cropStage, 660);
            Image viewport = Image("CircleViewport", cropStage, Color.white); viewport.sprite = circleSprite; viewport.preserveAspect = true;
            viewport.rectTransform.anchorMin = viewport.rectTransform.anchorMax = new Vector2(.5f, .5f); viewport.rectTransform.sizeDelta = new Vector2(620, 620);
            Mask circleMask = viewport.gameObject.AddComponent<Mask>(); circleMask.showMaskGraphic = false;
            cropPreview = Image("CropPreview", viewport.transform, Color.white); cropPreview.rectTransform.anchorMin = cropPreview.rectTransform.anchorMax = new Vector2(.5f, .5f); cropPreview.rectTransform.pivot = new Vector2(.5f, .5f);
            cropGesture = viewport.gameObject.AddComponent<CropGestureHandler>(); cropGesture.Target = cropPreview.rectTransform; cropGesture.Viewport = viewport.rectTransform;
            Image ring = Image("CropRing", cropStage, Color.white); ring.sprite = CreateRingSprite(); ring.preserveAspect = true; ring.raycastTarget = false;
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(.5f, .5f); ring.rectTransform.sizeDelta = new Vector2(632, 632);
            Text hint = Label("单指拖动图片 · 双指捏合缩放 · 圆圈内为最终徽章", cropPanel.transform, 25, TextAnchor.MiddleCenter, Hex("D0D5DD")); SetHeight(hint.rectTransform, 70);
            Button("ConfirmCrop", cropPanel.transform, "确认裁切并保存", ConfirmCrop, Accent, 84);
            Button("CancelCrop", cropPanel.transform, "取消", () => CloseModal(cropPanel), Panel, 74);
            cropPanel.SetActive(false);
        }

        private void Throw(float strength, string source, float heldSeconds)
        {
            string question = questionInput.text.Trim();
            BadgeDefinition selectedBadge = store.SelectedBadge();
            float effectiveProbability = DecisionEngine.EffectiveYesProbability(strength, mode, selectedBadge.yesProbability);
            int yesWins = 0, noWins = 0;
            char[] rounds = new char[seriesLength];
            for (int i = 0; i < seriesLength; i++)
            {
                bool roundYes = DecisionEngine.Decide(strength, mode, selectedBadge.yesProbability);
                rounds[i] = roundYes ? 'Y' : 'N';
                if (roundYes) yesWins++; else noWins++;
            }
            bool yes = yesWins > noWins;
            pendingHoldSeconds = heldSeconds;
            pending = new PendingDecision { Question = question, IsYes = yes, Strength = strength, StrengthSource = source, Mode = mode, TimestampUtc = DateTime.UtcNow, BadgeId = selectedBadge.id, YesProbabilityUsed = effectiveProbability, SeriesLength = seriesLength, YesWins = yesWins, NoWins = noWins, RoundResults = new string(rounds) };
            UserActionLog.Add("开始投掷；问题=" + (string.IsNullOrEmpty(question) ? "（未填写）" : question) + "；赛制=" + SeriesLabel(seriesLength) + "；徽章=" + selectedBadge.name + "；力度=" + Mathf.RoundToInt(strength * 100) + "%");
            StopAllCoroutines(); StartCoroutine(AnimateThrow(pending));
        }

        private IEnumerator AnimateThrow(PendingDecision value)
        {
            float holdFactor = Mathf.InverseLerp(0f, 3f, Mathf.Clamp(pendingHoldSeconds, 0f, 3f));
            // Even a short press gets a readable, weighty throw. Longer presses still
            // travel higher and extend the full motion, capped at three seconds.
            float duration = Mathf.Lerp(1.2f, 3f, Mathf.SmoothStep(0f, 1f, holdFactor));
            BadgeDefinition animationBadge = store.Badges.badges.Find(item => item.id == value.BadgeId) ?? store.SelectedBadge();
            PrepareThrowDiscs(value.SeriesLength, animationBadge);
            Canvas.ForceUpdateCanvases();
            throwDiscBasePositions.Clear();
            for (int i = 0; i < throwDiscs.Count; i++) throwDiscBasePositions.Add(throwDiscs[i].RectTransform.anchoredPosition);
            bool[] physicsStarted = new bool[throwDiscs.Count];
            bool[] resultCorrectionStarted = new bool[throwDiscs.Count];
            status.text = "投掷中…";
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = t / duration;
                for (int i = 0; i < throwDiscs.Count; i++)
                {
                    CoinRenderView item = throwDiscs[i];
                    float stagger = i * .018f;
                    float localP = Mathf.Clamp01((p - stagger) / Mathf.Max(.8f, 1f - stagger));
                    Vector2 travel;
                    float flipDegrees;
                    float tiltDegrees;
                    float rollDegrees;
                    float uniformScale = 1f;
                    bool applyScriptedPose = true;
                    bool roundYes = i < value.RoundResults.Length && value.RoundResults[i] == 'Y';

                    if (localP < .12f)
                    {
                        float anticipation = Mathf.SmoothStep(0f, 1f, localP / .12f);
                        travel = new Vector2(-42f, -34f) * anticipation;
                        flipDegrees = -22f * anticipation;
                        tiltDegrees = 8f * anticipation;
                        rollDegrees = -6f * anticipation;
                        uniformScale = 1f - .05f * anticipation;
                    }
                    else if (localP < .86f)
                    {
                        float flight = (localP - .12f) / .74f;
                        // Physically valid ballistic arc: y = 4H*t*(1-t), equivalent
                        // to an initial upward velocity followed by constant gravity.
                        float multiDiscHeightScale = value.SeriesLength == 1 ? 1f : value.SeriesLength == 3 ? .82f : .68f;
                        float height = 4f * Mathf.Lerp(190f, 280f, holdFactor) * multiDiscHeightScale * flight * (1f - flight);
                        float lane = value.SeriesLength <= 1 ? 1f : i - (value.SeriesLength - 1) * .5f;
                        float direction = Mathf.Abs(lane) > .01f ? Mathf.Sign(lane) : (i % 2 == 0 ? 1f : -1f);
                        float sideways = value.SeriesLength <= 1
                            ? Mathf.Lerp(-70f, 95f, Mathf.SmoothStep(0f, 1f, flight))
                            : Mathf.Lerp(-18f * lane, 28f * lane, Mathf.SmoothStep(0f, 1f, flight));
                        travel = new Vector2(sideways, height);
                        flipDegrees = tiltDegrees = rollDegrees = 0f;
                        if (!physicsStarted[i])
                        {
                            float spinFactor = Mathf.Clamp01(Mathf.Max(holdFactor, value.Strength));
                            float spin = Mathf.Lerp(7f, 32f, spinFactor);
                            item.BeginPhysicsSpin(new Vector3(spin, Mathf.Lerp(.7f, 2.8f, spinFactor) * direction, Mathf.Lerp(.4f, 1.8f, spinFactor) * direction));
                            physicsStarted[i] = true;
                        }
                        applyScriptedPose = false;
                        uniformScale = 1f + Mathf.Sin(flight * Mathf.PI) * .08f;
                    }
                    else
                    {
                        float settle = (localP - .86f) / .14f;
                        float damping = 1f - settle;
                        float lane = value.SeriesLength <= 1 ? 1f : i - (value.SeriesLength - 1) * .5f;
                        float landingX = value.SeriesLength <= 1 ? 95f : 28f * lane;
                        travel = new Vector2(Mathf.Lerp(landingX, 0f, Mathf.SmoothStep(0f, 1f, settle)), Mathf.Abs(Mathf.Sin(settle * Mathf.PI * 2f)) * 30f * damping);
                        flipDegrees = tiltDegrees = rollDegrees = 0f;
                        if (!resultCorrectionStarted[i])
                        {
                            item.BeginResultCorrection();
                            resultCorrectionStarted[i] = true;
                        }
                        item.CorrectToResult(roundYes, settle);
                        applyScriptedPose = false;
                        float squash = Mathf.Sin(settle * Mathf.PI) * damping;
                        uniformScale = 1f + .04f * squash;
                    }

                    if (applyScriptedPose) item.SetPose(flipDegrees, tiltDegrees, rollDegrees);
                    item.RectTransform.anchoredPosition = throwDiscBasePositions[i] + travel;
                    item.RectTransform.localScale = Vector3.one * uniformScale;
                }
                yield return null;
            }
            for (int i = 0; i < throwDiscs.Count; i++)
            {
                CoinRenderView item = throwDiscs[i]; item.RectTransform.anchoredPosition = throwDiscBasePositions[i]; item.RectTransform.localScale = Vector3.one;
                bool roundYes = i < value.RoundResults.Length && value.RoundResults[i] == 'Y';
                item.SetPose(roundYes ? 0f : 180f, 0f, 0f);
                throwDiscLabels[i].text = roundYes ? "YES" : "NO";
                throwDiscLabels[i].color = roundYes ? Yes : No;
            }
            status.text = (value.IsYes ? "YES" : "NO") + "  ·  " + SeriesScore(value) + "  ·  力度 " + Mathf.RoundToInt(value.Strength * 100) + "%\n尚未保存";
            UserActionLog.Add("投掷完成；结果=" + (value.IsYes ? "YES" : "NO"));
            RefreshHistory();
            savePromptNote.text = string.Empty;
            savePromptTitle.text = (value.IsYes ? "YES" : "NO") + " · " + SeriesScore(value) + "\n是否保存本次结果？";
            PopulateResultFaces(savePromptFaces, value, animationBadge);
            OpenModal(savePromptPanel);
        }

        private void SaveCurrent()
        {
            if (pending == null) return;
            DecisionRecord record = store.SaveExplicit(pending, savePromptNote.text.Trim());
            pending.Note = record.note;
            pending.SavedRecordId = record.id;
            UserActionLog.Add("明确保存本次记录；结果=" + (pending.IsYes ? "YES" : "NO"));
            pending = null; CloseModal(savePromptPanel); ResetHomeVisuals();
            status.text = "本次记录已永久保存。"; RefreshHistory();
        }

        private void DiscardCurrent()
        {
            if (pending != null) UserActionLog.Add("不保存并删除本次结果；结果=" + (pending.IsYes ? "YES" : "NO"));
            pending = null;
            CloseModal(savePromptPanel);
            ResetHomeVisuals();
            status.text = "本次结果已删除。";
            RefreshHistory();
        }

        private void ToggleMode()
        {
            mode = mode == DecisionMode.Fair5050 ? DecisionMode.StrengthInfluences : DecisionMode.Fair5050;
            modeText.text = ModeLabel(mode);
            UserActionLog.Add("切换随机模式：" + ModeLabel(mode));
        }

        private void SelectSeries(int value)
        {
            seriesLength = value;
            seriesText.text = "赛制：" + SeriesLabel(seriesLength) + "  ›";
            CloseModal(seriesPanel);
            UserActionLog.Add("切换赛制：" + SeriesLabel(seriesLength));
        }

        private void CreateBadge()
        {
            createBadgeNameInput.text = string.Empty;
            OpenModal(createBadgePanel);
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
            CloseModal(createBadgePanel);
            RefreshBadges();
        }

        private void RefreshBadges()
        {
            if (badgeList == null) return; Clear(badgeList);
            UpdateSelectedBadgeText();
            BadgeDefinition selected = store.SelectedBadge();
            foreach (BadgeDefinition badge in store.Badges.badges)
                AddBadgeCard(badge, badge.id == selected.id);
            badgeStatus.text = "共 " + store.Badges.badges.Count + " 个徽章 · 按住左上角 ☰ 上下拖动排序";
            StartCoroutine(RefreshBadgeListLayout());
        }

        private void AddBadgeCard(BadgeDefinition badge, bool current)
        {
            var card = VerticalCard("BadgeCard", badgeList, 440);
            Transform header = Horizontal("BadgeHeader", card, 46);
            HorizontalLayoutGroup headerLayout = header.GetComponent<HorizontalLayoutGroup>(); headerLayout.childForceExpandWidth = false;
            Text handle = Label("☰", header, 35, TextAnchor.MiddleCenter, Accent); SetWidth(handle.rectTransform, 52);
            Text name = Label((current ? "使用中 · " : "") + badge.name, header, 31, TextAnchor.MiddleLeft, current ? Accent : PrimaryText);
            LayoutElement nameLayout = name.gameObject.AddComponent<LayoutElement>(); nameLayout.flexibleWidth = 1f;
            var previews = Horizontal("FacePreviews", card, 220);
            AddFacePreview(previews, badge, true, !badge.builtIn);
            AddFacePreview(previews, badge, false, !badge.builtIn);
            var actions = Horizontal("BadgeActions", card, 62);
            Button use = Button("Use", actions, current ? "当前使用中" : "设为当前徽章", () => SelectBadgeForUse(badge), current ? Panel : Accent, 60); use.interactable = !current;
            Button("OpenDetail", actions, "徽章设置  ›", () => OpenBadgeDetail(badge), Panel, 60);
            GetBadgeStats(badge.id, out int total, out int yesCount, out int noCount);
            float yesPercent = total == 0 ? 0f : yesCount * 100f / total;
            Text stats = Label("YES " + Mathf.RoundToInt(badge.yesProbability * 100) + "%  ·  使用 " + total + " 次  ·  YES " + yesCount + "（" + yesPercent.ToString("0.#") + "%）  ·  NO " + noCount + "（" + (total == 0 ? 0f : 100f - yesPercent).ToString("0.#") + "%）", card, 19, TextAnchor.MiddleCenter, SecondaryText); SetHeight(stats.rectTransform, 40); stats.horizontalOverflow = HorizontalWrapMode.Overflow;
            BadgeReorderDragHandler drag = handle.gameObject.AddComponent<BadgeReorderDragHandler>();
            drag.Bind((RectTransform)card, badgeList, targetIndex => ReorderBadge(badge, targetIndex));
        }

        private void ReorderBadge(BadgeDefinition badge, int targetIndex)
        {
            store.MoveBadgeToIndex(badge.id, targetIndex);
            UserActionLog.Add("拖动调整徽章顺序：" + badge.name + " → " + (targetIndex + 1));
            badgeStatus.text = "已将“" + badge.name + "”移动到第 " + (targetIndex + 1) + " 位";
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
            RefreshHomeFaces();
            UpdateSelectedBadgeText();
            badgeStatus.text = "已切换到“" + badge.name + "”。";
            UserActionLog.Add("切换当前徽章：" + badge.name);
            RefreshBadges();
        }

        private void UpdateSelectedBadgeText()
        {
            if (selectedBadgeText == null) return;
            BadgeDefinition badge = store.SelectedBadge();
            selectedBadgeText.text = "使用中 · " + badge.name;
        }

        private void OpenBadgeDetail(BadgeDefinition badge)
        {
            detailBadge = badge;
            badgeDetailTitle.text = "徽章设置 · " + badge.name;
            badgeDetailNameInput.text = badge.name;
            badgeDetailNameInput.interactable = !badge.builtIn;
            badgeProbabilitySlider.value = badge.yesProbability;
            badgeProbabilityText.text = "YES 概率  " + Mathf.RoundToInt(badge.yesProbability * 100) + "%";
            badgeProbabilityInput.text = Mathf.RoundToInt(badge.yesProbability * 100).ToString();
            badgeDetailDeleteButton.interactable = !badge.builtIn;
            RefreshBadgeDetailFaces();
            OpenModal(badgeDetailPanel);
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
            ApplyProbabilityInput(badgeProbabilityInput.text);
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

        private void OnProbabilitySliderChanged(float value)
        {
            int percent = Mathf.RoundToInt(value * 100f);
            if (badgeProbabilityText != null) badgeProbabilityText.text = "YES 概率  " + percent + "%";
            if (badgeProbabilityInput != null && !badgeProbabilityInput.isFocused) badgeProbabilityInput.text = percent.ToString();
        }

        private void ApplyProbabilityInput(string value)
        {
            if (!int.TryParse(value, out int percent)) percent = Mathf.RoundToInt(badgeProbabilitySlider.value * 100f);
            percent = Mathf.Clamp(percent, 0, 100); badgeProbabilityInput.text = percent.ToString(); badgeProbabilitySlider.value = percent / 100f;
        }

        private void DeleteDetailBadge()
        {
            if (detailBadge == null || detailBadge.builtIn) return;
            UserActionLog.Add("删除徽章：" + detailBadge.name);
            store.DeleteBadge(detailBadge.id);
            if (imageTarget == detailBadge) imageTarget = null;
            detailBadge = null; CloseModal(badgeDetailPanel); UpdateSelectedBadgeText(); RefreshBadges();
        }

        private void PickBadgeImage(BadgeDefinition badge, bool yesFace) { imageTarget = badge; imageTargetIsYes = yesFace; badgeStatus.text = "正在为“" + badge.name + "”选择 " + (yesFace ? "YES" : "NO") + " 面图片…"; UserActionLog.Add("选择徽章图片：" + badge.name + " / " + (yesFace ? "YES" : "NO")); files.PickImage(); }

        private void ApplyPickedImage(string path)
        {
            try
            {
                if (imageTarget == null) throw new InvalidOperationException("没有正在编辑的徽章。");
                cropSourcePath = path;
                Sprite sprite = LoadSprite(path);
                if (sprite == null) throw new InvalidDataException("无法读取所选图片。");
                cropPreview.sprite = sprite;
                OpenModal(cropPanel);
                Canvas.ForceUpdateCanvases();
                cropGesture.Configure(cropPreview.rectTransform, cropGesture.Viewport, sprite.texture.width, sprite.texture.height);
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
            if (visible == 0) CardText(historyList, "当前筛选条件下暂无已保存记录。只有在结果弹窗中点击保存才会显示在这里。");
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
                InputField note = NoteInput("输入或修改备注", details, 72); note.text = record.note ?? string.Empty;
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
            BadgeDefinition badge = store.Badges.badges.Find(item => item.id == decision.BadgeId) ?? store.SelectedBadge();
            PopulateResultFaces(savePromptFaces, decision, badge);
            OpenModal(savePromptPanel);
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

        private void ConfirmCrop()
        {
            try
            {
                Vector2 offset = cropGesture.NormalizedOffset;
                store.CopyBadgeImage(imageTarget, imageTargetIsYes, cropSourcePath, cropGesture.Zoom, -offset.x, -offset.y);
                InvalidateSprite(imageTargetIsYes ? imageTarget.yesImagePath : imageTarget.noImagePath);
                CloseModal(cropPanel);
                badgeStatus.text = "已将“" + imageTarget.name + "”的 " + (imageTargetIsYes ? "YES" : "NO") + " 面裁切为 512×512 圆形图片。";
                UserActionLog.Add("裁切并保存徽章图片：" + imageTarget.name + " / " + (imageTargetIsYes ? "YES" : "NO"));
                RefreshBadges(); RefreshHomeFaces();
                if (detailBadge == imageTarget) RefreshBadgeDetailFaces();
            }
            catch (Exception exception) { badgeStatus.text = "图片裁切失败：" + exception.Message; UserActionLog.Add("图片裁切失败：" + exception.Message); }
        }

        private void RefreshHomeFaces()
        {
            if (homeFaces == null) return;
            Clear(homeFaces);
            BadgeDefinition badge = store.SelectedBadge();
            AddFacePreview(homeFaces, badge, true, false);
            AddFacePreview(homeFaces, badge, false, false);
        }

        private void ResetHomeVisuals()
        {
            if (throwStage != null) throwStage.gameObject.SetActive(false);
            if (homeFaces != null) homeFaces.gameObject.SetActive(true);
            RefreshHomeFaces();
        }

        private void PrepareThrowDiscs(int count, BadgeDefinition badge)
        {
            Clear(throwStage); throwDiscs.Clear(); throwDiscLabels.Clear(); throwDiscBasePositions.Clear();
            homeFaces.gameObject.SetActive(false); throwStage.gameObject.SetActive(true);
            float imageSize = count == 1 ? 390f : count == 3 ? 230f : 138f;
            for (int i = 0; i < count; i++)
            {
                Transform cell = VerticalContainer("ThrowCell", throwStage, true);
                VerticalLayoutGroup cellLayout = cell.GetComponent<VerticalLayoutGroup>();
                cellLayout.childAlignment = TextAnchor.MiddleCenter; cellLayout.childForceExpandWidth = false;
                GameObject renderObject = new GameObject("ThrowCoin3D", typeof(RectTransform), typeof(RawImage), typeof(CoinRenderView));
                renderObject.transform.SetParent(cell, false);
                RectTransform renderRect = (RectTransform)renderObject.transform; SetHeight(renderRect, imageSize); SetWidth(renderRect, imageSize);
                RawImage rawImage = renderObject.GetComponent<RawImage>();
                CoinRenderView coin = renderObject.GetComponent<CoinRenderView>();
                int resolution = count == 5 ? 256 : 384;
                coin.Initialize(rawImage, FaceTexture(badge, true), FaceTexture(badge, false), Hex("687386"), 20 + i, resolution);
                Text label = Label("", cell, count == 5 ? 28 : 38, TextAnchor.MiddleCenter, SecondaryText); SetHeight(label.rectTransform, 50);
                throwDiscs.Add(coin); throwDiscLabels.Add(label);
            }
        }

        private void PopulateResultFaces(Transform parent, PendingDecision decision, BadgeDefinition badge)
        {
            if (parent == null) return; Clear(parent);
            string results = string.IsNullOrEmpty(decision.RoundResults) ? (decision.IsYes ? "Y" : "N") : decision.RoundResults;
            for (int i = 0; i < results.Length; i++) AddResultFace(parent, badge, results[i] == 'Y');
        }

        private void AddResultFace(Transform parent, BadgeDefinition badge, bool yesFace)
        {
            Transform container = VerticalContainer("ResultFace", parent, true);
            string path = yesFace ? badge.yesImagePath : badge.noImagePath;
            Sprite loaded = LoadSprite(path);
            Image image = Image("ResultImage", container, Color.white); image.sprite = loaded ?? DefaultFaceSprite(badge, yesFace); image.preserveAspect = true; SetFlexible(image.rectTransform);
            Text label = Label(yesFace ? "YES" : "NO", container, 30, TextAnchor.MiddleCenter, yesFace ? Yes : No); SetHeight(label.rectTransform, 46);
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
            Background = lightTheme ? Hex("EDF8FF") : Hex("0C1523");
            Panel = lightTheme ? new Color(.975f, .99f, 1f, .91f) : new Color(.09f, .14f, .22f, .94f);
            Accent = lightTheme ? Hex("27B7D6") : Hex("52D5E7");
            Yes = lightTheme ? Hex("20BFA9") : Hex("42D9BD");
            No = lightTheme ? Hex("F46F81") : Hex("FF8294");
            PrimaryText = lightTheme ? Hex("17334A") : Hex("F4FBFF");
            SecondaryText = lightTheme ? Hex("648096") : Hex("9CB5C7");
        }

        private void PreviewImport(string json)
        {
            try
            {
                pendingImport = store.ParseImport(json);
                int yesCount = pendingImport.records.FindAll(r => r.result == "YES").Count;
                importPreview.text = "格式版本：" + pendingImport.version + "\n记录数：" + pendingImport.records.Count + "\nYES：" + yesCount + "  ·  NO：" + (pendingImport.records.Count - yesCount) + "\n\n请选择合并或替换。在你确认前不会修改任何数据。";
                UserActionLog.Add("导入预览成功；记录数=" + pendingImport.records.Count);
                OpenModal(importPanel);
            }
            catch (Exception exception) { importPreview.text = "导入被拒绝：\n" + exception.Message; UserActionLog.Add("导入失败：" + exception.Message); pendingImport = null; OpenModal(importPanel); }
        }

        private void ApplyImport(bool replace)
        {
            if (pendingImport == null) { CloseModal(importPanel); return; }
            int count = pendingImport.records.Count; store.ApplyImport(pendingImport, replace); UserActionLog.Add((replace ? "替换" : "合并") + "导入历史；记录数=" + count); pendingImport = null; CloseModal(importPanel); RefreshHistory();
        }

        private void AddFacePreview(Transform parent, BadgeDefinition badge, bool yesFace, bool clickable)
        {
            string path = yesFace ? badge.yesImagePath : badge.noImagePath;
            Sprite sprite = LoadSprite(path);
            Transform container = VerticalContainer(yesFace ? "YesFace" : "NoFace", parent, true);
            Image preview = Image(yesFace ? "YesPreview" : "NoPreview", container, Color.white);
            preview.sprite = sprite ?? DefaultFaceSprite(badge, yesFace); preview.preserveAspect = true;
            SetFlexible(preview.rectTransform);
            Text face = Label(yesFace ? "YES" : "NO", container, 34, TextAnchor.MiddleCenter, yesFace ? Yes : No); SetHeight(face.rectTransform, 44);
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

        private Texture FaceTexture(BadgeDefinition badge, bool yesFace)
        {
            string path = yesFace ? badge.yesImagePath : badge.noImagePath;
            Sprite loaded = LoadSprite(path);
            if (loaded != null) return loaded.texture;
            Texture2D presetTexture = DefaultFaceTexture(badge, yesFace);
            if (presetTexture != null) return presetTexture;
            if (yesFace && defaultYesTexture == null) defaultYesTexture = CreateDefaultFaceTexture(true);
            if (!yesFace && defaultNoTexture == null) defaultNoTexture = CreateDefaultFaceTexture(false);
            return yesFace ? defaultYesTexture : defaultNoTexture;
        }

        private Texture2D DefaultFaceTexture(BadgeDefinition badge, bool yesFace)
        {
            return yesFace ? defaultYesTexture : defaultNoTexture;
        }

        private Sprite DefaultFaceSprite(BadgeDefinition badge, bool yesFace)
        {
            Texture2D texture = yesFace ? defaultYesTexture : defaultNoTexture;
            if (texture == null) return circleSprite;
            Sprite cached = yesFace ? defaultYesSprite : defaultNoSprite;
            if (cached != null) return cached;
            Sprite created = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            created.name = "Star " + (yesFace ? "YES" : "NO") + " Symbol";
            if (yesFace) defaultYesSprite = created; else defaultNoSprite = created;
            return created;
        }

        private static Texture2D CreateDefaultFaceTexture(bool yesFace)
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = yesFace ? "Generated YES Face" : "Generated NO Face",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32 fill = yesFace ? Yes : No;
            var pixels = new Color32[size * size];
            float center = (size - 1) * .5f, radius = size * .492f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (distance <= radius - 2f) pixels[y * size + x] = fill;
                else if (distance < radius + 1f)
                {
                    Color edge = fill; edge.a = Mathf.Clamp01((radius + 1f - distance) / 3f);
                    pixels[y * size + x] = edge;
                }
            }

            string word = yesFace ? "YES" : "NO";
            const int glyphWidth = 5, glyphHeight = 7, pixelScale = 22, glyphGap = 2;
            int totalUnits = word.Length * glyphWidth + (word.Length - 1) * glyphGap;
            int startX = (size - totalUnits * pixelScale) / 2;
            int startY = (size - glyphHeight * pixelScale) / 2;
            for (int character = 0; character < word.Length; character++)
            {
                string[] glyph = FaceGlyph(word[character]);
                int glyphX = startX + character * (glyphWidth + glyphGap) * pixelScale;
                for (int row = 0; row < glyphHeight; row++)
                for (int column = 0; column < glyphWidth; column++)
                {
                    if (glyph[row][column] != '1') continue;
                    int baseX = glyphX + column * pixelScale;
                    int baseY = startY + (glyphHeight - 1 - row) * pixelScale;
                    for (int py = 2; py < pixelScale - 2; py++)
                    for (int px = 2; px < pixelScale - 2; px++)
                        pixels[(baseY + py) * size + baseX + px] = new Color32(255, 255, 255, 255);
                }
            }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }

        private static string[] FaceGlyph(char value)
        {
            switch (value)
            {
                case 'Y': return new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" };
                case 'E': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" };
                case 'S': return new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" };
                case 'N': return new[] { "10001", "11001", "11001", "10101", "10011", "10011", "10001" };
                default: return new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" };
            }
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
            card.sprite = softRectSprite; card.type = UnityEngine.UI.Image.Type.Sliced;
            Outline edge = card.gameObject.AddComponent<Outline>(); edge.effectColor = lightTheme ? new Color(.25f, .71f, .85f, .20f) : new Color(.35f, .82f, .9f, .18f); edge.effectDistance = new Vector2(1.5f, -1.5f);
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
            root.sprite = softRectSprite; root.type = UnityEngine.UI.Image.Type.Sliced;
            Outline edge = root.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(Accent.r, Accent.g, Accent.b, .22f); edge.effectDistance = new Vector2(1.5f, -1.5f);
            var field = root.gameObject.AddComponent<InputField>(); field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            Text value = Label("", root.transform, 30, TextAnchor.MiddleLeft, PrimaryText); Stretch(value.rectTransform, 24, 10, 24, 10);
            Text hint = Label(placeholder, root.transform, 28, TextAnchor.MiddleLeft, Hex("667085")); Stretch(hint.rectTransform, 24, 10, 24, 10);
            field.textComponent = value; field.placeholder = hint; field.targetGraphic = root; return field;
        }

        private static InputField NoteInput(string placeholder, Transform parent, float height)
        {
            InputField field = Input(placeholder, parent, height, false);
            Image background = field.GetComponent<Image>(); background.color = lightTheme ? Hex("EEF6FF") : Hex("26364A");
            Outline outline = field.gameObject.AddComponent<Outline>(); outline.effectColor = Accent; outline.effectDistance = new Vector2(2, -2);
            return field;
        }

        private static Button Button(string name, Transform parent, string value, UnityEngine.Events.UnityAction action, Color color, float height)
        {
            var image = Image(name, parent, color); SetHeight(image.rectTransform, height);
            image.sprite = softRectSprite; image.type = UnityEngine.UI.Image.Type.Sliced;
            Outline edge = image.gameObject.AddComponent<Outline>(); edge.effectColor = Approximately(color, Panel) ? new Color(Accent.r, Accent.g, Accent.b, .24f) : new Color(1f, 1f, 1f, .28f); edge.effectDistance = new Vector2(1.5f, -1.5f);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f); colors.pressedColor = new Color(.88f, .94f, .98f, 1f); colors.fadeDuration = .10f; button.colors = colors;
            Text label = Label(value, image.transform, 26, TextAnchor.MiddleCenter, Approximately(color, Panel) ? PrimaryText : Color.white); Stretch(label.rectTransform, 8, 4, 8, 4); return button;
        }

        private Slider SliderControl(string name, Transform parent, float min, float max, UnityEngine.Events.UnityAction<float> changed)
        {
            Image root = Image(name, parent, lightTheme ? Hex("EEF2F6") : Hex("243244")); SetHeight(root.rectTransform, 84);
            RectTransform track = Rect("Track", root.transform); track.anchorMin = new Vector2(.08f, .5f); track.anchorMax = new Vector2(.92f, .5f); track.sizeDelta = new Vector2(0, 16);
            Image trackImage = track.gameObject.AddComponent<Image>(); trackImage.color = lightTheme ? Hex("CAD5E2") : Hex("475467");
            RectTransform fillArea = Rect("FillArea", root.transform); fillArea.anchorMin = new Vector2(.08f, .5f); fillArea.anchorMax = new Vector2(.92f, .5f); fillArea.sizeDelta = new Vector2(0, 16);
            Image fill = Image("Fill", fillArea, Accent); Stretch(fill.rectTransform);
            RectTransform handleArea = Rect("HandleArea", root.transform); handleArea.anchorMin = new Vector2(.08f, 0); handleArea.anchorMax = new Vector2(.92f, 1); handleArea.offsetMin = Vector2.zero; handleArea.offsetMax = Vector2.zero;
            Image handle = Image("Handle", handleArea, Color.white); handle.sprite = circleSprite; handle.preserveAspect = true; handle.rectTransform.sizeDelta = new Vector2(52, 52);
            Outline outline = handle.gameObject.AddComponent<Outline>(); outline.effectColor = Accent; outline.effectDistance = new Vector2(3, -3);
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
            root.sprite = softRectSprite; root.type = UnityEngine.UI.Image.Type.Sliced;
            Outline edge = root.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(Accent.r, Accent.g, Accent.b, .20f); edge.effectDistance = new Vector2(1.5f, -1.5f);
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
        private static void SetHeight(RectTransform rect, float height) { var e = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.preferredHeight = height; e.minHeight = height; e.flexibleHeight = 0; }
        private static void SetWidth(RectTransform rect, float width) { var e = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.preferredWidth = width; e.minWidth = width; e.flexibleWidth = 0; }
        private static void SetFlexible(RectTransform rect) { var e = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.flexibleHeight = 1; }
        private static void Clear(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) { GameObject child = parent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); } }
        private static string Present(string path) { return string.IsNullOrEmpty(path) || !File.Exists(path) ? "默认文字面" : Path.GetFileName(path) + "（应用内部副本）"; }
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color result); return result; }
        private static bool Approximately(Color a, Color b) { return Mathf.Abs(a.r - b.r) < .001f && Mathf.Abs(a.g - b.g) < .001f && Mathf.Abs(a.b - b.b) < .001f && Mathf.Abs(a.a - b.a) < .001f; }

        private Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (spriteCache.TryGetValue(path, out Sprite cached) && cached != null) return cached;
            try
            {
                var texture = new Texture2D(2, 2);
                if (!texture.LoadImage(File.ReadAllBytes(path))) { Destroy(texture); return null; }
                Sprite loaded = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
                spriteCache[path] = loaded; return loaded;
            }
            catch { return null; }
        }

        private void InvalidateSprite(string path)
        {
            if (string.IsNullOrEmpty(path) || !spriteCache.TryGetValue(path, out Sprite sprite)) return;
            spriteCache.Remove(path); if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 256; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false); Color[] pixels = new Color[size * size]; Vector2 center = Vector2.one * (size - 1) * .5f; float radius = size * .49f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { float distance = Vector2.Distance(new Vector2(x, y), center); float alpha = Mathf.Clamp01(radius - distance); pixels[y * size + x] = new Color(1, 1, 1, alpha); }
            texture.SetPixels(pixels); texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
        }

        private static Sprite CreateRoundedRectSprite()
        {
            const int size = 64;
            const float radius = 15f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Decision Disc Soft Rectangle" };
            var pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
            Vector2 half = new Vector2(size * .5f - radius, size * .5f - radius);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y)) - half;
                float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                float signedDistance = outside + inside - radius;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(.75f - signedDistance));
            }
            texture.SetPixels(pixels); texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 256; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false); Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * (size - 1) * .5f; float outer = size * .49f, inner = size * .465f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(Mathf.Min(outer - distance, distance - inner));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
        }
    }
}
