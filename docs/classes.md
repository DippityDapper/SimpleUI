# SimpleUI — Classes

APIs below match the source as of SimpleUI 1.2.11 (Phase 1 docking primitives plus `UiFileBrowser`, Proton Places, `UiStack` scroll-viewport fix, `UiToolbar.AddLabel`, content-sized `UiContextMenu`, `UiCatalogue` with scroll batches, drag-out drop, and a cursor ghost). Factory methods are always `Create` (or `Vertical`/`Horizontal`/`Columns`/`Rows` for layout widgets). Every widget takes an optional `UiTheme` defaulting to `UiTheme.Default`.

## Base classes

### `UiElement` (non-generic)

```csharp
public abstract class UiElement
{
    public GameObject GameObject { get; protected set; }
    public RectTransform RectTransform { get; protected set; }
}
```

The non-generic base — just enough for a container to hold *any* widget as a child without caring what it is.

### `UiElement<TSelf>` (generic base)

```csharp
public abstract class UiElement<TSelf> : UiElement where TSelf : UiElement<TSelf>
{
    public TSelf Grow(float weight = 1f);
    public TSelf FixedWidth(float pixels);
    public TSelf FixedHeight(float pixels);
    public TSelf Name(string name);
    public TSelf Visible(bool visible);
}
```

Every concrete widget inherits from this. Methods return `TSelf` so chaining stays typed. Sizing is driven through `LayoutElement` — see [`architecture.md`](architecture.md). There is no `Margin`/`Padding` helper on the base; padding lives on `UiStack` at construction.

## Container widgets

### `UiPanel`

```csharp
public sealed class UiPanel : UiElement<UiPanel>
{
    public Transform ContentParent { get; }
    public static UiPanel Create(Transform parent, UiTheme theme = null,
        string title = null, float titleHeight = 30f, Rect? region = null);
    public UiPanel Add(UiElement content);
}
```

A background box, optionally with a title bar. Fills its parent by default; pass `region` (parent fractions) for a centered/inset panel.

### `UiStack`

```csharp
public enum UiOrientation { Vertical, Horizontal }

public sealed class UiStack : UiElement<UiStack>
{
    public Transform ContentTransform { get; }
    public static UiStack Vertical(Transform parent, UiTheme theme = null,
        float spacing = -1f, float padding = -1f, bool scrollable = false);
    public static UiStack Horizontal(Transform parent, UiTheme theme = null,
        float spacing = -1f, float padding = -1f, bool scrollable = false);
    public UiStack Add(UiElement child);
    public UiStack Clear();
}
```

A layout group that arranges children in a row or column. `scrollable: true` wraps an inner stack in a `ScrollRect` + `RectMask2D`. Negative spacing/padding fall back to the theme.

A scrollable stack reports no preferred size on the scroll axis — it fills whatever its parent assigns. Do not nest a scrollable `UiStack` inside another scrollable stack, or inside a non-scrollable stack that still has a `ContentSizeFitter`. That reports zero height and the form looks empty. Put the `ScrollRect` on the outer host (`Grow()` in a dock, or a region-sized panel) and build inner content with `scrollable: false`. `Add(child.Grow())` is what relaxes this stack's own fitter; parenting via `Create` alone does not. See [conventions.md](conventions.md) ("One scroll viewport").

### `UiSplit`

```csharp
public readonly struct ColumnSpec
{
    public static ColumnSpec Fixed(float pixels);
    public static ColumnSpec Weighted(float weight);
}

public sealed class UiSplitSlot : UiElement<UiSplitSlot>
{
    public Rect NormalizedRect { get; }
}

public sealed class UiSplit
{
    public IReadOnlyList<UiSplitSlot> Slots { get; }
    public static UiSplit Columns(Transform parent, UiTheme theme, params ColumnSpec[] specs);
    public static UiSplit Rows(Transform parent, UiTheme theme, params ColumnSpec[] specs);
}
```

A weight-driven multi-column (or multi-row) layout that keeps ratios stable on resize. Slot fractions are resolved in C# rather than deferred to a Unity layout pass. `Grow`/`FixedWidth` on children are inert here — size comes from the specs only. This is **not** interactive; for a draggable divider use `UiSplitter`.

### `UiScreenSwitcher`

```csharp
public sealed class UiScreenSwitcher
{
    public string CurrentScreen { get; }
    public Transform Register(string name, Transform canvas);
    public Transform GetRoot(string name);
    public void Show(string name);
    public void Clear();
}
```

Shows one named full-stretch sibling at a time. Not itself a `UiElement`. For a visible, reorderable tab strip use `UiTabGroup`.

