# SimpleUI — Conventions

## Naming

- **Widget class names** start with `Ui` (`UiButton`, `UiPanel`, `UiStack`) — clear that these are UI components, distinct from game-domain classes.
- **Factory methods are always `Create()`** — every widget builds itself via `public static TSelf Create(Transform parent, ...)`; never `new UiButton(...)`.
- **Fluent setter methods are verbs** (`OnClick()`, `SetValue()`, `SetInteractable()`) — not property accessors. This maintains CRTP return-type safety and signals that the method has side effects (updating the underlying UI).
- **Read-only properties use Pascal case** (`IsOn`, `Value`, `Text`) — accessed without side effects, safe to call repeatedly.

## Widget creation

All widgets follow the same pattern:

```csharp
public static TSelf Create(Transform parent, /* required args */, UiTheme theme = null)
{
    // 1. Validate inputs
    // 2. Create GameObject + components
    // 3. Set up RectTransform anchoring/sizing
    // 4. Apply theme colors/fonts
    // 5. Return new instance
}
```

Every widget takes a `parent` Transform (where it's parented in the hierarchy) and an optional `UiTheme` (defaulting to `UiTheme.Default` if null). This makes the widget's appearance and hierarchy explicit at construction time — no hidden defaults or lazy initialization.

## Layout and sizing

### Canvas fractions vs. pixels

The library uses **canvas fractions** (0–1 as a proportion of parent size) for most position/size operations:

- `RectTransform.anchorMin/anchorMax` — expressed in fractions (e.g., `anchorMin = (0, 0)` = bottom-left, `anchorMax = (1, 1)` = top-right)
- `LayoutElement.preferredWidth/preferredHeight` — expressed in **pixels** (an absolute size hint to the layout group)
- `Margin()` / `Padding()` — **pixels**, applied as padding in a layout group or as `RectTransform.offsetMin/offsetMax` for absolute positioning

This asymmetry is intentional:
- Anchoring/positioning use fractions (responsive to screen size).
- Sizing hints use pixels (absolute minimum-width guardrails, readable as "this needs 200px").

### Inside a layout group vs. absolute positioning

- **Inside a `UiStack` or `UiSplit`**: use `.Grow()`, `.FixedWidth()`, `.FixedHeight()`. The parent layout group reads these and arranges children automatically.
- **Absolute positioning (e.g., a centered modal)**: pass a `region` parameter to `.Create()` (`Rect(x, y, width, height)` as screen fractions). No layout group involved; position is set directly on `RectTransform.anchorMin/Max`.

A widget never mixes both — it's either "I'm in a layout group" or "I'm absolutely positioned," not both.

### One scroll viewport

A `scrollable: true` `UiStack` (and `UiList<T>`, which defaults to that) is a stretch-to-fill `ScrollRect`. It reports **no preferred height** — it only fills a size its parent already assigned (`Grow()`, `FixedHeight`, or a stretch rect that already has a real size).

Nesting one inside a `ContentSizeFitter` parent (a non-scrollable `UiStack`, or the inner content of another scrollable stack) collapses the child to **zero height**. The widgets are built and clickable in the hierarchy; they are clipped to an empty viewport. The inspector looks blank.

Rule: **exactly one `ScrollRect`**, on the host that already has an assigned size. Everything inside that host is `scrollable: false` and sizes to its children. Horizontal chip/button rows may scroll if they also have `FixedHeight`.

`UiStack.Add(child.Grow())` relaxes that stack's own fitter so leftover space exists. Parenting a scrollable child with `Create(parent)` and never calling `Add()` skips that relax — same collapse.

This is the same class of bug as a scrollable `UiList<T>` inside a self-fitting section with no `FixedHeight`/`Grow()`. See [`classes.md`](classes.md) (`UiStack`, `UiList<T>`, `UiCatalogue`). A scrollable `UiCatalogue` is the list `ScrollRect` — put it in a non-scroll `Grow()` host.

## Theming

Every widget reads from a shared `UiTheme` at construction:

```csharp
var myTheme = new UiTheme
{
    ButtonBackground = Color.red,
    TextColor = Color.white,
    FontSize = 16
};

var button = UiButton.Create(parent, "Click Me", theme: myTheme);
var label = UiLabel.Create(parent, "Hello", theme: myTheme);
// Both use myTheme's colors/fonts
```

Colors are applied once at construction (not on every frame, so changing `theme.ButtonBackground` later doesn't update already-created buttons). This is a feature — themes are immutable constants, not dynamic configuration.

## GameObject naming

Every widget's `GameObject` is named after its class by default (`UiPanel`, `UiButton`, etc.), with one exception: if a label is provided, it's appended:

```csharp
UiButton.Create(parent, "Save")  // GameObject named "UiButton"
UiLabel.Create(parent, "Status")  // GameObject named "UiLabel"
```

This is purely for inspector readability — the hierarchy shows "UiButton" for a button without a label, "UiButton (Save)" for a button labeled "Save". It helps when debugging in the editor.

## Callbacks and event handling

- **Synchronous callbacks** — all events (`.OnClick()`, `.OnValueChanged()`, `.OnFractionChanged()`, etc.) are immediate, invoked during the frame they occur (mouse click, text edit, drag, etc.).
- **Callback signature** follows `UnityAction` / `Action` conventions — no return value, parameters only for the event data being reported. `.OnClick()` takes `UnityAction` (no args); `.OnValueChanged()` on a toggle takes `UnityAction<bool>` (the new state).
- **`OnClick` is additive** — calling `.OnClick()` twice stacks both handlers on the underlying `Button.onClick`. Other widgets that store a single `Action` field (`UiSplitter.OnFractionChanged`, `UiTabGroup.OnChanged`) replace the previous callback.

For advanced scenarios (multiple callbacks, event broadcasting), the caller can subscribe to raw Unity events instead (`button.GameObject.GetComponent<Button>().onClick.AddListener(...)`).

## Error handling

- **Construction errors** (e.g., invalid column counts in `UiSplit`) throw immediately — fail fast at the callsite rather than silently creating a broken widget.
- **Runtime errors** (e.g., calling `.Add()` on a widget that has no `ContentParent`) throw — invalid at construction time, so this would be a caller bug, not something to silently ignore.
- **Logging** uses `SimpleUIPlugin.Log` (a `ManualLogSource` cached at plugin startup) — only for debug info, not errors. Actual errors throw exceptions.

## Interactivity

- **Widgets are interactive by default** — clickable buttons, editable text fields, toggleable checkboxes.
- **Disable interactivity** with `.SetInteractable(false)` if needed.
- **Modal dialogs** are the only case where a widget actively blocks interaction with siblings — the backdrop captures all raycasts, and `GraphicRaycaster` on the canvas is the single point of truth for which UI layer is interactive.

Widgets never hijack input from the rest of the game — they only react to clicks/input within their own bounds, checked by Unity's `EventSystem` + `GraphicRaycaster`.

Phase 1 docking widgets add drag via `IBeginDragHandler` / `IDragHandler` / `IEndDragHandler` on small helper `MonoBehaviour`s (same pattern as `UiImageClickTarget`). A click that travels far enough to become a drag does not also fire the widget's click handler.

## Composition and nesting limits

The library doesn't enforce nesting depth or panel count limits. A deeply nested hierarchy (panel → stack → split → stack → ...) works fine, but:

- Deep nesting hurts readability — prefer flat hierarchies where possible.
- Every layout group adds CPU time during `LateUpdate` (when `LayoutGroup.CalculateLayoutInputVertical/Horizontal` run) — nested layout groups stack up. For massive UIs, consider handwritten `RectTransform` positioning for lower-level details.

`LokrCharacterLab`'s hierarchy (root panel → stack → split → 3 sidebar panels) is a typical depth — about as deep as you'd want without hitting performance issues.
