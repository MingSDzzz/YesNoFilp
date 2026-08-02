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
        private Text importPreview;
        private GameObject importPanel;
        private HistoryExport pendingImport;
        private BadgeDefinition imageTarget;
        private bool imageTargetIsYes;
        private PendingDecision pending;
        private DecisionMode mode = DecisionMode.Fair5050;
        private Sprite circleSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindObjectOfType<DecisionDiscApp>() == null)
                new GameObject("DecisionDiscApp").AddComponent<DecisionDiscApp>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Screen.orientation = ScreenOrientation.Portrait;
            UnityEngine.Input.multiTouchEnabled = false;
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            circleSprite = CreateCircleSprite();
            store = new DecisionStore();
            files = gameObject.AddComponent<AndroidFileBridge>();
            files.TextImported += PreviewImport;
            files.ImageImported += ApplyPickedImage;
            BuildUi();
            ShowPage(0);
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
            Text title = Label("DECISION DISC", page.transform, 54, TextAnchor.MiddleCenter, Color.white);
            SetHeight(title.rectTransform, 90);
            Text sub = Label("Ask. Hold. Release.", page.transform, 28, TextAnchor.MiddleCenter, Hex("98A2B3")); SetHeight(sub.rectTransform, 50);

            var discWrap = Rect("DiscWrap", page.transform); SetHeight(discWrap, 480);
            disc = Image("Disc", discWrap, Accent); disc.sprite = circleSprite;
            disc.type = UnityEngine.UI.Image.Type.Simple; disc.preserveAspect = true;
            disc.rectTransform.anchorMin = disc.rectTransform.anchorMax = new Vector2(.5f, .5f);
            disc.rectTransform.sizeDelta = new Vector2(380, 380);
            discText = Label("YES / NO", disc.transform, 58, TextAnchor.MiddleCenter, Background); Stretch(discText.rectTransform);

            questionInput = Input("What do you want to decide?", page.transform, 128, false);
            var modeButton = Button("Mode", page.transform, "FAIR 50 / 50", ToggleMode, Panel, 80);
            modeText = modeButton.GetComponentInChildren<Text>();

            var chargeObject = new GameObject("Charge", typeof(RectTransform), typeof(Image), typeof(ChargeThrowButton));
            chargeObject.transform.SetParent(page.transform, false); SetHeight((RectTransform)chargeObject.transform, 160);
            chargeObject.GetComponent<Image>().color = Hex("243B53");
            var fill = Image("Fill", chargeObject.transform, Accent); Stretch(fill.rectTransform); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0;
            var chargeLabel = Label("HOLD TO THROW", chargeObject.transform, 40, TextAnchor.MiddleCenter, Color.white); Stretch(chargeLabel.rectTransform);
            var charge = chargeObject.GetComponent<ChargeThrowButton>(); charge.Label = chargeLabel; charge.Fill = fill; charge.Released += Throw;

            status = Label("Your unsaved result will appear here", page.transform, 30, TextAnchor.MiddleCenter, Hex("D0D5DD")); SetHeight(status.rectTransform, 70);
            noteInput = Input("Optional note (saved only with the record)", page.transform, 100, false);
            saveButton = Button("Save", page.transform, "SAVE THIS RECORD", SaveCurrent, Accent, 88);
            saveButton.interactable = false;
            return page;
        }

        private GameObject BuildBadges(Transform parent)
        {
            var page = Page("BadgesPage", parent);
            Header(page.transform, "BADGES", "Create a badge and copy both faces into the app");
            Button("AddBadge", page.transform, "+  CREATE NEW BADGE", CreateBadge, Accent, 88);
            badgeList = ScrollContent("BadgeScroll", page.transform, 0);
            RefreshBadges();
            return page;
        }

        private GameObject BuildHistory(Transform parent)
        {
            var page = Page("HistoryPage", parent);
            Header(page.transform, "SAVED HISTORY", "Only records you explicitly saved appear here");
            var actions = Horizontal("Actions", page.transform, 92);
            Button("Export", actions, "EXPORT JSON", () => files.ExportJson(store.CreateExportJson()), Panel, 82);
            Button("Import", actions, "IMPORT JSON", files.PickJson, Panel, 82);
            historyList = ScrollContent("HistoryScroll", page.transform, 0);
            RefreshHistory();
            return page;
        }

        private GameObject BuildSettings(Transform parent)
        {
            var page = Page("SettingsPage", parent);
            Header(page.transform, "SETTINGS", "A stable home for future personal preferences");
            CardText(page.transform, "PRIVACY\nCurrent questions and unsaved outcomes stay in memory. Only Save This Record writes history.");
            CardText(page.transform, "RANDOMNESS\nFair 50/50 never biases a face. Strength mode maps the measured force to a 25–75% YES chance.");
            CardText(page.transform, "STORAGE\nSaved data and copied badge images live in Application.persistentDataPath.");
            CardText(page.transform, "VERSION\nDecision Disc 1.0 · JSON schema v1");
            return page;
        }

        private void BuildNavigation(Transform safe)
        {
            var nav = Horizontal("Navigation", safe, 94);
            var rt = (RectTransform)nav; rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0); rt.anchoredPosition = Vector2.zero;
            string[] names = { "THROW", "BADGES", "HISTORY", "SETTINGS" };
            for (int i = 0; i < names.Length; i++) { int index = i; Button("Nav" + i, nav, names[i], () => ShowPage(index), Panel, 94); }
        }

        private void BuildImportPanel(Transform parent)
        {
            importPanel = Image("ImportPreviewPanel", parent, new Color(0.05f, .07f, .12f, .97f)).gameObject; Stretch((RectTransform)importPanel.transform, 70, 220, 70, 220);
            var layout = importPanel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(32, 32, 32, 32); layout.spacing = 24;
            Label("IMPORT PREVIEW", importPanel.transform, 42, TextAnchor.MiddleCenter, Color.white);
            importPreview = Label("", importPanel.transform, 28, TextAnchor.UpperLeft, Color.white); SetFlexible(importPreview.rectTransform);
            Button("Merge", importPanel.transform, "MERGE WITH SAVED RECORDS", () => ApplyImport(false), Accent, 86);
            Button("Replace", importPanel.transform, "REPLACE SAVED RECORDS", () => ApplyImport(true), No, 86);
            Button("Cancel", importPanel.transform, "CANCEL", () => importPanel.SetActive(false), Panel, 76);
            importPanel.SetActive(false);
        }

        private void Throw(float strength, string source)
        {
            string question = questionInput.text.Trim();
            if (string.IsNullOrEmpty(question)) { status.text = "Enter a question first."; return; }
            bool yes = DecisionEngine.Decide(strength, mode);
            pending = new PendingDecision { Question = question, IsYes = yes, Strength = strength, StrengthSource = source, Mode = mode, TimestampUtc = DateTime.UtcNow, BadgeId = store.SelectedBadge().id };
            saveButton.interactable = false;
            StopAllCoroutines(); StartCoroutine(AnimateThrow(pending));
        }

        private IEnumerator AnimateThrow(PendingDecision value)
        {
            float duration = 1.45f + value.Strength * .8f;
            Vector2 start = disc.rectTransform.anchoredPosition;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = t / duration;
                float height = Mathf.Sin(p * Mathf.PI) * (260 + 260 * value.Strength);
                disc.rectTransform.anchoredPosition = start + Vector2.up * height;
                disc.rectTransform.localEulerAngles = new Vector3(0, p * (720 + 1080 * value.Strength), p * 80);
                disc.rectTransform.localScale = new Vector3(Mathf.Max(.08f, Mathf.Abs(Mathf.Cos(p * Mathf.PI * (5 + value.Strength * 5)))), 1, 1);
                discText.text = Mathf.FloorToInt(p * 12) % 2 == 0 ? "YES" : "NO";
                yield return null;
            }
            disc.rectTransform.anchoredPosition = start;
            disc.rectTransform.localEulerAngles = Vector3.zero; disc.rectTransform.localScale = Vector3.one;
            RenderDiscFace(value.IsYes, store.SelectedBadge());
            status.text = (value.IsYes ? "YES" : "NO") + "  ·  strength " + Mathf.RoundToInt(value.Strength * 100) + "%  ·  " + value.StrengthSource + "\nUnsaved — tap Save This Record to keep it";
            saveButton.interactable = true;
        }

        private void SaveCurrent()
        {
            if (pending == null) return;
            store.SaveExplicit(pending, noteInput.text.Trim());
            pending = null; saveButton.interactable = false; noteInput.text = string.Empty;
            status.text = "Saved explicitly to history."; RefreshHistory();
        }

        private void ToggleMode()
        {
            mode = mode == DecisionMode.Fair5050 ? DecisionMode.StrengthInfluences : DecisionMode.Fair5050;
            modeText.text = mode == DecisionMode.Fair5050 ? "FAIR 50 / 50" : "STRENGTH AFFECTS PROBABILITY";
        }

        private void CreateBadge()
        {
            BadgeDefinition badge = store.CreateBadge("My Badge " + store.Badges.badges.Count);
            imageTarget = badge; imageTargetIsYes = true;
            RefreshBadges(); files.PickImage();
        }

        private void RefreshBadges()
        {
            if (badgeList == null) return; Clear(badgeList);
            foreach (BadgeDefinition badgeItem in store.Badges.badges)
            {
                BadgeDefinition badge = badgeItem;
                var card = VerticalCard("Badge", badgeList, 240);
                Text name = Label((badge.id == store.Badges.selectedBadgeId ? "●  " : "○  ") + badge.name, card, 34, TextAnchor.MiddleLeft, Color.white); SetHeight(name.rectTransform, 58);
                var actions = Horizontal("BadgeActions", card, 72);
                Button("Use", actions, "USE", () => { store.SelectBadge(badge.id); RenderDiscFace(true, badge); RefreshBadges(); }, Accent, 70);
                if (!badge.builtIn)
                {
                    Button("YesImage", actions, "YES IMAGE", () => PickBadgeImage(badge, true), Yes, 70);
                    Button("NoImage", actions, "NO IMAGE", () => PickBadgeImage(badge, false), No, 70);
                    Button("Delete", actions, "DELETE", () => { store.DeleteBadge(badge.id); RefreshBadges(); }, Panel, 70);
                }
                Text paths = Label(badge.builtIn ? "Built-in vector face" : "YES: " + Present(badge.yesImagePath) + "\nNO: " + Present(badge.noImagePath), card, 22, TextAnchor.UpperLeft, Hex("98A2B3")); SetHeight(paths.rectTransform, 82);
            }
        }

        private void PickBadgeImage(BadgeDefinition badge, bool yesFace) { imageTarget = badge; imageTargetIsYes = yesFace; files.PickImage(); }

        private void ApplyPickedImage(string path)
        {
            try { store.CopyBadgeImage(imageTarget, imageTargetIsYes, path); RefreshBadges(); if (store.Badges.selectedBadgeId == imageTarget.id) RenderDiscFace(imageTargetIsYes, imageTarget); }
            catch (Exception exception) { Debug.LogWarning(exception); }
        }

        private void RefreshHistory()
        {
            if (historyList == null) return; Clear(historyList);
            if (store.History.records.Count == 0) { CardText(historyList, "No saved records yet."); return; }
            foreach (DecisionRecord recordItem in store.History.records)
            {
                DecisionRecord record = recordItem;
                var card = VerticalCard("Record", historyList, 230);
                Text headline = Label(record.result + "  ·  " + Mathf.RoundToInt(record.strength * 100) + "%  ·  " + record.mode, card, 30, TextAnchor.MiddleLeft, record.result == "YES" ? Yes : No); SetHeight(headline.rectTransform, 52);
                Text question = Label(record.question + (string.IsNullOrEmpty(record.note) ? "" : "\nNote: " + record.note) + "\n" + record.timestampUtc, card, 25, TextAnchor.UpperLeft, Color.white); SetHeight(question.rectTransform, 104);
                Button("Delete", card, "DELETE RECORD", () => { store.DeleteRecord(record.id); RefreshHistory(); }, Panel, 58);
            }
        }

        private void PreviewImport(string json)
        {
            try
            {
                pendingImport = store.ParseImport(json);
                int yesCount = pendingImport.records.FindAll(r => r.result == "YES").Count;
                importPreview.text = "Schema version: " + pendingImport.version + "\nRecords: " + pendingImport.records.Count + "\nYES: " + yesCount + "  ·  NO: " + (pendingImport.records.Count - yesCount) + "\n\nChoose merge or replace. No data changes until you choose.";
                importPanel.SetActive(true);
            }
            catch (Exception exception) { importPreview.text = "Import rejected:\n" + exception.Message; pendingImport = null; importPanel.SetActive(true); }
        }

        private void ApplyImport(bool replace)
        {
            if (pendingImport == null) { importPanel.SetActive(false); return; }
            store.ApplyImport(pendingImport, replace); pendingImport = null; importPanel.SetActive(false); RefreshHistory();
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
            if (index == 1) RefreshBadges(); if (index == 2) RefreshHistory();
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
        private static string Present(string path) { return string.IsNullOrEmpty(path) ? "not selected" : Path.GetFileName(path) + " (app copy)"; }
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
