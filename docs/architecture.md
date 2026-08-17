# SimpleUI — Architecture

## The fluent CRTP pattern

Every concrete widget class inherits from the generic `UiElement<TSelf>` base:

```csharp
public abstract class UiElement<TSelf> : UiElement where TSelf : UiElement<TSelf>
{
    // Common methods that return TSelf, so chaining stays typed
    public TSelf Grow(float weight = 1f);
    public TSelf FixedWidth(float width);
    public TSelf FixedHeight(float height);
    public TSelf Margin(float left, float top, float right, float bottom);
    // ... etc
}

public class UiButton : UiElement<UiButton>
{
    public UiButton OnClick(UnityAction callback);  // returns UiButton, not generic base
    // ...
}
```

This **Curiously Recurring Template Pattern (CRTP)** means that `UiButton.Create(...).Grow().OnClick(callback)` stays typed as `UiButton` the whole way down — callers get IDE autocomplete for `UiButton`-specific methods like `OnClick`, not just the generic `Grow()` methods. The alternative (a single non-generic `UiElement` returning `UiElement` from every method) would lose type safety and require casts.

## Composition model — `.Add()` and parenting

Widgets compose through explicit `.Add(child)` calls:

```csharp
var stack = UiStack.Create(parent);
stack.Add(UiLabel.Create(...));
stack.Add(UiButton.Create(...));
```

Each widget owns a `GameObject` and `RectTransform` (set at construction, never moved). `.Add()` calls `child.transform.SetParent(this.ContentParent, worldPositionStays: false)` internally, so the caller never deals with raw parent/child manipulation.

Containers (`UiPanel`, `UiStack`, `UiDockPanel`, `UiModal`) expose a `ContentParent` / `ContentTransform` property — the actual `Transform` where children are placed. For simple widgets (`UiLabel`, `UiButton`), there is no `.Add()`. For containers, `.Add()` always adds to the right place. `UiSplit` and `UiScreenSwitcher` are standalone helpers, not `UiElement<TSelf>` containers.

## Layout philosophy — sizing

### `LayoutElement` and layout groups

Sizing is expressed via Unity's built-in `LayoutElement` component (a hints system that `LayoutGroup`s read from) rather than direct `RectTransform` manipulation. This makes sizes **relative and responsive**:

- `Grow(weight)` — takes proportional leftover space inside a layout group parent; useful for spacers and "fill remaining space" patterns
- `FixedWidth(pixels)` / `FixedHeight(pixels)` — reserves a specific pixel size; layout group reads this and reserves space
- `Margin(left, top, right, bottom)` — padding around the element; stored on `LayoutElement` as `layoutGroup.spacingX/spacingY`

Inside a `UiStack` (vertical or horizontal layout group), these sizing hints **mean something**. Inside a `UiSplit` (weight-driven grid), sizing instead comes from the column/row weights passed to `UiSplit` at construction; `Grow`/`FixedWidth`/`FixedHeight` are simply inert (not an error, just ignored).

A scrollable `UiStack` is the exception to "the container grows to fit children": its outer viewport does **not** self-fit. It only fills a size the parent already assigned. Nesting that viewport inside another fitter or `ScrollRect` is how inspectors go blank — see [conventions.md](conventions.md) ("One scroll viewport").

### Inside vs. outside

The **key rule**: sizing methods only work inside a `LayoutGroup` parent. For a standalone `UiPanel` centered on-screen with margins, you pass a `region` parameter (`Rect(x, y, width, height)` as screen fractions) directly to `.Create()` — those fractions are set directly on `RectTransform.anchorMin/anchorMax`, not on `LayoutElement`, so the panel sits at that exact screen position.

This asymmetry exists because:
- Multiple sibling panels (like `LokrCharacterLab`'s inspector/timeline/viewport) need weight-driven layout — `UiSplit` is the right tool.
- A single centered modal needs absolute placement — `region` parameters are simpler than "wrap it in a dummy `UiSplit` with fractional columns just to position it."

## Hierarchy — what goes where

A typical `LokrCharacterLab` editor scene builds as:

```
Canvas (world-space, on the scene's additive Scene)
├── UiPanel (background, fills screen)
│   └── Content (the panel's ContentParent)
│       └── UiStack (vertical)
│           ├── MenuBarPanel (a custom hand-built panel, see LokrCharacterLab docs)
│           ├── UiStack (horizontal, main row — three columns)
│           │   ├── SceneTreePanel (left sidebar)
│           │   ├── MainViewport (center — the rig editor)
│           │   └── InspectorPanel (right sidebar)
│           └── TimelinePanel (bottom row)
```

`SimpleUI` builds the outer shell (`Canvas`, root `UiPanel`, the main flow `UiStack`); `LokrCharacterLab` then adds its own custom panels (which are non-`SimpleUI` hand-built `MonoBehaviour`s) as children.

## Theme inheritance

Every `UiElement<TSelf>` receives a `UiTheme` at construction:

```csharp
public UiButton Create(Transform parent, string label, UiTheme theme = null)
{
    theme = theme ?? UiTheme.Default;
    // ... uses theme.ButtonBackground, theme.TextColor, theme.FontSize, etc.
}
```

`UiTheme.Default` is a static, immutable theme with sensible defaults. Plugins can instantiate their own `UiTheme` (colors, fonts, spacing) and pass it through — all widgets in that tree then inherit its styling without individual configuration.

Inside `LokrCharacterLab`, a single `UiTheme` instance is created once per session and passed to every widget, keeping the entire editor's UI visually consistent.

## No magic, no hidden state

The library does **not**:
- Automatically resize panels based on content (the caller owns sizing)
- Persist state to config files (the caller owns serialization if needed)
- Drive animations or tweens (the caller adds their own animation logic)
- Manage focus/input routing (uses standard Unity/`EventSystem` only)
- Provide a navigation system (the caller owns "which panel is active now")

It provides low-level layout primitives. `LokrCharacterLab` and other consumers build higher-level UI systems on top of these primitives.

## Docking primitives (Phase 1)

`UiDockSpace` is the dockable-container primitive the editor redesign calls for by name: four named zones (Left / Center / Right / Bottom), each a `UiTabGroup` of `UiDockPanel`s. Dragging a tab redocks it into another zone. There is **no floating/undocked state**.

`UiSplitter` is the interactive counterpart to `UiSplit`: `UiSplit`'s weights are fixed at `Create()`, while `UiSplitter` reports a live 0–1 fraction the consumer applies. `UiDockSpace` uses that callback (and does not auto-write a file). `CaptureLayout` / `ApplyLayout` expose a serializable snapshot (`DockLayoutSnapshot`) the *consumer* persists — the same "no hidden state" rule as everything else in this library.

`UiTree`, `UiContextMenu`, `UiToolbar`, and `UiStatusBar` are the other Phase 1 widgets: a generic indented tree, a right-click popup, a tool-button strip, and a status strip. They are independently usable outside a dock space. The Lab shell is the in-game consumer. See [`classes.md`](classes.md) and [`../docs/roadmaps/started/editor-redesign.md`](../../docs/roadmaps/started/editor-redesign.md) §5.1.