### `UiSplitter`

```csharp
public sealed class UiSplitter : UiElement<UiSplitter>
{
    public float Fraction { get; }
    public static UiSplitter Create(Transform parent, UiOrientation orientation,
        UiTheme theme = null, float thicknessPx = -1f);
    public UiSplitter OnFractionChanged(Action<float> callback);
    public UiSplitter SetMinPixels(float minFirst, float minSecond);
    public UiSplitter SetFraction(float value, bool notify = false);
    public UiSplitter Bind(RectTransform firstPane, RectTransform secondPane);
}
```

A draggable divider. `Horizontal` is a vertical bar (first = left); `Vertical` is a horizontal bar (first = top). The splitter reports a 0–1 fraction — it does not own sibling layout unless you call `Bind`, which applies anchors to two sibling rects. `UiDockSpace` uses the callback form and applies zone sizes itself.

### `UiTabGroup`

```csharp
public sealed class UiTabGroup : UiElement<UiTabGroup>
{
    public string SelectedId { get; }
    public int TabCount { get; }
    public IReadOnlyList<string> TabIds { get; }
    public static UiTabGroup Create(Transform parent, UiTheme theme = null);
    public UiTabGroup SetReorderable(bool value);
    public UiTabGroup OnChanged(Action<string> callback);
    public UiTabGroup OnClose(Action<string> callback);
    public UiTabGroup OnPinChanged(Action<string, bool> callback);
    public UiTabGroup AddTab(string id, string title, UiElement content,
        bool closable = false, bool pinnable = false);
    public UiTabGroup InsertTab(string id, string title, UiElement content, int index,
        bool closable = false, bool pinnable = false);
    public UiElement RemoveTab(string id);
    public UiTabGroup Select(string id);
    public UiTabGroup MoveTab(string id, int newIndex);
    public UiTabGroup SetPinned(string id, bool pinned);
    public bool IsPinned(string id);
}
```

A visible tab strip over one shared content area. Dragging a tab reorders it within the strip; dragging it *out* of the strip is forwarded to `UiDockSpace` for redocking. Right-click a closable tab for Pin/Close; middle-click closes. Pin shows a left accent. No separate x or P buttons. Pin/close do nothing on non-closable tabs.

### `UiDockPanel`

```csharp
public sealed class UiDockPanel : UiElement<UiDockPanel>
{
    public string Id { get; }
    public string Title { get; }
    public Transform ContentParent { get; }
    public bool Closable { get; }
    public bool Pinnable { get; }
    public bool IsPinned { get; }
    public static UiDockPanel Create(Transform parent, string id, string title,
        UiTheme theme = null, bool closable = true, bool pinnable = true);
    public UiDockPanel Add(UiElement content);
    public UiDockPanel SetTitle(string title);
    public UiDockPanel OnClose(Action<UiDockPanel> callback);
    public UiDockPanel OnPinChanged(Action<UiDockPanel, bool> callback);
    public UiDockPanel SetTitleBarVisible(bool visible);
    public UiDockPanel SetPinned(bool value);
}
```

A titled panel meant to live inside a `UiDockSpace` zone. When hosted by a tab group the title bar is hidden (the tab strip is the title). A pinned panel cannot be closed. Close and pin live on the tab/title (right-click menu; middle-click closes). No separate x or P button.

### `UiDockSpace`

```csharp
public enum DockZone { Left, Center, Right, Bottom }

public sealed class DockZoneSnapshot
{
    public DockZone Zone;
    public float Size;
    public string[] PanelIds;
    public string SelectedPanelId;
}

public sealed class DockLayoutSnapshot
{
    public DockZoneSnapshot[] Zones;
}

public sealed class UiDockSpace : UiElement<UiDockSpace>
{
    public static UiDockSpace Create(Transform parent, UiTheme theme = null);
    public UiDockSpace AddPanel(UiDockPanel panel, DockZone zone);
    public UiDockSpace MovePanel(string panelId, DockZone zone, int tabIndex = -1);
    public bool SelectPanel(string panelId);
    public UiDockPanel RemovePanel(string panelId);
    public UiDockSpace ClosePanel(string panelId);
    public bool TryGetPanel(string panelId, out UiDockPanel panel);
    public DockLayoutSnapshot CaptureLayout();
    public UiDockSpace ApplyLayout(DockLayoutSnapshot snapshot);
}
```

Root dockable container. Four named zones, each a `UiTabGroup`. Panels are always docked — there is no floating state. Dragging a tab onto another zone redocks it. Empty side/bottom zones collapse; Center stays open. **Does not persist layout** — `CaptureLayout` / `ApplyLayout` expose a serializable snapshot the consumer writes (e.g. `layout.json`).

