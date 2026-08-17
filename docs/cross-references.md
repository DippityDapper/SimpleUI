# SimpleUI — Cross-References

## Base-game UI conventions

This library is built on top of standard Unity uGUI (Canvas system) and follows its conventions:

- **`RectTransform`** — the 2D layout component; every UI widget has one. Controlled via `anchorMin/anchorMax` (positions as screen fractions) and `offsetMin/offsetMax` (pixel offsets).
- **`LayoutGroup`** — automatic child-sizing component. `SimpleUI` uses `VerticalLayoutGroup` (for `UiStack` with `UiOrientation.Vertical`) and `HorizontalLayoutGroup` (for horizontal stacks).
- **`LayoutElement`** — size hints for layout groups. `SimpleUI` widgets set `preferredWidth/preferredHeight` (pixel sizes), `flexibleWidth/flexibleHeight` (weights for `.Grow()`), and `minWidth/minHeight` (minimum sizes).
- **`GraphicRaycaster`** — raycasting component on the Canvas that determines which UI elements respond to mouse clicks. `SimpleUI` doesn't add any custom raycasting — it uses the built-in raycaster, so click order is determined by Canvas sort order (typically the topmost/last-rendered element gets the click).
- **`EventSystem`** — the global input handler. Every scene with interactive UI needs an `EventSystem` + `StandaloneInputModule`. `SimpleUI` doesn't create these (the caller does, typically once per scene), but all widgets depend on them existing.

## `LokrCharacterLab` usage

`SimpleUI` is designed to replace the hand-written UI boilerplate that was scattered throughout `LokrCharacterLab`'s editor code. Specific patterns replaced:

### Before (hand-written)
```csharp
GameObject panelObj = new GameObject("InspectorPanel", typeof(Image));
panelObj.transform.SetParent(canvas.transform, false);
RectTransform rect = panelObj.GetComponent<RectTransform>();
rect.anchorMin = Vector2.zero;
rect.anchorMax = new Vector2(1, 1);
Image image = panelObj.GetComponent<Image>();
image.color = new Color(0.2f, 0.2f, 0.2f);
// ... repeat for every panel, button, label
```

### After (SimpleUI)
```csharp
var panel = UiPanel.Create(canvas.transform, myTheme);
panel.Add(UiLabel.Create(panel.ContentParent, "Inspector"));
panel.Add(UiButton.Create(panel.ContentParent, "Save").OnClick(() => Save()));
```

The boilerplate (`new GameObject`, `GetComponent`, `SetParent`, `anchorMin/Max` setup) is now hidden inside `SimpleUI`'s `.Create()` methods.

`LokrLab`'s shell Inspector (`InspectorDock`) is the main consumer of scrollable stacks. Its three hosts (`drawerHost`, `propertiesHost`, `animatorHost`) are the `ScrollRect`s; Ability / Animator / Properties forms build into them with `scrollable: false`. Nesting a second scroll there blanks the inspector — see [conventions.md](conventions.md) ("One scroll viewport") and `LokrLab/docs/conventions.md`.

### Shared UI-construction helpers

`LokrCharacterLab` originally had its own `CharacterLabScene.CreateLabel()`/`CreateButton()`/`CreateInputField()` and a parallel `Editor/EditorUiHelpers.cs` helper set for top-level and within-panel controls, respectively. Both are gone as of 2026-08-13 (pre-redesign audit C-UI-01) — every real caller had already migrated onto `SimpleUI` widgets, so the hand-rolled helpers were dead code. The small number of canvas-level chrome elements that need an absolute anchor point rather than layout-group placement now use `UiLabel.Create`/`UiButton.Create` and set `RectTransform.anchorMin`/`anchorMax`/`sizeDelta` directly afterward. See `architecture.md` in `LokrCharacterLab`'s docs for the pattern.

## Dependency graph

```
SimpleUI (passive library, no Harmony patches, no dependencies except BepInEx + Unity)
  ↑
  │ (referenced by)
LokrCharacterLab
```

No other BepInEx plugin in this solution uses `SimpleUI` yet. Any future plugin that builds in-game UI should reference `SimpleUI.dll` and use its classes directly.

## Editor vs. runtime

`SimpleUI` works in both the `LokrCharacterLab` editor (a custom in-game scene) and general runtime scenarios (a mod that adds an in-game menu, etc.). The library is **not** a visual editor for designing UIs — it's a factory API for building them in code. If a plugin wants a visually-designed, drag-and-drop UI layout, it would use prefabs + manual `GameObject.Instantiate()`, not `SimpleUI`.

## No integration with game systems

`SimpleUI` is deliberately isolated from game-domain logic:

- No references to `Ironhide.Legends.*` namespaces
- No patching of base-game UI methods
- No save/load serialization (the caller owns that)
- No automatic focus management or keyboard navigation (beyond what vanilla Unity provides)

It's a pure UI abstraction layer, useful for any plugin that needs to build UI, whether that's an editor (like `LokrCharacterLab`), a mod menu, or an in-game overlay. The game's own UI classes (`UIMainMenu`, `UIHeroManage`, etc.) are untouched.

## `Text` vs. `TextMeshPro`

`SimpleUI` uses Unity's legacy **`Text`** component for all text rendering (`UiLabel`, `UiButton` labels, `UiTextField`, etc.), not `TextMeshProUGUI`. That matches the widgets that were migrated off `LokrCharacterLab`'s old hand-rolled helpers, and avoids a TMP font-asset dependency the library does not currently ship. The base game itself uses TMP on some title-screen buttons; SimpleUI does not.

## Docking and EventSystem drag

Phase 1 docking widgets (`UiSplitter`, `UiTabGroup`, `UiDockSpace`, `UiTree`) implement Unity `IBeginDragHandler` / `IDragHandler` / `IEndDragHandler` on small helper `MonoBehaviour`s (same pattern as `UiImageClickTarget`). They do not add a custom raycaster — click/drag order is still Canvas sort order + `GraphicRaycaster`. The Lab shell and Ability Lab overlays each own their `EventSystem` and temporarily disable foreign ones.

## Color and theme compatibility

The library uses `Color` (rgba floats, 0–1 range) for all theme colors, matching vanilla Unity. No HSV conversions or named color lookups — themes are just explicit `Color` fields. This keeps theme code simple and predictable.

`UiTheme.Default` provides reasonable colors for a dark-themed UI (dark grays for backgrounds, white for text) — suitable for an in-game editor tool. Plugins can easily override it with their own `UiTheme` instance.
