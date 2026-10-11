using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Code.Game;
using Assets.Code.Locale;
using Assets.Code.UI;
using Assets.Code.UI.Managers;
using Assets.Code.UI.Screens;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkestDungeon3.Ui;

/// <summary>Native menu entry, estate management, and an asset gate before DD1's campaign handoff.</summary>
internal sealed class CampaignMenu : MonoBehaviour
{
    public static CampaignMenu Instance { get; private set; }
    public bool IsOpen => _view != View.Closed;
    private enum View { Closed, Estates, Naming, Delete, Loading }
    private View _view;
    private MainMenuUiScreenBhv _screen;
    private Button _entry, _template, _embark, _delete, _create, _retry, _back;
    private GameObject _canvasRoot, _estatePage, _namingPage, _deletePage, _loadingPage;
    private RectTransform _rows;
    private ScrollRect _scroll;
    private TMP_Text _estateTitle, _estateState, _estateStats, _empty, _message, _deleteName, _loadTitle, _loadMessage;
    private TMP_InputField _nameInput;
    private TMP_FontAsset _headingFont, _bodyFont;
    private CampaignTorch _torch;
    private Canvas _nativeCanvas;
    private SpriteRenderer _nativeTitle;
    private bool _nativeTitleEnabled;
    private Sprite _banner;
    private bool _nativeCanvasEnabled;
    private readonly Dictionary<Selectable, bool> _nativeInteractable = new();
    private readonly List<Row> _estates = new();
    private GameObject _previousSelection;
    private Row _selected;
    private int _pendingSlot, _enterFrame, _lastBackFrame = -1;
    private string _newName;
    private float _nextFind;
    private bool _reportedReady, _smokeOpened, _smokeDismissed, _smokeReopened;
    private static readonly Color Ivory = new(.83f, .81f, .75f), Muted = new(.57f, .57f, .54f), Brass = new(.69f, .57f, .36f), Red = new(.66f, .22f, .16f);
    private bool ReadOnlyCheck => Plugin.DebugKeysEnabled.Value && Environment.GetCommandLineArgs()
        .Any(arg => arg == "-dd3-menu-check" || arg == "-dd3-menu-preview");
    private bool TorchCheck => ReadOnlyCheck && Environment.GetCommandLineArgs().Contains("-dd3-menu-check");

    private sealed class Row
    {
        public int Slot;
        public SaveFile Save;
        public Exception Error;
        public Button Button;
        public TMP_Text Mark, Name;
        public string Title => Save?.Estate?.Name ?? "Estate unavailable";
    }