## Interactive widgets

### `UiButton`

```csharp
public sealed class UiButton : UiElement<UiButton>
{
    public Button Button { get; }
    public Image Image { get; }
    public Text Label { get; }
    public static UiButton Create(Transform parent, string label, UnityAction onClick = null,
        UiTheme theme = null, bool primary = true);
    public UiButton OnClick(UnityAction action);
    public UiButton SetLabel(string text);
    public UiButton SetColor(Color color);
    public UiButton Interactable(bool value);
}
```

`primary: true` uses `Theme.ButtonColor`; `false` uses `Theme.RowButtonColor`. `OnClick` is additive.

### `UiToggle`

```csharp
public sealed class UiToggle : UiElement<UiToggle>
{
    public Toggle Toggle { get; }
    public static UiToggle Create(Transform parent, string label, bool initialValue, UiTheme theme = null);
    public UiToggle OnValueChanged(UnityAction<bool> action);
    public UiToggle SetValueSilently(bool value);
}
```

### `UiTextField`

```csharp
public sealed class UiTextField : UiElement<UiTextField>
{
    public InputField InputField { get; }
    public static UiTextField Create(Transform parent, string placeholder = "",
        string initialValue = "", UiTheme theme = null);
    public UiTextField OnChange(UnityAction<string> callback);
    public UiTextField OnEndEdit(UnityAction<string> callback);
    public UiTextField SetValue(string text);
    public string Value { get; }
}
```

### `UiDropdown`

```csharp
public sealed class UiDropdown : UiElement<UiDropdown>
{
    public static UiDropdown Create(Transform parent, string[] options, int initialIndex = 0,
        UiTheme theme = null);
    public UiDropdown OnChange(UnityAction<int, string> callback);
    public UiDropdown SetOptions(params string[] options);
    public int SelectedIndex { get; }
    public string SelectedValue { get; }
}
```

### `UiCatalogue`

```csharp
public sealed class UiCatalogueItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Sprite Image { get; set; }
}

public sealed class UiCatalogue : UiElement<UiCatalogue>
{
    public UiTextField SearchField { get; }
    public string SelectedId { get; }
    public UiCatalogueItem SelectedItem { get; }
    public static UiCatalogue Create(Transform parent, UiTheme theme = null,
        bool scrollable = true);
    public const int DefaultBatchSize = 24;
    public static bool Matches(string id, string name, string query);
    public UiCatalogue SetItems(IReadOnlyList<UiCatalogueItem> items);
    public UiCatalogue SetFilter(string query);
    public UiCatalogue SetBatchSize(int size);
    public UiCatalogue SetSelectedId(string id);
    public UiCatalogue SetItemImage(string id, Sprite sprite);
    public UiCatalogue OnSelected(Action<UiCatalogueItem> handler);
    public UiCatalogue OnActivated(Action<UiCatalogueItem> handler);
    public UiCatalogue OnItemShown(Action<UiCatalogueItem> handler);
    public UiCatalogue OnDropped(Action<UiCatalogueItem, Vector2> handler);
}
```

Searchable card list (thumbnail + name + id). Built-in search matches name and/or id. Single-click selects; double-click activates. Vertical drag inside the list still scrolls. Dragging a card out of the list (or sideways) shows a cursor ghost and fires `OnDropped` with the card and screen position so a host can place on a board hole. When `scrollable` is true the card list is the only `ScrollRect` — parent with `Grow()` in a non-scroll host. Rows instantiate in batches of `DefaultBatchSize` as the user scrolls; `OnItemShown` fires once per created card so the host can load a thumbnail. Do not nest inside another scrollable stack.

### `UiComboBox`

```csharp
public sealed class UiComboBox : UiElement<UiComboBox>
{
    public InputField InputField { get; }
    public static UiComboBox Create(Transform parent, IEnumerable<string> options,
        string defaultText = "", UiTheme theme = null);
    public UiComboBox OnEndEdit(UnityAction<string> callback);
    public UiComboBox SetOptions(IEnumerable<string> options);
    public UiComboBox SetText(string text);
    public string Value { get; }
}
```

An editable field plus a dropdown of known options. The user can type a value that is not in the list. The option list reparents to the root canvas while open so it is not clipped by nested scroll views.

### `UiList<T>`

