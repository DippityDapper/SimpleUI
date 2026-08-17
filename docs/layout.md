# SimpleUI — Layout

## Folder structure

```
SimpleUI/
├── SimpleUI.csproj
├── SimpleUIPlugin.cs          ← BepInPlugin entry point, logger cache
├── UiElement.cs               ← base class hierarchy (non-generic + generic)
├── UiLayoutUtil.cs            ← internal RectTransform/canvas helpers
├── UiPanel.cs                 ← background box with optional title
├── UiStack.cs                 ← row/column layout group (UiOrientation)
├── UiSplit.cs                 ← weight-driven multi-column/row layout (fixed at Create)
├── UiSplitter.cs              ← draggable divider; reports a 0-1 fraction
├── UiScreenSwitcher.cs        ← show/hide one of N child panels by name
├── UiTabGroup.cs              ← visible, reorderable tab strip
├── UiDockPanel.cs             ← titled panel for a dock zone
├── UiDockSpace.cs             ← Left/Right/Bottom/Center dockable container
├── UiLabel.cs                 ← text display (UnityEngine.UI.Text)
├── UiButton.cs                ← clickable button
├── UiToggle.cs                ← checkbox toggle
├── UiTextField.cs             ← text input field
├── UiImage.cs                 ← sprite/texture display
├── UiDropdown.cs              ← dropdown selector (one-of-many)
├── UiComboBox.cs              ← editable field + known-options dropdown
├── UiCatalogue.cs             ← searchable image / name / id card list
├── UiCatalogueDragGhost.cs    ← cursor ghost while a card is pulled out of the list
├── UiCatalogueItem.cs         ← one catalogue row (id, name, sprite)
├── UiList.cs                  ← generic key-diffing row list (UiList<T>)
├── UiTree.cs                  ← indented tree (expand/collapse, multi-select, reparent)
├── UiContextMenu.cs           ← right-click popup
├── UiToolbar.cs               ← horizontal button strip with separators
├── UiStatusBar.cs             ← status text + right-aligned indicator
├── UiModal.cs                 ← centered dialog box with backdrop
├── UiFileBrowser.cs           ← Dolphin-like in-game file browser
├── UiFileBrowserHostPaths.cs  ← Proton/Wine host Linux home + Z:\
├── UiFileBrowserTypes.cs      ← mode, sort, and Places types
└── UiTheme.cs                 ← color/font/spacing constants
```

## Namespace

All classes live in the `SimpleUI` namespace. No sub-namespaces — a flat, single-namespace design to minimize `using` clutter and keep the API surface obvious.

## Dependency graph

```
BepInEx.BaseUnityPlugin
  ↓
SimpleUIPlugin

UiElement (non-generic base)
  ↓
UiElement<TSelf> (generic CRTP base — every concrete widget)
  ├─ UiPanel
  ├─ UiStack (layout group)
  ├─ UiSplitSlot (one cell of a UiSplit)
  ├─ UiSplitter (draggable divider)
  ├─ UiTabGroup
  ├─ UiDockPanel
  ├─ UiDockSpace
  ├─ UiLabel
  ├─ UiButton
  ├─ UiToggle
  ├─ UiTextField
  ├─ UiImage
  ├─ UiDropdown
  ├─ UiComboBox
  ├─ UiCatalogue
  ├─ UiList<T>
  ├─ UiTree
  ├─ UiContextMenu
  ├─ UiToolbar
  ├─ UiStatusBar
  ├─ UiModal
  └─ UiFileBrowser

Standalone (not UiElement<TSelf>):
  UiSplit, UiScreenSwitcher, DockLayoutSnapshot / DockZoneSnapshot / DockZone

UiTheme (immutable color/font/spacing store)
  ↓ (referenced by every UiElement<TSelf>)
```

Every concrete widget (`UiButton`, `UiStack`, etc.) is a `UiElement<TSelf>` and receives a `UiTheme` at construction — fonts, colors, and default spacing follow from the theme rather than being hardcoded.

No plugin-to-plugin cross-dependencies here (other than SimpleUI depending on Unity/BepInEx). `LokrCharacterLab` is the primary consumer; any other plugin that wants to build UI can reference this assembly independently.