    private void Awake() => Instance = this;
    private void Update()
    {
        if (Driver.Instance == null) return;
        if (GameModeMgr.CurrentMode != GameModeType.MAIN_MENU || Driver.Instance.Phase != Phase.Off)
        {
            if (IsOpen) Close(false);
            return;
        }
        if (_entry == null && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + 1f;
            try { Attach(); } catch (Exception e) { Plugin.Log.LogError("[campaign menu] " + e); }
        }
        // Debug checks use synthetic summaries and never access real estate files.
        if (ReadOnlyCheck && !_smokeDismissed && _screen != null) { _smokeDismissed = true; _screen.OnMainMenuPress(); }
        if (ReadOnlyCheck && !_smokeOpened && _entry != null && _entry.isActiveAndEnabled && MainMenuControllerBhv.Instance?.HasDisclaimerShown == true)
        {
            _smokeOpened = true;
            _entry.onClick.Invoke();
        }
        if (!IsOpen) return;
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Back(); return; }
        if (_view != View.Loading) return;
        var status = CampaignAssets.Status;
        _loadMessage.text = status.Error ?? "";
        _retry.gameObject.SetActive(status.Error != null);
        if (!status.Ready || !_torch.Finished || Time.frameCount <= _enterFrame + 1) return;
        if (!_reportedReady)
        {
            _reportedReady = true;
            Plugin.Log.LogInfo("[campaign menu] asset gate ready; torch screen painted before campaign handoff");
        }
        if (ReadOnlyCheck)
        {
            if (TorchCheck && !_smokeReopened)
            {
                _smokeReopened = true;
                Close(); _entry.onClick.Invoke();
                Plugin.Log.LogInfo("[campaign menu] read-only smoke: cancelled and reopened; no estate files accessed");
            }
            _loadTitle.text = "Asset check complete";
            _loadMessage.text = "Read-only preview. Return to the main menu to play.";
            SetButtonText(_back, "Main menu");
            return;
        }
        int slot = _pendingSlot;
        string name = _newName;
        Close(false);
        Driver.Instance.EnterHamlet(slot, name);
    }

    private void Attach()
    {
        _screen = UnityEngine.Object.FindObjectOfType<MainMenuUiScreenBhv>();
        if (_screen == null) return;
        _template = AccessTools.Field(typeof(MainMenuUiScreenBhv), "m_kingdomsMainMenuButton")?.GetValue(_screen) as Button;
        if (_template == null) return;
        _headingFont = _template.GetComponentsInChildren<TMP_Text>(true).First().font;
        _banner = _template.GetComponentsInChildren<Image>(true).First(i => i.sprite?.name == "ui_button_banner").sprite;
        _bodyFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name.IndexOf("AlegreyaSans", StringComparison.OrdinalIgnoreCase) >= 0) ?? _headingFont;
        _entry = CloneButton(_template.transform.parent, "The Hamlet", Open);
        _entry.gameObject.name = "DD3 The Hamlet";
        var logo = _entry.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Logo");
        if (logo != null)
        {
            var mark = Label(logo.transform.parent, new Rect(0, 0, 60, 60), "I", 44, Brass, true);
            mark.rectTransform.anchorMin = logo.rectTransform.anchorMin;
            mark.rectTransform.anchorMax = logo.rectTransform.anchorMax;
            mark.rectTransform.pivot = logo.rectTransform.pivot;
            mark.rectTransform.anchoredPosition = logo.rectTransform.anchoredPosition;
            mark.rectTransform.sizeDelta = logo.rectTransform.sizeDelta;
            mark.alignment = TextAlignmentOptions.Center;
        }
        var layout = _entry.GetComponent<LayoutElement>() ?? _entry.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_template.transform.parent);
        var source = (RectTransform)_template.transform;
        var confession = AccessTools.Field(typeof(MainMenuUiScreenBhv), "m_confessionsButton")?.GetValue(_screen) as GameObject;
        float spacing = confession != null ? Mathf.Abs(source.localPosition.y - confession.transform.localPosition.y) : 80;
        if (spacing < 20 || spacing > 200) spacing = 80;
        ((RectTransform)_entry.transform).anchoredPosition = source.anchoredPosition + new Vector2(0, spacing * 2);
        _entry.transform.SetAsFirstSibling();
        if (AccessTools.Field(typeof(MainMenuUiScreenBhv), "m_mainMenuSelectables")?.GetValue(_screen) is List<Selectable> selectables) selectables.Add(_entry);
        var first = confession?.GetComponent<Selectable>();
        if (first != null)
        {
            var navigation = _entry.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnDown = first;
            navigation.selectOnUp = first.navigation.selectOnUp;
            _entry.navigation = navigation;
            navigation = first.navigation; navigation.selectOnUp = _entry; first.navigation = navigation;
        }
        if (_canvasRoot != null) UnityEngine.Object.Destroy(_canvasRoot);
        BuildScreens();
        Plugin.Log.LogInfo($"[campaign menu] attached native Hamlet button; font {_headingFont.name}, spacing {spacing:0}");
    }

    private Button CloneButton(Transform parent, string text, UnityAction action)
    {
        var clone = UnityEngine.Object.Instantiate(_template.gameObject, parent, false);
        clone.SetActive(false);
        foreach (var localization in clone.GetComponentsInChildren<LocalizeTextBhv>(true)) { localization.enabled = false; UnityEngine.Object.Destroy(localization); }
        var button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(action);
        // Button handles submission; the native highlight component paints hover only.
        foreach (var highlight in clone.GetComponentsInChildren<HighlightableButtonBhv>(true))
        {
            AccessTools.Field(typeof(HighlightableButtonBhv), "m_onSubmitAction")?.SetValue(highlight, new UnityEvent());
            AccessTools.Field(typeof(HighlightableButtonBhv), "m_onHoverAction")?.SetValue(highlight, new UnityEvent());
            AccessTools.Field(typeof(HighlightableButtonBhv), "m_onHoverExitAction")?.SetValue(highlight, new UnityEvent());
        }
        foreach (var tooltip in clone.GetComponentsInChildren<Assets.Code.UI.Tooltips.TooltipUiBhv>(true))
        {
            tooltip.enabled = false;
            UnityEngine.Object.Destroy(tooltip);
        }
        foreach (var image in clone.GetComponentsInChildren<Image>(true))
            if (image.name == "Logo" && image.sprite?.name == "icon_logo_kingdoms_small") image.gameObject.SetActive(false);
        var group = clone.GetComponent<CanvasGroup>();
        if (group != null) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
        button.interactable = true; SetButtonText(button, text); SetButtonColours(button, Ivory); clone.SetActive(true);
        return button;
    }

    private static void SetButtonText(Button button, string text)
    {
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.text = text;
    }
    private static void SetButtonColours(Button button, Color colour)
    {
        var colors = button.colors;
        colors.normalColor = colour;
        colors.highlightedColor = colors.selectedColor = Color.Lerp(colour, Color.white, .28f);
        colors.pressedColor = Color.white;
        colors.disabledColor = new Color(Muted.r, Muted.g, Muted.b, .45f);
        button.colors = colors;
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.color = Color.white;
    }

    private void BuildScreens()
    {
        _canvasRoot = new GameObject("DD3 Campaign Front End", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvasRoot.transform.SetParent(transform, false);
        var canvas = _canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
        var scaler = _canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        ImageAt(_canvasRoot.transform, new Rect(0, 0, 1920, 1080), new Color(.008f, .009f, .012f, .80f));

        _estatePage = Page("Estates"); Header(_estatePage.transform, "The Hamlet", "CHOOSE YOUR ESTATE");
        var viewport = new GameObject("Estate banners", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(_estatePage.transform, false); Position((RectTransform)viewport.transform, new Rect(110, 330, 790, 490));
        _rows = new GameObject("Estate list", typeof(RectTransform)).GetComponent<RectTransform>();
        _rows.SetParent(viewport.transform, false); Position(_rows, new Rect(0, 0, 790, 490));
        _scroll = viewport.AddComponent<ScrollRect>(); _scroll.viewport = (RectTransform)viewport.transform; _scroll.content = _rows;
        _scroll.horizontal = false; _scroll.movementType = ScrollRect.MovementType.Clamped; _scroll.scrollSensitivity = 44;
        _empty = Label(_estatePage.transform, new Rect(150, 370, 640, 120), "No estates yet.\nBegin your legacy below.", 30, Muted);
        _estateTitle = Label(_estatePage.transform, new Rect(1040, 370, 700, 130), "", 64, Ivory, true);
        _estateState = Label(_estatePage.transform, new Rect(1045, 525, 650, 45), "", 28, Brass, true);
        _estateStats = Label(_estatePage.transform, new Rect(1045, 593, 650, 130), "", 27, Muted);
        _embark = PanelButton(_estatePage.transform, new Rect(1030, 760, 680, 90), "Return to the Hamlet", EnterSelected);
        _delete = PanelButton(_estatePage.transform, new Rect(1180, 874, 380, 55), "Delete estate", ConfirmDelete, Red);
        PanelButton(_estatePage.transform, new Rect(130, 846, 670, 80), "Establish a new estate", ShowNaming, Brass);
        _message = Label(_estatePage.transform, new Rect(140, 940, 1520, 55), "", 23, Red);

        _namingPage = Page("A new estate"); Header(_namingPage.transform, "A new legacy", "ESTABLISH AN ESTATE");
        CenterLabel(_namingPage.transform, new Rect(460, 362, 1000, 55), "What name will your estate bear?", 34, Ivory, true);
        var field = ImageAt(_namingPage.transform, new Rect(510, 455, 900, 100), new Color(.12f, .11f, .10f));
        field.sprite = _banner;
        _nameInput = field.gameObject.AddComponent<TMP_InputField>();
        var text = CenterLabel(field.transform, new Rect(50, 15, 800, 70), "", 44, Ivory, true);
        var placeholder = CenterLabel(field.transform, new Rect(50, 15, 800, 70), "Estate name", 40, Muted, true);
        _nameInput.textViewport = field.rectTransform; _nameInput.textComponent = (TextMeshProUGUI)text;
        _nameInput.placeholder = placeholder; _nameInput.targetGraphic = field; _nameInput.characterLimit = 40;
        _nameInput.lineType = TMP_InputField.LineType.SingleLine;
        _nameInput.onValueChanged.AddListener(value => _create.interactable = !ReadOnlyCheck && !string.IsNullOrWhiteSpace(value));
        _nameInput.onSubmit.AddListener(_ => CreateEstate());
        _create = PanelButton(_namingPage.transform, new Rect(625, 658, 670, 90), "Begin the campaign", CreateEstate, Brass);
        CenterLabel(_namingPage.transform, new Rect(510, 570, 900, 60), "Your journey begins on the Old Road.", 27, Muted);

        _deletePage = Page("Delete estate confirmation"); Header(_deletePage.transform, "Abandon this legacy?", "DELETE ESTATE", Red);
        _deleteName = CenterLabel(_deletePage.transform, new Rect(360, 400, 1200, 110), "", 64, Ivory, true);
        CenterLabel(_deletePage.transform, new Rect(480, 535, 960, 90), "This will permanently erase this estate and its recovery save.", 30, Muted);
        PanelButton(_deletePage.transform, new Rect(395, 705, 530, 85), "Keep estate", ShowEstates);
        var erase = PanelButton(_deletePage.transform, new Rect(995, 705, 530, 85), "Delete estate", DeleteSelected, Red);
        if (ReadOnlyCheck) erase.interactable = false;

        _loadingPage = Page("Torch loading screen");
        ImageAt(_loadingPage.transform, new Rect(0, 0, 1920, 1080), new Color(.025f, .022f, .020f, 1));
        var torchRoot = new GameObject("Burning torch", typeof(RectTransform)); torchRoot.transform.SetParent(_loadingPage.transform, false);
        Position((RectTransform)torchRoot.transform, new Rect(710, 245, 500, 430));
        _torch = torchRoot.AddComponent<CampaignTorch>(); _torch.Build();
        _loadTitle = CenterLabel(_loadingPage.transform, new Rect(360, 728, 1200, 90), "Returning to the Hamlet", 48, Ivory, true);
        _loadMessage = CenterLabel(_loadingPage.transform, new Rect(460, 824, 1000, 90), "", 26, Red);
        _retry = PanelButton(_loadingPage.transform, new Rect(710, 920, 500, 65), "Try again", Retry, Brass);
        _back = PanelButton(_canvasRoot.transform, new Rect(80, 996, 360, 60), "Back", Back);
        _canvasRoot.SetActive(false);
    }

    private GameObject Page(string name)
    {
        var page = new GameObject(name, typeof(RectTransform)); page.transform.SetParent(_canvasRoot.transform, false);
        Stretch((RectTransform)page.transform); return page;
    }
    private void Header(Transform parent, string title, string caption, Color? colour = null)
    {
        Label(parent, new Rect(145, 115, 1500, 35), "DARKEST2IN1", 23, Brass);
        Label(parent, new Rect(140, 166, 1600, 100), title, 78, Ivory, true);
        Label(parent, new Rect(148, 285, 1300, 35), caption, 23, colour ?? Muted);
    }
    private Button PanelButton(Transform parent, Rect rect, string text, UnityAction action, Color? colour = null)
    {
        var button = CloneButton(parent, text, action); Position((RectTransform)button.transform, rect);
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        SetButtonColours(button, colour ?? Ivory);
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.richText = false;
        return button;
    }
    private TMP_Text CenterLabel(Transform parent, Rect rect, string text, float size, Color colour, bool heading = false)
    {
        var label = Label(parent, rect, text, size, colour, heading); label.alignment = TextAlignmentOptions.Center; return label;
    }
    private TMP_Text Label(Transform parent, Rect rect, string text, float size, Color colour, bool heading = false)
    {
        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); label.transform.SetParent(parent, false);
        Position(label.rectTransform, rect); label.font = heading ? _headingFont : _bodyFont; label.fontSize = size; label.color = colour; label.text = text;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false; label.richText = false;
        label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis; return label;
    }
    private static Image ImageAt(Transform parent, Rect rect, Color colour)
    {
        var image = new GameObject("Ink", typeof(RectTransform)).AddComponent<Image>(); image.transform.SetParent(parent, false);
        Position(image.rectTransform, rect); image.color = colour; return image;
    }
    private static void Position(RectTransform transform, Rect rect)
    {
        transform.anchorMin = transform.anchorMax = new Vector2(0, 1); transform.pivot = new Vector2(0, 1);
        transform.anchoredPosition = new Vector2(rect.x, -rect.y); transform.sizeDelta = new Vector2(rect.width, rect.height);
    }
    private static void Stretch(RectTransform transform)
    {
        transform.anchorMin = Vector2.zero; transform.anchorMax = Vector2.one; transform.offsetMin = transform.offsetMax = Vector2.zero;
    }

    public void Open()
    {
        if (IsOpen || Driver.Instance?.Phase != Phase.Off || _canvasRoot == null || MainMenuControllerBhv.Instance?.HasDisclaimerShown != true || _screen.IsLoadingGameOrCinematic) return;
        _previousSelection = EventSystem.current?.currentSelectedGameObject;
        _nativeCanvas = _screen.GetComponentInParent<Canvas>();
        if (_nativeCanvas != null) { _nativeCanvasEnabled = _nativeCanvas.enabled; _nativeCanvas.enabled = false; }
        // The Mountain scene's title is a world sprite on its own canvas path, outside the menu screen.
        _nativeTitle = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>().FirstOrDefault(r => r.name == "Darkest_Dungeon_Horizontal_TItle_Sprite");
        if (_nativeTitle != null) { _nativeTitleEnabled = _nativeTitle.enabled; _nativeTitle.enabled = false; }
        _nativeInteractable.Clear();
        foreach (var selectable in _screen.GetComponentsInChildren<Selectable>(true)) { _nativeInteractable[selectable] = selectable.interactable; selectable.interactable = false; }
        EventSystem.current?.SetSelectedGameObject(null); _canvasRoot.SetActive(true);
        if (TorchCheck) { BeginLoading(0, null, null); return; }
        ReadSlots(); ShowEstates();
        if (ReadOnlyCheck && Environment.GetCommandLineArgs().Contains("-dd3-menu-show-name")) ShowNaming();
        if (ReadOnlyCheck && Environment.GetCommandLineArgs().Contains("-dd3-menu-show-delete")) ConfirmDelete();
    }
    private void SetView(View view)
    {
        _view = view; _estatePage.SetActive(view == View.Estates); _namingPage.SetActive(view == View.Naming);
        _deletePage.SetActive(view == View.Delete); _loadingPage.SetActive(view == View.Loading);
        SetButtonText(_back, view == View.Loading ? "Cancel" : "Back");
    }
    private void ReadSlots()
    {
        int selected = _selected?.Slot ?? 0;
        foreach (var row in _estates) if (row.Button != null) UnityEngine.Object.Destroy(row.Button.gameObject);
        _estates.Clear(); _selected = null; _message.text = "";
        try
        {
            if (ReadOnlyCheck)
            {
                foreach (var name in new[] { "The Hamlet", "The Wolves at the Door", "The Old Road" })
                    _estates.Add(new Row { Slot = _estates.Count + 1, Save = new SaveFile { Estate = new Estate { Name = name, Week = _estates.Count * 4 } } });
            }
            else foreach (int slot in EstateSlots.List(Session.SaveDir))
            {
                var summary = EstatePickerSlot.Read(Session.SlotPath(slot));
                _estates.Add(new Row { Slot = slot, Save = summary.Save, Error = summary.Error ?? (summary.Save == null ? new FileNotFoundException("The estate was removed.") : null) });
            }
        }
        catch (Exception e) { _message.text = "Estates could not be listed. " + e.Message; }
        for (int i = 0; i < _estates.Count; i++)
        {
            var row = _estates[i];
            var ink = ImageAt(_rows, new Rect(0, i * 115, 770, 105), Color.white);
            ink.sprite = _banner;
            var button = row.Button = ink.gameObject.AddComponent<Button>();
            button.targetGraphic = ink;
            button.onClick.AddListener(() => Select(row));
            row.Mark = Label(button.transform, new Rect(20, 28, 65, 55), "I", 36, Muted, true);
            row.Name = Label(button.transform, new Rect(90, 14, 630, 58), row.Title, 37, Ivory, true);
            Label(button.transform, new Rect(94, 68, 600, 27), row.Error != null ? "Save unavailable" : $"Week {row.Save?.Estate.Week + 1}", 22, Muted);
            button.gameObject.AddComponent<KeepEstateVisible>().Initialize(_scroll);
        }
        _rows.sizeDelta = new Vector2(790, Math.Max(490, _estates.Count * 115));
        _empty.gameObject.SetActive(_estates.Count == 0);
        Select(_estates.FirstOrDefault(row => row.Slot == selected) ?? _estates.FirstOrDefault());
    }
    private void Select(Row row)
    {
        _selected = row;
        foreach (var estate in _estates) { estate.Mark.color = estate == row ? Red : Muted; estate.Name.color = estate == row ? Brass : Ivory; }
        _embark.gameObject.SetActive(row != null); _delete.gameObject.SetActive(row != null); _embark.interactable = row?.Error == null && row?.Save != null;
        if (row == null) { _estateTitle.text = "Your legacy awaits"; _estateState.text = ""; _estateStats.text = "Establish an estate to begin the campaign."; return; }
        _estateTitle.text = row.Title;
        var data = row.Save?.Estate;
        _estateState.text = row.Error != null ? "The estate could not be read" : row.Save.Expedition != null ? "An expedition awaits" : data.OpeningRaidPending ? "The Old Road awaits" : "Week " + (data.Week + 1);
        _estateStats.text = row.Error != null ? "Its files have been kept. Return to the main menu and try again."
            : $"{data.Roster.Count} heroes\n{data.Get(Currency.Gold):N0} gold" + (row.Save.RecoveredFromBackup ? "\nRecovered from the previous save" : "");
        SetButtonText(_embark, row.Save?.Expedition != null ? "Resume expedition" : "Return to the Hamlet");
    }
    private void ShowEstates()
    {
        SetView(View.Estates); EventSystem.current?.SetSelectedGameObject(_selected?.Button.gameObject ?? _back.gameObject);
    }
    private void ShowNaming()
    {
        SetView(View.Naming); _nameInput.text = ""; _create.interactable = false;
        EventSystem.current?.SetSelectedGameObject(_nameInput.gameObject); _nameInput.ActivateInputField();
    }
    private void CreateEstate()
    {
        if (_view != View.Naming || ReadOnlyCheck || string.IsNullOrWhiteSpace(_nameInput.text)) return;
        try { BeginLoading(EstateSlots.Next(Session.SaveDir), _nameInput.text.Trim(), null); }
        catch (Exception e) { ShowEstates(); _message.text = "The estate could not be created. " + e.Message; }
    }
    private void ConfirmDelete()
    {
        if (_selected == null || _view != View.Estates) return;
        _deleteName.text = _selected.Title; SetView(View.Delete);
        EventSystem.current?.SetSelectedGameObject(_deletePage.GetComponentsInChildren<Button>().First().gameObject);
    }
    private void DeleteSelected()
    {
        if (_selected == null || _view != View.Delete || ReadOnlyCheck) return;
        try { Session.DeleteEstate(_selected.Slot); ReadSlots(); ShowEstates(); }
        catch (Exception e) { ShowEstates(); _message.text = "The estate could not be deleted. " + e.Message; }
    }
    private void EnterSelected()
    {
        if (_view != View.Estates || _selected?.Error != null || _selected?.Save == null) return;
        BeginLoading(_selected.Slot, null, _selected.Save.Estate);
    }
    private void BeginLoading(int slot, string newName, Estate estate)
    {
        _pendingSlot = slot; _newName = newName; _enterFrame = Time.frameCount; _reportedReady = false; SetView(View.Loading);
        _loadTitle.text = newName != null ? "Your journey begins" : _selected?.Save?.Expedition != null ? "Returning to the expedition" : "Returning to the Hamlet";
        _loadMessage.text = ""; _retry.gameObject.SetActive(false); EventSystem.current?.SetSelectedGameObject(_back.gameObject);
        CampaignAssets.Begin(estate);
        if (newName != null && Session.Current != null)
        {
            try { CinematicCache.Prepare(Session.Current.Dd1, Core.Dd1.Dd1Cinematic.Opening); }
            catch (Exception e) { Plugin.Log.LogWarning("[cinematic] " + e.Message); }
        }
    }
    private void Retry()
    {
        if (_view != View.Loading) return;
        Session.BeginLoad(Plugin.Dd1Path.Value, Path.GetDirectoryName(typeof(Plugin).Assembly.Location), Application.streamingAssetsPath);
        CampaignAssets.Begin(_newName == null ? _selected?.Save?.Estate : null); _retry.gameObject.SetActive(false);
    }
    private void Back()
    {
        if (_lastBackFrame == Time.frameCount) return;
        _lastBackFrame = Time.frameCount;
        if (_view == View.Estates || (ReadOnlyCheck && _view == View.Loading)) Close(); else ShowEstates();
    }
    public void Close(bool restoreSelection = true)
    {
        _view = View.Closed; if (_canvasRoot != null) _canvasRoot.SetActive(false);
        if (_nativeCanvas != null) _nativeCanvas.enabled = _nativeCanvasEnabled;
        if (_nativeTitle != null) _nativeTitle.enabled = _nativeTitleEnabled;
        foreach (var pair in _nativeInteractable) if (pair.Key != null) pair.Key.interactable = pair.Value;
        _nativeInteractable.Clear();
        if (restoreSelection && _previousSelection != null && _previousSelection.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(_previousSelection);
    }
    private void OnDestroy()
    {
        Close(false); if (_entry != null) UnityEngine.Object.Destroy(_entry.gameObject); if (Instance == this) Instance = null;
    }
    private sealed class KeepEstateVisible : MonoBehaviour, ISelectHandler
    {
        private ScrollRect _owner;
        public void Initialize(ScrollRect owner) => _owner = owner;
        public void OnSelect(BaseEventData eventData)
        {
            var rect = (RectTransform)transform;
            float top = -rect.anchoredPosition.y, offset = _owner.content.anchoredPosition.y, visible = _owner.viewport.rect.height;
            if (top < offset) offset = top;
            else if (top + rect.rect.height > offset + visible) offset = top + rect.rect.height - visible;
            _owner.content.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, _owner.content.rect.height - visible)));
        }
    }
    [HarmonyPatch(typeof(MainMenuUiScreenBhv), nameof(MainMenuUiScreenBhv.TryGoBack))]
    private static class NativeBack
    {
        private static bool Prefix(ref bool __result)
        {
            if (Instance?.IsOpen != true) return true;
            Instance.Back(); __result = true; return false;
        }
    }
}