```csharp
public sealed class UiList<T> : UiElement<UiList<T>>
{
    public static UiList<T> Create(Transform parent, UiOrientation orientation = UiOrientation.Vertical,
        UiTheme theme = null, float spacing = -1f, float padding = -1f, bool scrollable = true);
    public void SetItems(IEnumerable<T> items, Func<T, string> keyFn, Func<Transform, T, UiElement> build);
    public bool TryGetRow(string key, out GameObject rowObject);
    public void Clear();
}
```

A data-bound row list that diffs by key across refreshes instead of destroying and rebuilding every row. Generic — there is no non-generic `UiList`. Defaults to `scrollable: true` (no self-sizing). Inside a self-fitting `UiStack` that must pass `scrollable: false` (or give the list `FixedHeight`/`Grow()` and `Add()` it). Nesting a default-scrollable list inside another scroll view collapses it to zero height — see [conventions.md](conventions.md) ("One scroll viewport").

### `UiTree` / `UiTreeItem`

```csharp
public sealed class UiTreeItem
{
    public string Id;
    public string Label;
    public string IconKey;
    public object UserData;
    public bool Expanded;
    public List<UiTreeItem> Children;
}

public sealed class UiTree : UiElement<UiTree>
{
    public IReadOnlyList<UiTreeItem> SelectedItems { get; }
    public static UiTree Create(Transform parent, UiTheme theme = null);
    public UiTree SetRoots(IEnumerable<UiTreeItem> items);
    public UiTree OnSelectionChanged(Action<IReadOnlyList<UiTreeItem>> callback);
    public UiTree OnReordered(Action<UiTreeItem, UiTreeItem, int> callback);
    public UiTree OnRowRightClick(Action<UiTreeItem, PointerEventData> callback);
    public UiTree OnRowActivated(Action<UiTreeItem> callback);
    public UiTree SetReorderable(bool value);
    public UiTree Select(string id);
    public UiTree ExpandAll();
    public UiTree CollapseAll();
    public UiTree Refresh();
    public UiTreeItem FindById(string id);
}
```

Indented tree: expand/collapse, Ctrl+click multi-select, drag a row onto another to reparent (`SetReorderable(false)` disables that — File Tree uses it so disk rows are not a document). `OnRowActivated` fires after selection on a left double-click. `IconKey` is shown as a text prefix; the tree does not load sprites. Presentation-only — the caller owns persistence. Row labels are single-line (`Overflow`, no wrap) so a long name is not clipped off the second line of the fixed-height row.

### `UiContextMenu`

```csharp
public sealed class UiContextMenu : UiElement<UiContextMenu>
{
    public bool IsOpen { get; }
    public static UiContextMenu Create(Transform canvas, UiTheme theme = null);
    public UiContextMenu ClearItems();
    public UiContextMenu AddItem(string label, Action onClick, bool enabled = true);
    public UiContextMenu AddSeparator();
    public void Show(Vector2 screenPosition);
    public void Hide();
}
```

Right-click popup. Reparents to the root canvas while open. A transparent catcher behind the menu closes it on an outside click. `Show` sizes the menu from its current items (width hugs the longest label plus padding; height = padding + rows). `ClearItems` destroys those rows immediately so a shared menu (File then Edit) does not keep the previous dropdown's height. A `ContentSizeFitter` on the root is not used, because with no LayoutGroup it would collapse to the Image's 0 preferred size and clip labels to a single letter.

### `UiToolbar`

```csharp
public sealed class UiToolbar : UiElement<UiToolbar>
{
    public string ActiveId { get; }
    public static UiToolbar Create(Transform parent, UiTheme theme = null);
    public UiButton AddButton(string id, string label, Action onClick);
    public UiToggle AddToggle(string id, string label, bool initial, Action<bool> onChange);
    public UiToolbar AddSeparator();
    public UiToolbar AddSpacer();
    public UiLabel AddLabel(string text, TextAnchor alignment = TextAnchor.MiddleRight);
    public UiToolbar SetActive(string id);
    public UiToolbar Clear();
    public UiToolbar ClearActive();
    public bool TryGetButton(string id, out UiButton button);
}
```

Horizontal button strip with grouping separators and an optional active-tool highlight (`SetActive`).

### `UiStatusBar`

```csharp
public sealed class UiStatusBar : UiElement<UiStatusBar>
{
    public static UiStatusBar Create(Transform parent, UiTheme theme = null, float heightPx = -1f);
    public UiStatusBar SetText(string text);
    public UiStatusBar SetRightText(string text);
}
```

Thin strip: left-aligned status text, right-aligned indicator (e.g. project id / type).

## Display widgets

### `UiLabel`

