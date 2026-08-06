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
        private sealed class UiPanelOpacityEntry
        {
            public Image image;
            public Color baseColor;
        }
        private static Color Background = Hex("F4FAFF");
        private static Color Panel = Hex("FFFFFF");
        private static Color Accent = Hex("4FB7E8");
        private static Color Yes = Hex("43C9B8");
        private static Color No = Hex("F07F9B");
        private static Color PrimaryText = Hex("283C59");
        private static Color SecondaryText = Hex("71849C");
        private static Color ButtonTextColor = Hex("283C59");
        private static Color SurfaceShadow = new Color(.18f, .42f, .62f, .14f);
        private static Font font;
        private static Sprite softRectSprite;

        private DecisionStore store;
        private AndroidFileBridge files;
        private readonly List<GameObject> pages = new List<GameObject>();
        private InputField questionInput;
        private Transform homeFaces;
        private BadgeCollisionMotion homeCollisionMotion;
        private Transform throwStage;
        private readonly List<CoinRenderView> throwDiscs = new List<CoinRenderView>();
        private readonly List<Text> throwDiscLabels = new List<Text>();
        private readonly List<Vector2> throwDiscBasePositions = new List<Vector2>();
        private Text status;
        private ChargeThrowButton chargeButton;
        private Text selectedBadgeText;
        private Text seriesText;
        private Transform badgeList;
        private Transform historyList;
        private Text badgeStatus;
        private Text backgroundStatus;
        private Text backgroundOpacityText;
        private Slider backgroundOpacitySlider;
        private Text uiPanelOpacityText;
        private Slider uiPanelOpacitySlider;
        private Text buttonTextColorStatus;
        private InputField buttonTextColorInput;
        private RawImage themeBackgroundImage;
        private Image themeBackgroundVeil;
        private Image customBackgroundImage;
        private Image customBackgroundVeil;
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
        private Text cropTitle;
        private Text cropHint;
        private Image cropViewport;
        private Image cropRing;
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
        private bool pickingBackground;
        private bool croppingBackground;
        private PendingDecision pending;
        private DecisionMode mode = DecisionMode.StrengthInfluences;
        private Sprite circleSprite;
        private Texture2D defaultYesTexture;
        private Texture2D defaultNoTexture;
        private Sprite defaultYesSprite;
        private Sprite defaultNoSprite;
        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly List<PendingDecision> sessionDecisions = new List<PendingDecision>();
        private readonly List<Button> navigationButtons = new List<Button>();
        private static readonly List<UiPanelOpacityEntry> uiPanelOpacityTargets = new List<UiPanelOpacityEntry>();
        private float uiPanelOpacity = 0.88f;
        private Coroutine throwRoutine;

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
            defaultYesTexture = Resources.Load<Texture2D>("Theme/default-yes-symbol-v3");
            defaultNoTexture = Resources.Load<Texture2D>("Theme/default-no-symbol-v3");
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
            ButtonTextColor = ParseHexColor(store.Appearance.buttonTextColor, lightTheme ? Hex("283C59") : Hex("F5FAFF"));
            uiPanelOpacity = Mathf.Clamp01(store.Appearance.uiPanelOpacity);
            uiPanelOpacityTargets.Clear();
            themeBackgroundImage = null;
            themeBackgroundVeil = null;
            customBackgroundImage = null;
            customBackgroundVeil = null;
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
                    float opacity = Mathf.Clamp01(store.Appearance.backgroundOpacity);
                    GameObject artObject = new GameObject("ResonanceThemeArt", typeof(RectTransform), typeof(RawImage));
                    artObject.transform.SetParent(background.transform, false);
                    themeBackgroundImage = artObject.GetComponent<RawImage>(); themeBackgroundImage.texture = themeArt; themeBackgroundImage.color = new Color(1f, 1f, 1f, opacity); themeBackgroundImage.raycastTarget = false;
                    Stretch(themeBackgroundImage.rectTransform);
                    themeBackgroundVeil = Image("ReadabilityVeil", background.transform, new Color(.97f, .99f, 1f, .08f * opacity)); Stretch(themeBackgroundVeil.rectTransform); themeBackgroundVeil.raycastTarget = false;
                }
            }
            Sprite customBackground = LoadSprite(store.Appearance.backgroundImagePath);
            if (customBackground != null)
            {
                float opacity = Mathf.Clamp01(store.Appearance.backgroundOpacity);
                customBackgroundImage = Image("CustomBackground", background.transform, Color.white);
                customBackgroundImage.sprite = customBackground;
                // The saved crop is portrait (720x1280), while Android devices
                // commonly have a taller viewport once the status/navigation
                // areas are included.  Preserve the artwork ratio but envelope
                // the viewport so no strip of the default theme can show above
                // or below the user's background.
                customBackgroundImage.preserveAspect = false;
                customBackgroundImage.color = new Color(1f, 1f, 1f, opacity);
                customBackgroundImage.raycastTarget = false;
                Stretch(customBackgroundImage.rectTransform);
                AspectRatioFitter backgroundCover = customBackgroundImage.gameObject.AddComponent<AspectRatioFitter>();
                backgroundCover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                backgroundCover.aspectRatio = Mathf.Max(.01f, customBackground.texture.width / (float)Mathf.Max(1, customBackground.texture.height));
                customBackgroundVeil = Image("CustomBackgroundVeil", background.transform, new Color(.95f, .985f, 1f, .08f * opacity));
                customBackgroundVeil.raycastTarget = false;
                Stretch(customBackgroundVeil.rectTransform);
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
            ApplyUiPanelOpacity(uiPanelOpacity);
        }

        private GameObject BuildHome(Transform parent)
        {
            var page = Page("ThrowPage", parent);
            Text title = Label(InAppName(), page.transform, 46, TextAnchor.MiddleCenter, PrimaryText);
            SetHeight(title.rectTransform, 62);
            Text sub = Label("按住蓄力，3 秒达到满力，松开投掷", page.transform, 24, TextAnchor.MiddleCenter, SecondaryText); SetHeight(sub.rectTransform, 38);

            RectTransform visualStage = Rect("VisualStage", page.transform); SetHeight(visualStage, 520);
            Image stageSurface = Image("BadgeStageSurface", visualStage, lightTheme ? new Color(.97f, .995f, 1f, .72f) : Panel);
            RegisterPanelOpacity(stageSurface);
            stageSurface.sprite = softRectSprite; stageSurface.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(stageSurface.rectTransform, 4, 4, 4, 4); stageSurface.raycastTarget = false;
            Outline stageEdge = stageSurface.gameObject.AddComponent<Outline>(); stageEdge.effectColor = new Color(Accent.r, Accent.g, Accent.b, .28f); stageEdge.effectDistance = new Vector2(1f, -1f);
            Shadow stageShadow = stageSurface.gameObject.AddComponent<Shadow>(); stageShadow.effectColor = SurfaceShadow; stageShadow.effectDistance = new Vector2(0f, -7f);

            Image orbit = Image("CollisionOrbit", visualStage, new Color(Accent.r, Accent.g, Accent.b, lightTheme ? .10f : .18f));
            orbit.sprite = CreateRingSprite(); orbit.preserveAspect = true; orbit.raycastTarget = false;
            orbit.rectTransform.anchorMin = orbit.rectTransform.anchorMax = new Vector2(.5f, .5f);
            orbit.rectTransform.sizeDelta = new Vector2(430f, 430f); orbit.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            homeFaces = Rect("HomeFaces", visualStage); Stretch((RectTransform)homeFaces);
            homeCollisionMotion = homeFaces.gameObject.AddComponent<BadgeCollisionMotion>();
            throwStage = Rect("ThrowStage", visualStage); Stretch((RectTransform)throwStage);
            throwStage.gameObject.SetActive(false);
            RefreshHomeFaces();

            // An opaque row drawn after the face stage prevents the stage from visually
            // bleeding into the active-badge information below it.
            Image badgeSwitchSurface = Image("BadgeSwitch", page.transform, lightTheme ? new Color(1f, 1f, 1f, .96f) : Panel);
            RegisterPanelOpacity(badgeSwitchSurface);
            badgeSwitchSurface.sprite = softRectSprite; badgeSwitchSurface.type = UnityEngine.UI.Image.Type.Sliced;
            Outline badgeSwitchEdge = badgeSwitchSurface.gameObject.AddComponent<Outline>(); badgeSwitchEdge.effectColor = new Color(Accent.r, Accent.g, Accent.b, .20f); badgeSwitchEdge.effectDistance = new Vector2(1f, -1f);
            RectTransform badgeSwitch = badgeSwitchSurface.rectTransform; SetHeight(badgeSwitch, 78);
            HorizontalLayoutGroup badgeSwitchLayout = badgeSwitchSurface.gameObject.AddComponent<HorizontalLayoutGroup>();
            badgeSwitchLayout.padding = new RectOffset(18, 12, 8, 8);
            badgeSwitchLayout.spacing = 12;
            badgeSwitchLayout.childAlignment = TextAnchor.MiddleLeft;
            badgeSwitchLayout.childControlWidth = true;
            badgeSwitchLayout.childControlHeight = true;
            badgeSwitchLayout.childForceExpandWidth = false;
            badgeSwitchLayout.childForceExpandHeight = true;
            selectedBadgeText = Label("当前徽章：" + store.SelectedBadge().name, badgeSwitch, 28, TextAnchor.MiddleLeft, PrimaryText);
            selectedBadgeText.fontStyle = FontStyle.Normal;
            LayoutElement selectedBadgeLayout = selectedBadgeText.gameObject.AddComponent<LayoutElement>();
            selectedBadgeLayout.flexibleWidth = 1f;
            selectedBadgeLayout.minWidth = 0f;
            Button switchBadge = Button("SwitchBadge", badgeSwitch, "更换", () => ShowPage(1), Panel, 50);
            SetWidth(switchBadge.GetComponent<RectTransform>(), 150);

            questionInput = Input("可选：输入本次要决定的问题", page.transform, 140, false);
            var seriesButton = Button("Series", page.transform, "投掷数量：1 枚  ›", () => OpenModal(seriesPanel), Panel, 140);
            seriesText = seriesButton.GetComponentInChildren<Text>();

            var chargeObject = new GameObject("Charge", typeof(RectTransform), typeof(Image), typeof(ChargeThrowButton));
            chargeObject.transform.SetParent(page.transform, false); SetHeight((RectTransform)chargeObject.transform, 140);
            Image chargeBackground = chargeObject.GetComponent<Image>();
            chargeBackground.color = lightTheme ? new Color(.93f, .985f, 1f, .88f) : Hex("243B53");
            chargeBackground.sprite = softRectSprite; chargeBackground.type = UnityEngine.UI.Image.Type.Sliced;
            Outline chargeEdge = chargeObject.AddComponent<Outline>(); chargeEdge.effectColor = new Color(Accent.r, Accent.g, Accent.b, .35f); chargeEdge.effectDistance = new Vector2(2f, -2f);
            var fill = Image("Fill", chargeObject.transform, Accent); Stretch(fill.rectTransform); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0;
            // The launch surface is intentionally saturated, so its instruction must
            // remain high contrast instead of inheriting the darker body-text colour.
            var chargeLabel = Label("按住蓄力，3 秒达到满力", chargeObject.transform, 40, TextAnchor.MiddleCenter, ButtonTextColor); Stretch(chargeLabel.rectTransform, 18, 0, 18, 0);
            chargeButton = chargeObject.GetComponent<ChargeThrowButton>(); chargeButton.Label = chargeLabel; chargeButton.Fill = fill; chargeButton.Released += Throw;

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
            Header(page.transform, "设置", "隐私说明、数据与问题排查");
            Transform content = ScrollContent("SettingsScroll", page.transform, 0);
            Text appearanceTitle = Label("外观与数据", content, 31, TextAnchor.MiddleLeft, Accent); SetHeight(appearanceTitle.rectTransform, 54);
            Button("Theme", content, lightTheme ? "切换为夜间主题" : "切换为日间主题", ToggleTheme, Panel, 82);
            Text appNameHint = Label("应用名称：决策勋章", content, 22, TextAnchor.MiddleLeft, SecondaryText); SetHeight(appNameHint.rectTransform, 44);
            buttonTextColorStatus = Label("按钮/蓄力文字颜色：" + store.Appearance.buttonTextColor, content, 22, TextAnchor.MiddleLeft, SecondaryText); SetHeight(buttonTextColorStatus.rectTransform, 44);
            Transform buttonTextColorControls = Horizontal("ButtonTextColorControls", content, 82);
            buttonTextColorInput = Input("输入 HEX，例如 #283C59", buttonTextColorControls, 76, false);
            buttonTextColorInput.text = store.Appearance.buttonTextColor;
            buttonTextColorInput.characterLimit = 7;
            SetWidth(buttonTextColorInput.GetComponent<RectTransform>(), 300);
            Button("ApplyButtonTextColor", buttonTextColorControls, "应用文字颜色", ApplyButtonTextColor, Panel, 76);
            Button("ResetButtonTextColor", buttonTextColorControls, "恢复默认", ResetButtonTextColor, Panel, 76);
            backgroundStatus = Label(string.IsNullOrEmpty(store.Appearance.backgroundImagePath) ? "背景：使用应用默认背景" : "背景：正在使用你上传的图片", content, 24, TextAnchor.MiddleLeft, SecondaryText); SetHeight(backgroundStatus.rectTransform, 48);
            Transform backgroundActions = Horizontal("BackgroundActions", content, 86);
            Button("UploadBackground", backgroundActions, "上传/更换背景图", PickBackgroundImage, Accent, 82);
            Button("ResetBackground", backgroundActions, "恢复默认背景", ResetBackgroundImage, Panel, 82);
            backgroundOpacityText = Label("背景不透明度：" + Mathf.RoundToInt(store.Appearance.backgroundOpacity * 100f) + "%", content, 24, TextAnchor.MiddleLeft, SecondaryText); SetHeight(backgroundOpacityText.rectTransform, 44);
            backgroundOpacitySlider = SliderControl("BackgroundOpacity", content, 0f, 1f, OnBackgroundOpacityChanged);
            backgroundOpacitySlider.SetValueWithoutNotify(store.Appearance.backgroundOpacity);
            uiPanelOpacityText = Label("界面底板不透明度：" + Mathf.RoundToInt(store.Appearance.uiPanelOpacity * 100f) + "%", content, 24, TextAnchor.MiddleLeft, SecondaryText); SetHeight(uiPanelOpacityText.rectTransform, 44);
            uiPanelOpacitySlider = SliderControl("UiPanelOpacity", content, 0f, 1f, OnUiPanelOpacityChanged);
            uiPanelOpacitySlider.SetValueWithoutNotify(store.Appearance.uiPanelOpacity);
            Text uiPanelOpacityHint = Label("仅影响卡片、输入框和按钮的底板，不影响背景图片、文字和徽章。", content, 21, TextAnchor.MiddleLeft, SecondaryText); SetHeight(uiPanelOpacityHint.rectTransform, 50);
            var historyActions = Horizontal("HistoryDataActions", content, 86);
            Button("Export", historyActions, "导出历史 JSON", () => { UserActionLog.Add("点击导出历史 JSON"); files.ExportJson(store.CreateExportJson()); }, Panel, 82);
            Button("Import", historyActions, "导入历史 JSON", files.PickJson, Panel, 82);
            Text diagnosticsTitle = Label("问题排查", content, 31, TextAnchor.MiddleLeft, Accent); SetHeight(diagnosticsTitle.rectTransform, 54);
            Text diagnosticsHint = Label("遇到问题时，先点“刷新日志预览”；需要发给开发者时，再点“导出操作日志”。", content, 24, TextAnchor.MiddleLeft, SecondaryText); SetHeight(diagnosticsHint.rectTransform, 82);
            Button("RefreshLog", content, "刷新操作日志预览", RefreshLogPreview, Panel, 82);
            Button("ExportLog", content, "导出操作日志", ExportOperationLog, Accent, 88);
            logPreview = Label("暂无操作日志。", content, 21, TextAnchor.UpperLeft, PrimaryText); SetHeight(logPreview.rectTransform, 260);
            CardText(content, "隐私\n投掷结果只在确认弹窗中临时存在；选择不保存会立即删除。只有明确保存或导出才会写入文件。");
            CardText(content, "概率规则\n每个徽章可设置 0%–100% YES 概率；0% 必定 NO，100% 必定 YES。蓄力只影响动画，不影响随机结果。");
            CardText(content, "本地存储\n历史记录和徽章图片副本保存在 Application.persistentDataPath。");
            CardText(content, "版本\nYesNoFilp " + Application.version + " · 历史 JSON 格式 v1");
            return page;
        }

        private void BuildNavigation(Transform safe)
        {
            Image navSurface = Image("Navigation", safe, lightTheme ? new Color(1f, 1f, 1f, .96f) : Panel);
            RegisterPanelOpacity(navSurface);
            navSurface.sprite = softRectSprite; navSurface.type = UnityEngine.UI.Image.Type.Sliced;
            Shadow navShadow = navSurface.gameObject.AddComponent<Shadow>(); navShadow.effectColor = SurfaceShadow; navShadow.effectDistance = new Vector2(0f, 5f);
            var nav = navSurface.transform;
            SetHeight(navSurface.rectTransform, 94);
            var rt = navSurface.rectTransform; rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, 94);
            var navLayout = navSurface.gameObject.AddComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 8; navLayout.padding = new RectOffset(14, 14, 10, 10); navLayout.childControlHeight = true; navLayout.childControlWidth = true; navLayout.childForceExpandWidth = true;
            string[] names = { "◆  投掷", "◉  徽章", "▤  记录", "⚙  设置" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Button navigation = Button("Nav" + i, nav, names[i], () => ShowPage(index), Panel, 94);
                navigationButtons.Add(navigation);
            }
        }

        private void BuildModalBackdrop(Transform parent)
        {
            Image backdrop = Image("ModalBackdrop", parent, new Color(.05f, .16f, .28f, .34f));
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
            StartCoroutine(AnimateModalOpen(panel));
        }

        private IEnumerator AnimateModalOpen(GameObject panel)
        {
            CanvasGroup panelGroup = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
            CanvasGroup backdropGroup = modalBackdrop == null ? null : (modalBackdrop.GetComponent<CanvasGroup>() ?? modalBackdrop.AddComponent<CanvasGroup>());
            panelGroup.alpha = 0f;
            panel.transform.localScale = Vector3.one * .965f;
            if (backdropGroup != null) backdropGroup.alpha = 0f;
            const float duration = .2f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                panelGroup.alpha = p;
                panel.transform.localScale = Vector3.one * Mathf.Lerp(.965f, 1f, p);
                if (backdropGroup != null) backdropGroup.alpha = p;
                yield return null;
            }
            panelGroup.alpha = 1f;
            panel.transform.localScale = Vector3.one;
            if (backdropGroup != null) backdropGroup.alpha = 1f;
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

        private static Color ModalSurfaceColor()
        {
            return lightTheme ? new Color(.975f, .995f, 1f, .985f) : new Color(.10f, .17f, .27f, .985f);
        }

        private static void StyleModalSurface(Image surface)
        {
            if (surface == null) return;
            RegisterPanelOpacity(surface);
            surface.sprite = softRectSprite;
            surface.type = UnityEngine.UI.Image.Type.Sliced;
            Outline edge = surface.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(Accent.r, Accent.g, Accent.b, lightTheme ? .38f : .30f);
            edge.effectDistance = new Vector2(2f, -2f);
            Shadow shadow = surface.gameObject.AddComponent<Shadow>();
            shadow.effectColor = lightTheme ? new Color(.10f, .32f, .50f, .24f) : new Color(0f, 0f, 0f, .38f);
            shadow.effectDistance = new Vector2(0f, -12f);
        }

        private void BuildImportPanel(Transform parent)
        {
            Image importSurface = Image("ImportPreviewPanel", parent, ModalSurfaceColor());
            StyleModalSurface(importSurface); importPanel = importSurface.gameObject; Stretch((RectTransform)importPanel.transform, 70, 220, 70, 220);
            var layout = importPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(32, 32, 32, 32); layout.spacing = 24;
            Label("导入预览", importPanel.transform, 42, TextAnchor.MiddleCenter, PrimaryText);
            importPreview = Label("", importPanel.transform, 28, TextAnchor.UpperLeft, PrimaryText); SetFlexible(importPreview.rectTransform);
            Button("Merge", importPanel.transform, "与已保存记录合并", () => ApplyImport(false), Accent, 86);
            Button("Replace", importPanel.transform, "替换全部已保存记录", () => ApplyImport(true), No, 86);
            Button("Cancel", importPanel.transform, "取消", () => CloseModal(importPanel), Panel, 76);
            importPanel.SetActive(false);
        }

        private void BuildBadgeCreatePanel(Transform parent)
        {
            Image createSurface = Image("CreateBadgePanel", parent, ModalSurfaceColor());
            StyleModalSurface(createSurface); createBadgePanel = createSurface.gameObject;
            Stretch((RectTransform)createBadgePanel.transform, 90, 470, 90, 470);
            var layout = createBadgePanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(36, 36, 36, 36); layout.spacing = 28; layout.childForceExpandHeight = false;
            Text title = Label("创建新徽章", createBadgePanel.transform, 42, TextAnchor.MiddleCenter, PrimaryText); SetHeight(title.rectTransform, 76);
            Text hint = Label("先输入名称。创建后会立即出现在列表顶部，默认使用 YES/NO 文字面和 50% 概率。", createBadgePanel.transform, 26, TextAnchor.MiddleCenter, SecondaryText); SetHeight(hint.rectTransform, 110);
            createBadgeNameInput = Input("请输入徽章名称", createBadgePanel.transform, 100, false);
            Button("ConfirmCreate", createBadgePanel.transform, "创建徽章", ConfirmCreateBadge, Accent, 88);
            Button("CancelCreate", createBadgePanel.transform, "取消", () => CloseModal(createBadgePanel), Panel, 78);
            createBadgePanel.SetActive(false);
        }

        private void BuildBadgeDetailPanel(Transform parent)
        {
            Image detailSurface = Image("BadgeDetailPanel", parent, ModalSurfaceColor());
            StyleModalSurface(detailSurface); badgeDetailPanel = detailSurface.gameObject;
            Stretch((RectTransform)badgeDetailPanel.transform, 54, 160, 54, 130);
            var layout = badgeDetailPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(34, 34, 30, 30); layout.spacing = 18; layout.childForceExpandHeight = false;
            badgeDetailTitle = Label("徽章设置", badgeDetailPanel.transform, 42, TextAnchor.MiddleCenter, PrimaryText); SetHeight(badgeDetailTitle.rectTransform, 70);
            badgeDetailNameInput = Input("徽章名称", badgeDetailPanel.transform, 88, false);
            badgeDetailFaces = Horizontal("DetailFaces", badgeDetailPanel.transform, 260);
            Text imageHint = Label("点击 YES 或 NO 图片即可上传、替换并裁切", badgeDetailPanel.transform, 23, TextAnchor.MiddleCenter, SecondaryText); SetHeight(imageHint.rectTransform, 52);
            badgeProbabilityText = Label("YES 概率  50%", badgeDetailPanel.transform, 30, TextAnchor.MiddleCenter, PrimaryText); SetHeight(badgeProbabilityText.rectTransform, 52);
            Transform probabilityRow = Horizontal("ProbabilityControls", badgeDetailPanel.transform, 82);
            HorizontalLayoutGroup probabilityLayout = probabilityRow.GetComponent<HorizontalLayoutGroup>(); probabilityLayout.childForceExpandWidth = false;
            badgeProbabilitySlider = SliderControl("BadgeProbability", probabilityRow, 0f, 1f, OnProbabilitySliderChanged);
            LayoutElement sliderLayout = badgeProbabilitySlider.GetComponent<LayoutElement>(); sliderLayout.flexibleWidth = 1f;
            badgeProbabilityInput = Input("0–100", probabilityRow, 76, false); SetWidth(badgeProbabilityInput.GetComponent<RectTransform>(), 112);
            badgeProbabilityInput.contentType = InputField.ContentType.IntegerNumber;
            badgeProbabilityInput.onEndEdit.AddListener(ApplyProbabilityInput);
            Text explanation = Label("可设置 0%–100%。0% 必定 NO，100% 必定 YES；蓄力仅影响动画表现。", badgeDetailPanel.transform, 24, TextAnchor.MiddleCenter, SecondaryText); SetHeight(explanation.rectTransform, 86);
            Button("SaveDetail", badgeDetailPanel.transform, "保存徽章设置", SaveBadgeDetail, Accent, 84);
            badgeDetailDeleteButton = Button("DeleteDetail", badgeDetailPanel.transform, "删除此徽章", DeleteDetailBadge, No, 76);
            Button("CloseDetail", badgeDetailPanel.transform, "返回徽章列表", () => CloseModal(badgeDetailPanel), Panel, 76);
            badgeDetailPanel.SetActive(false);
        }

        private void BuildSavePromptPanel(Transform parent)
        {
            Image promptSurface = Image("SavePromptPanel", parent, ModalSurfaceColor());
            StyleModalSurface(promptSurface); savePromptPanel = promptSurface.gameObject;
            RectTransform promptRect = (RectTransform)savePromptPanel.transform;
            promptRect.anchorMin = new Vector2(0, 0); promptRect.anchorMax = new Vector2(1, 0); promptRect.pivot = new Vector2(.5f, 0);
            promptRect.offsetMin = new Vector2(50, 115); promptRect.offsetMax = new Vector2(-50, 1015);
            var layout = savePromptPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(38, 38, 38, 38); layout.spacing = 24; layout.childForceExpandHeight = false;
            savePromptTitle = Label("是否保存本次结果？", savePromptPanel.transform, 40, TextAnchor.MiddleCenter, PrimaryText); SetHeight(savePromptTitle.rectTransform, 100);
            savePromptFaces = Horizontal("SavePromptFaces", savePromptPanel.transform, 220);
            Text noteTitle = Label("备注（可选，可在历史记录中继续修改）", savePromptPanel.transform, 25, TextAnchor.MiddleLeft, SecondaryText); SetHeight(noteTitle.rectTransform, 46);
            savePromptNote = NoteInput("输入本次备注", savePromptPanel.transform, 110);
            Button("ConfirmSave", savePromptPanel.transform, "保存本次记录", SaveCurrent, Accent, 88);
            Button("DiscardResult", savePromptPanel.transform, "不保存并删除本次结果", DiscardCurrent, Panel, 82);
            savePromptPanel.SetActive(false);
        }

        private void BuildSeriesPanel(Transform parent)
        {
            Image seriesSurface = Image("SeriesPanel", parent, ModalSurfaceColor());
            StyleModalSurface(seriesSurface); seriesPanel = seriesSurface.gameObject;
            Stretch((RectTransform)seriesPanel.transform, 110, 520, 110, 520);
            var layout = seriesPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(40, 40, 40, 40); layout.spacing = 24; layout.childForceExpandHeight = false;
            Text title = Label("选择投掷数量", seriesPanel.transform, 42, TextAnchor.MiddleCenter, PrimaryText); SetHeight(title.rectTransform, 90);
            Text hint = Label("同时投出对应数量的徽章\n3 枚取 2 胜，5 枚取 3 胜", seriesPanel.transform, 24, TextAnchor.MiddleCenter, SecondaryText); SetHeight(hint.rectTransform, 86);
            Button("One", seriesPanel.transform, "1 枚（单次决定）", () => SelectSeries(1), Panel, 88);
            Button("Three", seriesPanel.transform, "3 枚（取 2 胜）", () => SelectSeries(3), Panel, 88);
            Button("Five", seriesPanel.transform, "5 枚（取 3 胜）", () => SelectSeries(5), Panel, 88);
            Button("Close", seriesPanel.transform, "取消", () => CloseModal(seriesPanel), No, 78);
            seriesPanel.SetActive(false);
        }

        private void BuildCropPanel(Transform parent)
        {
            Image cropSurface = Image("CropPanel", parent, ModalSurfaceColor());
            StyleModalSurface(cropSurface); cropPanel = cropSurface.gameObject;
            Stretch((RectTransform)cropPanel.transform, 70, 160, 70, 130);
            var layout = cropPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(34, 34, 30, 30); layout.spacing = 20; layout.childForceExpandHeight = false;
            cropTitle = Label("裁切圆形徽章", cropPanel.transform, 40, TextAnchor.MiddleCenter, PrimaryText); SetHeight(cropTitle.rectTransform, 66);
            RectTransform cropStage = Rect("CropStage", cropPanel.transform); SetHeight(cropStage, 660);
            cropViewport = Image("CropViewport", cropStage, Color.white); cropViewport.sprite = circleSprite; cropViewport.preserveAspect = true;
            cropViewport.rectTransform.anchorMin = cropViewport.rectTransform.anchorMax = new Vector2(.5f, .5f); cropViewport.rectTransform.sizeDelta = new Vector2(620, 620);
            Mask cropMask = cropViewport.gameObject.AddComponent<Mask>(); cropMask.showMaskGraphic = false;
            Outline cropEdge = cropViewport.gameObject.AddComponent<Outline>(); cropEdge.effectColor = Accent; cropEdge.effectDistance = new Vector2(3f, -3f);
            cropPreview = Image("CropPreview", cropViewport.transform, Color.white); cropPreview.rectTransform.anchorMin = cropPreview.rectTransform.anchorMax = new Vector2(.5f, .5f); cropPreview.rectTransform.pivot = new Vector2(.5f, .5f);
            cropGesture = cropViewport.gameObject.AddComponent<CropGestureHandler>(); cropGesture.Target = cropPreview.rectTransform; cropGesture.Viewport = cropViewport.rectTransform;
            cropRing = Image("CropRing", cropStage, Color.white); cropRing.sprite = CreateRingSprite(); cropRing.preserveAspect = true; cropRing.raycastTarget = false;
            cropRing.rectTransform.anchorMin = cropRing.rectTransform.anchorMax = new Vector2(.5f, .5f); cropRing.rectTransform.sizeDelta = new Vector2(632, 632);
            cropHint = Label("单指拖动图片 · 双指捏合缩放 · 圆圈内为最终徽章", cropPanel.transform, 25, TextAnchor.MiddleCenter, SecondaryText); SetHeight(cropHint.rectTransform, 70);
            Button("ConfirmCrop", cropPanel.transform, "确认裁切并保存", ConfirmCrop, Accent, 84);
            Button("CancelCrop", cropPanel.transform, "取消", CancelCrop, Panel, 74);
            cropPanel.SetActive(false);
        }

        private void Throw(float strength, string source, float heldSeconds)
        {
            if (pending != null) return;
            if (chargeButton != null) chargeButton.SetInteractable(false);
            SetDecisionNavigationLocked(true);
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
            UserActionLog.Add("开始投掷；问题=" + (string.IsNullOrEmpty(question) ? "（未填写）" : question) + "；投掷数量=" + SeriesLabel(seriesLength) + "；徽章=" + selectedBadge.name + "；力度=" + Mathf.RoundToInt(strength * 100) + "%");
            if (throwRoutine != null) StopCoroutine(throwRoutine);
            throwRoutine = StartCoroutine(AnimateThrow(pending));
        }

        private IEnumerator AnimateThrow(PendingDecision value)
        {
            float holdFactor = Mathf.InverseLerp(0f, 3f, Mathf.Clamp(pendingHoldSeconds, 0f, 3f));
            // Even a short press gets a readable, weighty throw. Longer presses still
            // travel higher and extend the full motion, capped at three seconds.
            float duration = Mathf.Lerp(1.05f, 2.25f, Mathf.SmoothStep(0f, 1f, holdFactor));
            BadgeDefinition animationBadge = store.Badges.badges.Find(item => item.id == value.BadgeId) ?? store.SelectedBadge();
            if (homeCollisionMotion != null) yield return homeCollisionMotion.PlayRelease();
            PrepareThrowDiscs(value.SeriesLength, animationBadge);
            Canvas.ForceUpdateCanvases();
            throwDiscBasePositions.Clear();
            for (int i = 0; i < throwDiscs.Count; i++) throwDiscBasePositions.Add(throwDiscs[i].RectTransform.anchoredPosition);
            bool[] physicsStarted = new bool[throwDiscs.Count];
            bool[] resultCorrectionStarted = new bool[throwDiscs.Count];
            status.text = "投掷中…";
            float staggerSeconds = value.SeriesLength <= 1 ? 0f : duration * .045f;
            float totalDuration = duration + staggerSeconds * Mathf.Max(0, throwDiscs.Count - 1);
            for (float t = 0; t < totalDuration; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < throwDiscs.Count; i++)
                {
                    CoinRenderView item = throwDiscs[i];
                    // Multi-disc throws keep the complete single-disc curve and duration,
                    // but start each following disc a little later.
                    float localP = Mathf.Clamp01((t - staggerSeconds * i) / duration);
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
                        // A compact downward compression gives the launch weight without
                        // the old diagonal "slide away" that made a single coin look lost.
                        travel = new Vector2(0f, -24f) * anticipation;
                        flipDegrees = -22f * anticipation;
                        tiltDegrees = 8f * anticipation;
                        rollDegrees = -6f * anticipation;
                        uniformScale = 1f - .05f * anticipation;
                    }
                    else if (localP < .68f)
                    {
                        float flight = (localP - .12f) / .74f;
                        // Physically valid ballistic arc: y = 4H*t*(1-t), equivalent
                        // to an initial upward velocity followed by constant gravity.
                        float height = 4f * Mathf.Lerp(135f, 215f, holdFactor) * flight * (1f - flight);
                        float sideways = Mathf.Lerp(-10f, 18f, Mathf.SmoothStep(0f, 1f, flight));
                        travel = new Vector2(sideways, height);
                        flipDegrees = tiltDegrees = rollDegrees = 0f;
                        if (!physicsStarted[i])
                        {
                            // Rotation should match the visible charge gauge. Device pressure
                            // is still measured and recorded, but must not make a half charge
                            // unexpectedly look like a full-power throw.
                            float spinFactor = holdFactor;
                            // The airborne interval occupies .56 of the throw.  Convert the
                            // requested 3–10 full flips into an angular velocity so every
                            // charge has a clearly visible, deliberate number of rotations.
                            float rotationalSeconds = Mathf.Max(.1f, duration * .70f);
                            // Ease the turn count upward: 0% = 3, 50% = 4.75, 100% = 10.
                            // This preserves the requested endpoints without making ordinary
                            // mid-strength throws feel nearly as frantic as a full charge.
                            float turns = Mathf.Lerp(3f, 10f, spinFactor * spinFactor);
                            float spin = turns * Mathf.PI * 2f / rotationalSeconds;
                            float spinDirection = value.SeriesLength <= 1 || i % 2 == 0 ? 1f : -1f;
                            item.BeginPhysicsSpin(new Vector3(spin * spinDirection, Mathf.Lerp(.12f, .72f, spinFactor) * spinDirection, -Mathf.Lerp(.08f, .42f, spinFactor) * spinDirection), turns);
                            physicsStarted[i] = true;
                        }
                        float normalizedFlight = Mathf.Clamp01(flight / ((.68f - .12f) / .74f));
                        float speedUp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .18f, normalizedFlight));
                        item.SetPhysicsSpinMultiplier(speedUp);
                        applyScriptedPose = false;
                        uniformScale = 1f + Mathf.Sin(flight * Mathf.PI) * .08f;
                    }
                    else
                    {
                        // Choose the final face while the disc is still airborne.
                        // This avoids showing one face on landing and then visibly
                        // snapping to the pre-drawn result.
                        float settle = (localP - .68f) / .32f;
                        float damping = 1f - settle;
                        float correctionStartFlight = (.68f - .12f) / .74f;
                        float correctionStartX = Mathf.Lerp(-10f, 18f, Mathf.SmoothStep(0f, 1f, correctionStartFlight));
                        float ballisticPhase = Mathf.Lerp(correctionStartFlight, 1f, Mathf.SmoothStep(0f, 1f, settle));
                        float height = 4f * Mathf.Lerp(135f, 215f, holdFactor) * ballisticPhase * (1f - ballisticPhase);
                        float bounce = settle > .72f ? Mathf.Abs(Mathf.Sin((settle - .72f) / .28f * Mathf.PI)) * 14f * damping : 0f;
                        travel = new Vector2(Mathf.Lerp(correctionStartX, 0f, Mathf.SmoothStep(0f, 1f, settle)), height + bounce);
                        flipDegrees = tiltDegrees = rollDegrees = 0f;
                        if (!resultCorrectionStarted[i])
                        {
                            float correctionSeconds = duration * .32f;
                            item.BeginResultCorrection(roundYes, correctionSeconds);
                            resultCorrectionStarted[i] = true;
                        }
                        item.CorrectToResult(settle);
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
            status.text = (value.IsYes ? "YES" : "NO") + "  ·  " + SeriesScore(value) + "  ·  YES 概率 " + Mathf.RoundToInt(value.YesProbabilityUsed * 100) + "%\n尚未保存";
            UserActionLog.Add("投掷完成；结果=" + (value.IsYes ? "YES" : "NO"));
            RefreshHistory();
            savePromptNote.text = string.Empty;
            savePromptTitle.text = (value.IsYes ? "YES" : "NO") + " · " + SeriesScore(value) + "\n每枚 YES 概率 " + Mathf.RoundToInt(value.YesProbabilityUsed * 100) + "% · 是否保存？";
            PopulateResultFaces(savePromptFaces, value, animationBadge);
            OpenModal(savePromptPanel);
            throwRoutine = null;
        }

        private void SaveCurrent()
        {
            if (pending == null) return;
            DecisionRecord record = store.SaveExplicit(pending, savePromptNote.text.Trim());
            pending.Note = record.note;
            pending.SavedRecordId = record.id;
            UserActionLog.Add("明确保存本次记录；结果=" + (pending.IsYes ? "YES" : "NO"));
            pending = null; CloseModal(savePromptPanel); ResetHomeVisuals();
            if (chargeButton != null) chargeButton.SetInteractable(true);
            SetDecisionNavigationLocked(false);
            status.text = "本次记录已永久保存。"; RefreshHistory();
        }

        private void DiscardCurrent()
        {
            if (pending != null) UserActionLog.Add("不保存并删除本次结果；结果=" + (pending.IsYes ? "YES" : "NO"));
            pending = null;
            CloseModal(savePromptPanel);
            ResetHomeVisuals();
            if (chargeButton != null) chargeButton.SetInteractable(true);
            SetDecisionNavigationLocked(false);
            status.text = "本次结果已删除。";
            RefreshHistory();
        }

        private void SelectSeries(int value)
        {
            seriesLength = value;
            seriesText.text = "投掷数量：" + SeriesLabel(seriesLength) + "  ›";
            CloseModal(seriesPanel);
            UserActionLog.Add("切换投掷数量：" + SeriesLabel(seriesLength));
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
            var card = VerticalCard("BadgeCard", badgeList, 370);
            RectTransform header = Rect("BadgeHeader", card); SetHeight(header, 48);
            Text handle = Label("☰", header, 35, TextAnchor.MiddleCenter, Accent);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f); handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.pivot = new Vector2(0f, .5f); handle.rectTransform.anchoredPosition = Vector2.zero; handle.rectTransform.sizeDelta = new Vector2(58f, 0f);
            Text name = Label((current ? "使用中 · " : "") + badge.name, header, 31, TextAnchor.MiddleCenter, current ? Accent : PrimaryText);
            Stretch(name.rectTransform, 64, 0, 64, 0);

            Transform body = Horizontal("BadgeBody", card, 284);
            HorizontalLayoutGroup bodyLayout = body.GetComponent<HorizontalLayoutGroup>(); bodyLayout.childForceExpandWidth = false; bodyLayout.spacing = 14;
            AddFacePreview(body, badge, true, !badge.builtIn);
            AddFacePreview(body, badge, false, !badge.builtIn);
            Transform info = VerticalContainer("BadgeInfo", body, false); SetWidth((RectTransform)info, 196);
            Button use = Button("Use", info, current ? "当前使用中" : "设为当前", () => SelectBadgeForUse(badge), current ? Panel : Accent, 58); use.interactable = !current;
            Button("OpenDetail", info, "徽章设置  ›", () => OpenBadgeDetail(badge), Panel, 58);
            GetBadgeStats(badge.id, out int total, out int yesCount, out int noCount);
            float yesPercent = total == 0 ? 0f : yesCount * 100f / total;
            Text probability = Label("YES 概率 " + Mathf.RoundToInt(badge.yesProbability * 100) + "%", info, 21, TextAnchor.MiddleCenter, Accent); SetHeight(probability.rectTransform, 38);
            Text usage = Label("使用 " + total + " 次", info, 20, TextAnchor.MiddleCenter, SecondaryText); SetHeight(usage.rectTransform, 34);
            Text results = Label("YES " + yesCount + "（" + yesPercent.ToString("0.#") + "%）\nNO " + noCount + "（" + (total == 0 ? 0f : 100f - yesPercent).ToString("0.#") + "%）", info, 18, TextAnchor.MiddleCenter, SecondaryText); SetHeight(results.rectTransform, 56);
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
            selectedBadgeText.text = "当前徽章：" + badge.name;
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

        private void PickBackgroundImage()
        {
            pickingBackground = true;
            imageTarget = null;
            UserActionLog.Add("选择自定义背景图");
            files.PickImage();
        }

        private void ResetBackgroundImage()
        {
            if (string.IsNullOrEmpty(store.Appearance.backgroundImagePath)) return;
            store.ClearBackgroundImage();
            spriteCache.Clear();
            UserActionLog.Add("恢复默认背景图");
            RebuildUiAtSettings();
        }

        private void ApplyPickedImage(string path)
        {
            try
            {
                bool backgroundCrop = pickingBackground;
                pickingBackground = false;
                if (!backgroundCrop && imageTarget == null) throw new InvalidOperationException("没有正在编辑的徽章。");
                cropSourcePath = path;
                Sprite sprite = LoadSprite(path);
                if (sprite == null) throw new InvalidDataException("无法读取所选图片。");
                croppingBackground = backgroundCrop;
                cropPreview.sprite = sprite;
                cropTitle.text = backgroundCrop ? "裁切竖屏背景" : "裁切圆形徽章";
                cropHint.text = backgroundCrop
                    ? "单指拖动图片 · 双指捏合缩放 · 方框内为最终背景"
                    : "单指拖动图片 · 双指捏合缩放 · 圆圈内为最终徽章";
                cropViewport.sprite = backgroundCrop ? softRectSprite : circleSprite;
                cropViewport.preserveAspect = !backgroundCrop;
                cropViewport.rectTransform.sizeDelta = backgroundCrop ? new Vector2(348f, 620f) : new Vector2(620f, 620f);
                cropRing.gameObject.SetActive(!backgroundCrop);
                OpenModal(cropPanel);
                Canvas.ForceUpdateCanvases();
                cropGesture.Configure(cropPreview.rectTransform, cropGesture.Viewport, sprite.texture.width, sprite.texture.height);
            }
            catch (Exception exception)
            {
                string message = "图片读取失败：" + exception.Message;
                if (croppingBackground || pickingBackground) backgroundStatus.text = message; else badgeStatus.text = message;
                UserActionLog.Add(message); Debug.LogWarning(exception);
                pickingBackground = false; croppingBackground = false;
            }
        }

        private void CancelCrop()
        {
            croppingBackground = false;
            cropSourcePath = null;
            CloseModal(cropPanel);
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
                if (croppingBackground)
                {
                    store.CopyBackgroundImageCropped(cropSourcePath, cropGesture.Zoom, -offset.x, -offset.y);
                    spriteCache.Clear();
                    croppingBackground = false;
                    cropSourcePath = null;
                    CloseModal(cropPanel);
                    UserActionLog.Add("裁切并保存竖屏背景图：720×1280");
                    RebuildUiAtSettings();
                    return;
                }
                store.CopyBadgeImage(imageTarget, imageTargetIsYes, cropSourcePath, cropGesture.Zoom, -offset.x, -offset.y);
                InvalidateSprite(imageTargetIsYes ? imageTarget.yesImagePath : imageTarget.noImagePath);
                CloseModal(cropPanel);
                badgeStatus.text = "已将“" + imageTarget.name + "”的 " + (imageTargetIsYes ? "YES" : "NO") + " 面裁切为 512×512 圆形图片。";
                UserActionLog.Add("裁切并保存徽章图片：" + imageTarget.name + " / " + (imageTargetIsYes ? "YES" : "NO"));
                RefreshBadges(); RefreshHomeFaces();
                if (detailBadge == imageTarget) RefreshBadgeDetailFaces();
            }
            catch (Exception exception)
            {
                string message = "图片裁切失败：" + exception.Message;
                if (croppingBackground) backgroundStatus.text = message; else badgeStatus.text = message;
                UserActionLog.Add(message);
            }
        }

        private void RefreshHomeFaces()
        {
            if (homeFaces == null) return;
            Clear(homeFaces);
            BadgeDefinition badge = store.SelectedBadge();
            RectTransform yesFace = AddCollisionFace(homeFaces, badge, true, new Vector2(-160f, 16f), -5.5f);
            RectTransform noFace = AddCollisionFace(homeFaces, badge, false, new Vector2(160f, 16f), 5.5f);

            if (homeCollisionMotion != null) homeCollisionMotion.Configure(yesFace, noFace, null);
        }

        private RectTransform AddCollisionFace(Transform parent, BadgeDefinition badge, bool yesFace, Vector2 position, float rotation)
        {
            RectTransform holder = Rect(yesFace ? "CollisionYES" : "CollisionNO", parent);
            holder.anchorMin = holder.anchorMax = new Vector2(.5f, .5f);
            holder.pivot = new Vector2(.5f, .5f);
            holder.anchoredPosition = position;
            holder.sizeDelta = new Vector2(390f, 430f);
            holder.localRotation = Quaternion.Euler(0f, 0f, rotation);

            Image glow = Image("BadgeGlow", holder, new Color(yesFace ? Yes.r : No.r, yesFace ? Yes.g : No.g, yesFace ? Yes.b : No.b, lightTheme ? .20f : .27f));
            glow.sprite = circleSprite; glow.preserveAspect = true; glow.raycastTarget = false;
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(.5f, .5f);
            glow.rectTransform.sizeDelta = new Vector2(350f, 350f);
            glow.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            Shadow glowShadow = glow.gameObject.AddComponent<Shadow>(); glowShadow.effectColor = SurfaceShadow; glowShadow.effectDistance = new Vector2(0f, -10f);

            string path = yesFace ? badge.yesImagePath : badge.noImagePath;
            Sprite loaded = LoadSprite(path);
            Image content = CircularFaceImage(yesFace ? "HomeYesFace" : "HomeNoFace", holder, loaded ?? DefaultFaceSprite(badge, yesFace));
            RectTransform viewport = (RectTransform)content.transform.parent;
            viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
            viewport.pivot = new Vector2(.5f, .5f);
            viewport.sizeDelta = new Vector2(326f, 326f);
            viewport.anchoredPosition = new Vector2(0f, 30f);
            Outline faceEdge = viewport.gameObject.AddComponent<Outline>(); faceEdge.effectColor = new Color(1f, 1f, 1f, .82f); faceEdge.effectDistance = new Vector2(3f, -3f);

            return holder;
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
            float imageSize = count == 1 ? 390f : count == 3 ? 270f : 190f;
            float horizontalStep = count == 3 ? 205f : 158f;
            for (int i = 0; i < count; i++)
            {
                Transform cell = VerticalContainer("ThrowCell", throwStage, true);
                RectTransform cellRect = (RectTransform)cell;
                cellRect.anchorMin = cellRect.anchorMax = new Vector2(.5f, .5f);
                cellRect.pivot = new Vector2(.5f, .5f);
                float lane = count <= 1 ? 0f : i - (count - 1) * .5f;
                cellRect.anchoredPosition = new Vector2(lane * horizontalStep, 0f);
                cellRect.sizeDelta = new Vector2(imageSize, imageSize + 54f);
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
            HorizontalLayoutGroup layout = parent.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childForceExpandWidth = false;
                layout.spacing = results.Length >= 5 ? 8f : 18f;
            }
            float faceWidth = results.Length == 1 ? 220f : results.Length == 3 ? 176f : 104f;
            for (int i = 0; i < results.Length; i++) AddResultFace(parent, badge, results[i] == 'Y', faceWidth);
        }

        private void AddResultFace(Transform parent, BadgeDefinition badge, bool yesFace, float width)
        {
            Transform container = VerticalContainer("ResultFace", parent, true);
            SetWidth((RectTransform)container, width);
            string path = yesFace ? badge.yesImagePath : badge.noImagePath;
            Sprite loaded = LoadSprite(path);
            CircularFaceImage("ResultImage", container, loaded ?? DefaultFaceSprite(badge, yesFace));
            Text label = Label(yesFace ? "YES" : "NO", container, 30, TextAnchor.MiddleCenter, yesFace ? Yes : No); SetHeight(label.rectTransform, 46);
        }

        private void ToggleTheme()
        {
            lightTheme = !lightTheme;
            UserActionLog.Add("切换主题：" + (lightTheme ? "日间" : "夜间"));
            RebuildUiAtSettings();
        }

        private string InAppName()
        {
            return "决策勋章";
        }

        private void OnBackgroundOpacityChanged(float value)
        {
            float opacity = Mathf.Round(Mathf.Clamp01(value) * 100f) / 100f;
            if (backgroundOpacityText != null)
                backgroundOpacityText.text = "背景不透明度：" + Mathf.RoundToInt(opacity * 100f) + "%";
            if (themeBackgroundImage != null)
                themeBackgroundImage.color = new Color(1f, 1f, 1f, opacity);
            if (themeBackgroundVeil != null)
                themeBackgroundVeil.color = new Color(.97f, .99f, 1f, .08f * opacity);
            if (customBackgroundImage != null)
                customBackgroundImage.color = new Color(1f, 1f, 1f, opacity);
            if (customBackgroundVeil != null)
                customBackgroundVeil.color = new Color(.95f, .985f, 1f, .08f * opacity);
            if (!Mathf.Approximately(store.Appearance.backgroundOpacity, opacity))
                store.SetBackgroundOpacity(opacity);
        }

        private void OnUiPanelOpacityChanged(float value)
        {
            float opacity = Mathf.Round(Mathf.Clamp01(value) * 100f) / 100f;
            if (uiPanelOpacityText != null)
                uiPanelOpacityText.text = "界面底板不透明度：" + Mathf.RoundToInt(opacity * 100f) + "%";
            uiPanelOpacity = opacity;
            ApplyUiPanelOpacity(opacity);
            if (!Mathf.Approximately(store.Appearance.uiPanelOpacity, opacity))
                store.SetUiPanelOpacity(opacity);
        }

        private void ApplyButtonTextColor()
        {
            string raw = buttonTextColorInput == null ? string.Empty : buttonTextColorInput.text.Trim();
            if (!raw.StartsWith("#")) raw = "#" + raw;
            Color parsed;
            if (raw.Length != 7 || !ColorUtility.TryParseHtmlString(raw, out parsed))
            {
                if (buttonTextColorStatus != null) buttonTextColorStatus.text = "颜色格式错误，请输入 6 位 HEX，例如 #283C59";
                UserActionLog.Add("按钮文字颜色格式错误：" + raw);
                return;
            }
            parsed.a = 1f;
            string normalized = "#" + raw.Substring(1).ToUpperInvariant();
            ButtonTextColor = parsed;
            store.SetButtonTextColor(normalized);
            if (buttonTextColorInput != null) buttonTextColorInput.text = normalized;
            if (buttonTextColorStatus != null) buttonTextColorStatus.text = "按钮/蓄力文字颜色：" + normalized;
            ApplyButtonTextColorToUi();
            UserActionLog.Add("应用按钮/蓄力文字颜色：" + normalized);
        }

        private void ResetButtonTextColor()
        {
            const string defaultColor = "#283C59";
            ButtonTextColor = Hex("283C59");
            store.SetButtonTextColor(defaultColor);
            if (buttonTextColorInput != null) buttonTextColorInput.text = defaultColor;
            if (buttonTextColorStatus != null) buttonTextColorStatus.text = "按钮/蓄力文字颜色：" + defaultColor;
            ApplyButtonTextColorToUi();
            UserActionLog.Add("恢复默认按钮/蓄力文字颜色：" + defaultColor);
        }

        private void ApplyButtonTextColorToUi()
        {
            if (uiRoot != null)
            {
                Button[] buttons = uiRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Text[] labels = buttons[i].GetComponentsInChildren<Text>(true);
                    for (int j = 0; j < labels.Length; j++) labels[j].color = ButtonTextColor;
                }
            }
            if (chargeButton != null && chargeButton.Label != null)
                chargeButton.Label.color = ButtonTextColor;
        }

        private static void ApplyUiPanelOpacity(float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            for (int i = uiPanelOpacityTargets.Count - 1; i >= 0; i--)
            {
                UiPanelOpacityEntry entry = uiPanelOpacityTargets[i];
                if (entry == null || entry.image == null)
                {
                    uiPanelOpacityTargets.RemoveAt(i);
                    continue;
                }
                Color color = entry.baseColor;
                color.a = entry.baseColor.a * opacity;
                entry.image.color = color;
            }
        }

        private static void RegisterPanelOpacity(Image image)
        {
            if (image == null) return;
            for (int i = 0; i < uiPanelOpacityTargets.Count; i++)
                if (uiPanelOpacityTargets[i].image == image) return;
            uiPanelOpacityTargets.Add(new UiPanelOpacityEntry { image = image, baseColor = image.color });
        }

        private static void RefreshPanelOpacityBase(Image image)
        {
            if (image == null) return;
            for (int i = 0; i < uiPanelOpacityTargets.Count; i++)
            {
                if (uiPanelOpacityTargets[i].image != image) continue;
                uiPanelOpacityTargets[i].baseColor = image.color;
                return;
            }
            RegisterPanelOpacity(image);
        }

        private void RebuildUiAtSettings()
        {
            if (uiRoot != null) { uiRoot.SetActive(false); Destroy(uiRoot); }
            pages.Clear();
            BuildUi(); ShowPage(3);
        }

        private void ApplyThemePalette()
        {
            Background = lightTheme ? Hex("F4FAFF") : Hex("111D30");
            Panel = lightTheme ? new Color(1f, 1f, 1f, .94f) : new Color(.10f, .16f, .25f, .96f);
            Accent = lightTheme ? Hex("4FB7E8") : Hex("72CBF2");
            Yes = lightTheme ? Hex("43C9B8") : Hex("69DDCE");
            No = lightTheme ? Hex("F07F9B") : Hex("F59AB0");
            PrimaryText = lightTheme ? Hex("283C59") : Hex("F5FAFF");
            SecondaryText = lightTheme ? Hex("71849C") : Hex("AFC1D4");
            SurfaceShadow = lightTheme ? new Color(.18f, .42f, .62f, .14f) : new Color(0f, 0f, 0f, .32f);
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
            Image preview = CircularFaceImage(yesFace ? "YesPreview" : "NoPreview", container, sprite ?? DefaultFaceSprite(badge, yesFace));
            Text face = Label(yesFace ? "YES" : "NO", container, 34, TextAnchor.MiddleCenter, yesFace ? Yes : No); SetHeight(face.rectTransform, 44);
            if (clickable)
            {
                Button button = preview.transform.parent.gameObject.AddComponent<Button>();
                button.targetGraphic = preview;
                button.onClick.AddListener(() => PickBadgeImage(badge, yesFace));
            }
        }

        private Image CircularFaceImage(string name, Transform parent, Sprite sprite)
        {
            Image viewport = Image(name + "Mask", parent, Color.white);
            viewport.sprite = circleSprite;
            viewport.preserveAspect = true;
            viewport.raycastTarget = true;
            SetFlexible(viewport.rectTransform);
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // User-supplied PNG files are often transparent.  A neutral white circular
            // backing keeps those faces readable and prevents the page artwork from
            // becoming part of the badge image.
            Image backing = Image(name + "WhiteBacking", viewport.transform, Color.white);
            backing.raycastTarget = false;
            Stretch(backing.rectTransform);

            Image content = Image(name, viewport.transform, Color.white);
            content.sprite = sprite;
            content.preserveAspect = true;
            content.raycastTarget = false;
            Stretch(content.rectTransform);
            return content;
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
            if (pickingBackground) pickingBackground = false;
            if (badgeStatus != null) badgeStatus.text = message == "Cancelled" ? "已取消文件选择。" : "文件选择失败：" + message;
            if (backgroundStatus != null && message != "Cancelled") backgroundStatus.text = "背景图片选择失败：" + message;
            RefreshLogPreview();
        }

        private static string ModeLabel(DecisionMode value) { return value == DecisionMode.Fair5050 ? "固定 50 / 50" : "徽章概率"; }
        private static string SeriesLabel(int value) { return value == 3 ? "3 枚" : value == 5 ? "5 枚" : "1 枚"; }
        private static string SeriesScore(PendingDecision value) { return value.SeriesLength <= 1 ? "单次决定" : "比分 " + value.YesWins + ":" + value.NoWins; }
        private static string StoredModeLabel(string value) { return value == DecisionMode.Fair5050.ToString() ? "固定 50 / 50" : "徽章概率"; }
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
            // A decision belongs to the result sheet until it is explicitly saved or
            // discarded.  Do not let bottom navigation reveal another page underneath it.
            if (pending != null)
            {
                UserActionLog.Add("投掷结果待确认，已忽略页面切换");
                return;
            }
            for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == index);
            string[] labels = { "投掷", "徽章", "记录", "设置" };
            UserActionLog.Add("切换页面：" + labels[Mathf.Clamp(index, 0, labels.Length - 1)]);
            if (index == 1) RefreshBadges(); if (index == 2) RefreshHistory();
            if (index == 3) RefreshLogPreview();
        }

        private void SetDecisionNavigationLocked(bool locked)
        {
            foreach (Button navigation in navigationButtons)
                if (navigation != null) navigation.interactable = !locked;
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
            RegisterPanelOpacity(card);
            card.sprite = softRectSprite; card.type = UnityEngine.UI.Image.Type.Sliced;
            Outline edge = card.gameObject.AddComponent<Outline>(); edge.effectColor = lightTheme ? new Color(.31f, .72f, .91f, .32f) : new Color(.45f, .77f, .95f, .24f); edge.effectDistance = new Vector2(1f, -1f);
            Shadow shadow = card.gameObject.AddComponent<Shadow>(); shadow.effectColor = SurfaceShadow; shadow.effectDistance = new Vector2(0f, -5f);
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
            Color surfaceColor = lightTheme ? Hex("FBFDFF") : Panel;
            var root = Image("Input", parent, new Color(Accent.r, Accent.g, Accent.b, lightTheme ? .52f : .72f)); SetHeight(root.rectTransform, height);
            RegisterPanelOpacity(root);
            root.sprite = softRectSprite; root.type = UnityEngine.UI.Image.Type.Sliced;
            Image surface = Image("InputSurface", root.transform, surfaceColor);
            RegisterPanelOpacity(surface);
            surface.sprite = softRectSprite; surface.type = UnityEngine.UI.Image.Type.Sliced;
            surface.raycastTarget = false;
            Stretch(surface.rectTransform, 2, 2, 2, 2);
            var field = root.gameObject.AddComponent<InputField>(); field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            Text value = Label("", root.transform, 30, TextAnchor.MiddleLeft, PrimaryText); Stretch(value.rectTransform, 24, 10, 24, 10);
            Text hint = Label(placeholder, root.transform, 28, TextAnchor.MiddleLeft, Hex("667085")); Stretch(hint.rectTransform, 24, 10, 24, 10);
            field.textComponent = value; field.placeholder = hint; field.targetGraphic = root; return field;
        }

        private static InputField NoteInput(string placeholder, Transform parent, float height)
        {
            InputField field = Input(placeholder, parent, height, false);
            Transform surfaceTransform = field.transform.Find("InputSurface");
            if (surfaceTransform != null)
            {
                Image background = surfaceTransform.GetComponent<Image>();
                if (background != null) { background.color = lightTheme ? Hex("EEF6FF") : Hex("26364A"); RefreshPanelOpacityBase(background); }
            }
            return field;
        }

        private static Button Button(string name, Transform parent, string value, UnityEngine.Events.UnityAction action, Color color, float height)
        {
            var image = Image(name, parent, color); SetHeight(image.rectTransform, height);
            RegisterPanelOpacity(image);
            image.sprite = softRectSprite; image.type = UnityEngine.UI.Image.Type.Sliced;
            bool isSurfaceButton = Approximately(color, Panel);
            Outline edge = image.gameObject.AddComponent<Outline>(); edge.effectColor = isSurfaceButton ? new Color(Accent.r, Accent.g, Accent.b, .32f) : new Color(1f, 1f, 1f, .38f); edge.effectDistance = new Vector2(1f, -1f);
            Shadow shadow = image.gameObject.AddComponent<Shadow>(); shadow.effectColor = isSurfaceButton ? SurfaceShadow : new Color(color.r * .55f, color.g * .48f, color.b * .65f, .30f); shadow.effectDistance = new Vector2(0f, -5f);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white; colors.pressedColor = Color.white; colors.selectedColor = Color.white; colors.fadeDuration = .10f; button.colors = colors;
            image.gameObject.AddComponent<SoftPressFeedback>();
            Text label = Label(value, image.transform, 26, TextAnchor.MiddleCenter, ButtonTextColor); Stretch(label.rectTransform, 8, 4, 8, 4); return button;
        }

        private Slider SliderControl(string name, Transform parent, float min, float max, UnityEngine.Events.UnityAction<float> changed)
        {
            // Keep the slider container transparent. The track itself is the only
            // background; a full-width panel behind it made the opacity controls
            // look like large gray cards.
            Image root = Image(name, parent, Color.clear); SetHeight(root.rectTransform, 84); root.raycastTarget = true;
            RectTransform track = Rect("Track", root.transform); track.anchorMin = new Vector2(.08f, .5f); track.anchorMax = new Vector2(.92f, .5f); track.sizeDelta = new Vector2(0, 16);
            Image trackImage = track.gameObject.AddComponent<Image>(); trackImage.color = lightTheme ? Hex("CAD5E2") : Hex("475467"); trackImage.raycastTarget = false;
            RectTransform fillArea = Rect("FillArea", root.transform); fillArea.anchorMin = new Vector2(.08f, .5f); fillArea.anchorMax = new Vector2(.92f, .5f); fillArea.sizeDelta = new Vector2(0, 16);
            Image fill = Image("Fill", fillArea, Accent); Stretch(fill.rectTransform); fill.raycastTarget = false;
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
            RegisterPanelOpacity(root);
            root.sprite = softRectSprite; root.type = UnityEngine.UI.Image.Type.Sliced;
            Outline edge = root.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(Accent.r, Accent.g, Accent.b, .28f); edge.effectDistance = new Vector2(1f, -1f);
            Shadow shadow = root.gameObject.AddComponent<Shadow>(); shadow.effectColor = SurfaceShadow; shadow.effectDistance = new Vector2(0f, -5f);
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
        private static Color ParseHexColor(string value, Color fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            string raw = value.StartsWith("#") ? value : "#" + value;
            return raw.Length == 7 && ColorUtility.TryParseHtmlString(raw, out Color result) ? result : fallback;
        }
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
