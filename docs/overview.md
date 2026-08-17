# SimpleUI — Overview

A lightweight, fluent-API UI library built on Unity's `uGUI` (canvas system) — provides a set of composable, chainable building blocks for constructing complex layouts without the boilerplate of hand-written `GameObject`/`RectTransform`/`LayoutElement` setup. Designed as a passive shared library (no Harmony patches, no runtime behavior beyond providing factory methods); other plugins reference it and call its static/factory APIs directly.

Built primarily to serve `LokrLab`'s editor UI — but designed as a general-purpose primitive so any future plugin needing to build in-game UI can reuse it rather than reimplementing these same patterns. Version 1.1.0 adds the Phase 1 docking set (`UiSplitter`, `UiTabGroup`, `UiDockPanel`, `UiDockSpace`, `UiTree`, `UiContextMenu`, `UiToolbar`, `UiStatusBar`). Version 1.2.0 adds `UiFileBrowser` (Dolphin-like in-game file picker). Version 1.2.1 removes the Phase 1 `DockingSmokeTest` overlay. Version 1.2.2 adds Proton host-filesystem Places (Linux Home / Pictures / Root via `Z:\`). Version 1.2.3 fixes `UiStack` scroll-viewport sizing (no `sizeDelta` fallback on stretch-anchored viewports). Version 1.2.4 adds `UiToolbar.AddLabel`. Version 1.2.5 sizes `UiContextMenu` to its longest label instead of a fixed 200px panel. Version 1.2.6 rebuilds that menu without keeping the previous dropdown's height. Version 1.2.7 adds `UiCatalogue` (searchable image / name / id cards) and implements `UiTextField.OnChange`. Version 1.2.8 loads catalogue cards in scroll batches. Version 1.2.9 adds `UiCatalogue.OnDropped`. Version 1.2.10 lets a card take the drag once it leaves the list so ScrollRect cannot swallow a place gesture. Version 1.2.11 shows a cursor ghost (thumbnail + name) while that drag is out of the list.

## In this folder

- [`overview.md`](overview.md) — this file
- [`layout.md`](layout.md) — file structure and namespace organization
- [`architecture.md`](architecture.md) — the fluent CRTP pattern, composition model, and sizing philosophy
- [`classes.md`](classes.md) — every UI widget class and its configuration options
- [`conventions.md`](conventions.md) — naming patterns, pixel vs. canvas-fraction layout, theming
- [`cross-references.md`](cross-references.md) — base-game UI conventions and `LokrLab` usage patterns

## Plugin metadata

`SimpleUIPlugin.cs`: `Guid = "com.lokrmodding.simpleui"`,
`Name = "LoKR Simple UI"`, `Version = "1.2.11"`. No `[BepInDependency]` — a library plugin. `Awake()` logs plugin load and caches the logger. There are no Harmony patches.

## Quick example

```csharp
// Typical usage from LokrLab
var panel = UiPanel.Create(canvas.transform, theme: MyTheme, title: "My Editor Panel");
var stack = UiStack.Create(panel.ContentParent, FlowAxis.Vertical)
    .Spacing(5f);

stack.Add(UiLabel.Create(stack.transform, "Character Name:")
    .FixedHeight(25f));

stack.Add(UiTextField.Create(stack.transform, "MyCharacter")
    .Grow()
    .OnChange(text => Debug.Log($"Changed to {text}")));

stack.Add(UiButton.Create(stack.transform, "Save")
    .FixedHeight(30f)
    .OnClick(() => Debug.Log("Clicked!")));

UiFileBrowser.PickFile(canvas, "Select a PNG", startPath, new[] { ".png" }, path => { });
```

Chains methods fluently; composable via `.Add()` to nest widgets; sizing driven by `LayoutElement` hints and parent layout groups rather than hardcoded pixel positions.