```csharp
public sealed class UiLabel : UiElement<UiLabel>
{
    public Text Text { get; }
    public static UiLabel Create(Transform parent, string text, UiTheme theme = null,
        int? fontSize = null, TextAnchor alignment = TextAnchor.MiddleLeft);
    public UiLabel SetText(string text);
    public UiLabel SetColor(Color color);
}
```

Uses Unity's legacy `Text` component (not TextMeshPro) — matching every other SimpleUI widget and the base game's own uGUI text.

### `UiImage`

```csharp
public sealed class UiImage : UiElement<UiImage>
{
    public Image Image { get; }
    public static UiImage Create(Transform parent, Sprite sprite = null, UiTheme theme = null);
    public UiImage SetSprite(Sprite sprite);
    public UiImage OnClickAtUv(Action<Vector2> handler);
}
```

Aspect-preserving image. `OnClickAtUv` reports a normalized UV inside the letterboxed image area (clicks in the margin are ignored).

## Modal dialogs

### `UiModal`

```csharp
public sealed class UiModal : UiElement<UiModal>
{
    public Transform ContentParent { get; }
    public static UiModal Create(Transform canvas, UiTheme theme = null, string title = null,
        float widthPx = 600f, float heightPx = 400f);
    public UiModal Add(UiElement content);
    public void Show();
    public void Hide();
}
```

Centered dialog over a dimmed, click-to-close backdrop. Starts hidden.

### `UiFileBrowser`

```csharp
public enum UiFileBrowserMode { OpenFile, OpenFolder }
public enum UiFileBrowserSort { Name, Size, Type, Modified }

public sealed class UiFileBrowserPlace
{
    public string Label { get; }
    public string Path { get; }
    public UiFileBrowserPlace(string label, string path);
}

public sealed class UiFileBrowser : UiElement<UiFileBrowser>
{
    public string CurrentPath { get; }
    public string SelectedPath { get; }
    public UiFileBrowserMode Mode { get; }
    public static UiFileBrowser Create(Transform parent, UiTheme theme = null);
    public static void PickFile(Transform canvas, string title, string startPath, string[] extensions, Action<string> onSelected, IEnumerable<UiFileBrowserPlace> extraPlaces = null);
    public static void PickFolder(Transform canvas, string title, string startPath, Action<string> onSelected, IEnumerable<UiFileBrowserPlace> extraPlaces = null);
    public static UiFileBrowser EnsureModal(Transform canvas);
    public static void ReleaseModal();
    public static string HostStartDirectory { get; }
    public UiFileBrowser SetMode(UiFileBrowserMode value);
    public UiFileBrowser SetTitle(string title);
    public UiFileBrowser SetExtensions(params string[] values);
    public UiFileBrowser SetPlaces(IEnumerable<UiFileBrowserPlace> places);
    public UiFileBrowser OnSelected(Action<string> callback);
    public UiFileBrowser OnCancelled(Action callback);
    public UiFileBrowser Navigate(string path);
    public void Confirm();
    public void Cancel();
}
```

Dolphin-style in-game file manager: Places sidebar (Home, Desktop, Documents, Downloads, Pictures, Game, Mods, drives, bookmarks), back/forward/up/reload, editable path, breadcrumbs, filter, hidden-file toggle, details columns (name/size/type/modified) with click-to-sort, image preview, status bar, new folder / rename / delete, copy/cut/paste, and a right-click menu. Keyboard: Enter, Backspace, Alt+Left/Right, F5, F2, Delete, Ctrl+L/F/H/C/X/V, Escape. Under Steam Proton, Places also include **Linux Home**, **Linux Pictures**, and **Linux Root** (`Z:\`); the Wine profile is labeled **Proton Home**. `HostStartDirectory` is the host Pictures or home folder.

`Create` embeds the browser in any parent. `PickFile` / `PickFolder` open a shared modal. Character Lab's `FileBrowserPanel` is a thin wrapper around those Pick methods.

## Theme configuration

### `UiTheme`

A container of colors, fonts, and spacing constants. Every widget receives a `theme` at construction. Fields include the original panel/button/label colors plus Phase 1 additions: `SplitterColor`, `SplitterHoverColor`, `SplitterThickness`, `TabBackground`, `TabActiveBackground`, `TabHeight`, `DockDropHighlight`, `TreeSelection`, `TreeRowBackground`, `TreeRowHeight`, `TreeIndent`, `ContextMenuBackground`, `SeparatorColor`, `StatusBarHeight`, `ToolbarHeight`.

`UiTheme.Default` is the shared fallback instance. Themes are applied once at construction — changing a field later does not restyle already-created widgets.
