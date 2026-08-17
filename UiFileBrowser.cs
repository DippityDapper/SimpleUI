using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>In-game file browser with Dolphin-like places, history, details, filter, and file operations.</summary>
	/// <remarks>
	/// Embeddable via Create, or opened as a shared modal via PickFile / PickFolder. Does not use
	/// native OS dialogs — Unity's Mono here has no desktop file picker. File operations stay in
	/// the current process (copy/cut/paste, new folder, rename, delete with confirm).
	/// </remarks>
	public sealed class UiFileBrowser : UiElement<UiFileBrowser>
	{
		private const string BookmarkPrefsKey = "SimpleUI.FileBrowser.Bookmarks";
		private const float PlaceWidth = 200f;
		private const float PreviewWidth = 220f;
		private const float RowHeight = 26f;

		private static UiModal sharedModal;
		private static UiFileBrowser sharedBrowser;

		private readonly UiLabel titleLabel;
		private readonly UiButton backButton;
		private readonly UiButton forwardButton;
		private readonly UiButton upButton;
		private readonly UiTextField pathField;
		private readonly UiStack crumbRow;
		private readonly UiTextField filterField;
		private readonly UiToggle hiddenToggle;
		private readonly UiList<UiFileBrowserPlace> placeList;
		private readonly UiButton nameHeader;
		private readonly UiButton sizeHeader;
		private readonly UiButton typeHeader;
		private readonly UiButton modifiedHeader;
		private readonly UiList<Entry> fileList;
		private readonly UiImage previewImage;
		private readonly UiLabel previewLabel;
		private readonly UiStatusBar status;
		private readonly UiTextField promptField;
		private readonly UiButton promptOk;
		private readonly UiStack promptRow;
		private readonly UiContextMenu contextMenu;
		private readonly List<string> backStack = new List<string>();
		private readonly List<string> forwardStack = new List<string>();
		private readonly List<UiFileBrowserPlace> extraPlaces = new List<UiFileBrowserPlace>();
		private readonly List<Entry> directoryEntries = new List<Entry>();
		private readonly List<string> clipboardPaths = new List<string>();

		private UiFileBrowserMode mode = UiFileBrowserMode.OpenFolder;
		private string[] extensions;
		private string currentPath;
		private string selectedPath;
		private string filterText = string.Empty;
		private bool showHidden;
		private UiFileBrowserSort sort = UiFileBrowserSort.Name;
		private bool sortAscending = true;
		private bool clipboardCut;
		private PromptKind promptKind;
		private string promptTargetPath;
		private Action<string> onSelected;
		private Action onCancelled;
		private Texture2D previewTexture;
		private Sprite previewSprite;

		private enum PromptKind
		{
			None,
			NewFolder,
			Rename,
			Delete
		}

		private sealed class Entry
		{
			internal string FullPath;
			internal string Name;
			internal bool IsDirectory;
			internal long Size;
			internal DateTime Modified;
			internal string TypeLabel;
		}

		private sealed class Host : UiElement<Host>
		{
			internal Host(GameObject gameObject, UiTheme theme) : base(gameObject, theme)
			{
			}
		}

		private UiFileBrowser(
			GameObject gameObject,
			UiTheme theme,
			UiLabel titleLabel,
			UiButton backButton,
			UiButton forwardButton,
			UiButton upButton,
			UiTextField pathField,
			UiStack crumbRow,
			UiTextField filterField,
			UiToggle hiddenToggle,
			UiList<UiFileBrowserPlace> placeList,
			UiButton nameHeader,
			UiButton sizeHeader,
			UiButton typeHeader,
			UiButton modifiedHeader,
			UiList<Entry> fileList,
			UiImage previewImage,
			UiLabel previewLabel,
			UiStatusBar status,
			UiTextField promptField,
			UiButton promptOk,
			UiStack promptRow,
			UiContextMenu contextMenu)
			: base(gameObject, theme)
		{
			this.titleLabel = titleLabel;
			this.backButton = backButton;
			this.forwardButton = forwardButton;
			this.upButton = upButton;
			this.pathField = pathField;
			this.crumbRow = crumbRow;
			this.filterField = filterField;
			this.hiddenToggle = hiddenToggle;
			this.placeList = placeList;
			this.nameHeader = nameHeader;
			this.sizeHeader = sizeHeader;
			this.typeHeader = typeHeader;
			this.modifiedHeader = modifiedHeader;
			this.fileList = fileList;
			this.previewImage = previewImage;
			this.previewLabel = previewLabel;
			this.status = status;
			this.promptField = promptField;
			this.promptOk = promptOk;
			this.promptRow = promptRow;
			this.contextMenu = contextMenu;
		}

		/// <summary>Directory currently listed.</summary>
		public string CurrentPath => currentPath;

		/// <summary>Last selected file or folder path, or null.</summary>
		public string SelectedPath => selectedPath;

		/// <summary>Current confirm mode.</summary>
		public UiFileBrowserMode Mode => mode;

		/// <summary>Builds an embeddable browser that fills its parent.</summary>
		public static UiFileBrowser Create(Transform parent, UiTheme theme = null)
		{
			if (parent == null)
			{
				throw new ArgumentNullException(nameof(parent));
			}

			theme = theme ?? UiTheme.Default;

			GameObject root = new GameObject("UiFileBrowser", typeof(Image));
			root.transform.SetParent(parent, false);
			UiLayoutUtil.Stretch(root.GetComponent<RectTransform>());
			root.GetComponent<Image>().color = theme.PanelBackground;

			UiStack column = UiStack.Vertical(root.transform, theme, spacing: 6f, padding: 8f);

			UiLabel title = UiLabel.Create(column.ContentTransform, "Browse", theme, theme.TitleFontSize);
			column.Add(title.FixedHeight(24f));

			UiStack toolbar = UiStack.Horizontal(column.ContentTransform, theme, spacing: 6f, padding: 0f);
			column.Add(toolbar.FixedHeight(theme.ControlHeight));
			UiButton back = UiButton.Create(toolbar.ContentTransform, "Back", null, theme, false).FixedWidth(70f);
			toolbar.Add(back);
			UiButton forward = UiButton.Create(toolbar.ContentTransform, "Fwd", null, theme, false).FixedWidth(70f);
			toolbar.Add(forward);
			UiButton up = UiButton.Create(toolbar.ContentTransform, "Up", null, theme, false).FixedWidth(70f);
			toolbar.Add(up);
			UiButton reload = UiButton.Create(toolbar.ContentTransform, "Reload", null, theme, false).FixedWidth(80f);
			toolbar.Add(reload);
			UiTextField filter = UiTextField.Create(toolbar.ContentTransform, string.Empty, theme);
			filter.Grow();
			toolbar.Add(filter);
			UiToggle hidden = UiToggle.Create(toolbar.ContentTransform, "Hidden", false, theme).FixedWidth(90f);
			toolbar.Add(hidden);

			UiTextField path = UiTextField.Create(column.ContentTransform, string.Empty, theme);
			column.Add(path.FixedHeight(theme.ControlHeight));

			UiStack crumbs = UiStack.Horizontal(column.ContentTransform, theme, spacing: 4f, padding: 0f, scrollable: true);
			column.Add(crumbs.FixedHeight(28f));

			UiSplit body = UiSplit.Columns(column.ContentTransform, theme,
				ColumnSpec.Fixed(PlaceWidth),
				ColumnSpec.Weighted(1f),
				ColumnSpec.Fixed(PreviewWidth));
			column.Add(WrapSplit(body));

			UiStack placeColumn = UiStack.Vertical(body.Slots[0].GameObject.transform, theme, spacing: 4f, padding: 4f);
			placeColumn.Add(UiLabel.Create(placeColumn.ContentTransform, "Places", theme, theme.BodyFontSize).FixedHeight(20f));
			UiList<UiFileBrowserPlace> places = UiList<UiFileBrowserPlace>.Create(placeColumn.ContentTransform, spacing: 2f, padding: 0f);
			places.Grow();
			placeColumn.Add(places);

			UiStack filesColumn = UiStack.Vertical(body.Slots[1].GameObject.transform, theme, spacing: 2f, padding: 4f);
			UiStack headers = UiStack.Horizontal(filesColumn.ContentTransform, theme, spacing: 4f, padding: 0f);
			filesColumn.Add(headers.FixedHeight(24f));
			UiButton nameHeader = UiButton.Create(headers.ContentTransform, "Name", null, theme, false);
			nameHeader.Grow();
			headers.Add(nameHeader);
			UiButton sizeHeader = UiButton.Create(headers.ContentTransform, "Size", null, theme, false).FixedWidth(80f);
			headers.Add(sizeHeader);
			UiButton typeHeader = UiButton.Create(headers.ContentTransform, "Type", null, theme, false).FixedWidth(70f);
			headers.Add(typeHeader);
			UiButton modifiedHeader = UiButton.Create(headers.ContentTransform, "Modified", null, theme, false).FixedWidth(140f);
			headers.Add(modifiedHeader);
			UiList<Entry> files = UiList<Entry>.Create(filesColumn.ContentTransform, spacing: 1f, padding: 0f);
			files.Grow();
			filesColumn.Add(files);

			UiStack previewColumn = UiStack.Vertical(body.Slots[2].GameObject.transform, theme, spacing: 4f, padding: 4f);
			previewColumn.Add(UiLabel.Create(previewColumn.ContentTransform, "Preview", theme, theme.BodyFontSize).FixedHeight(20f));
			UiImage preview = UiImage.Create(previewColumn.ContentTransform, null, theme);
			preview.Grow();
			previewColumn.Add(preview);
			UiLabel previewCaption = UiLabel.Create(previewColumn.ContentTransform, "Select a file", theme, theme.BodyFontSize, TextAnchor.UpperLeft);
			previewColumn.Add(previewCaption.FixedHeight(48f));

			UiStatusBar status = UiStatusBar.Create(column.ContentTransform, theme);
			column.Add(status.FixedHeight(theme.StatusBarHeight > 0f ? theme.StatusBarHeight : 26f));

			UiStack prompt = UiStack.Horizontal(column.ContentTransform, theme, spacing: 6f, padding: 0f);
			column.Add(prompt.FixedHeight(theme.ControlHeight));
			UiTextField promptField = UiTextField.Create(prompt.ContentTransform, string.Empty, theme);
			promptField.Grow();
			prompt.Add(promptField);
			UiButton promptOk = UiButton.Create(prompt.ContentTransform, "OK", null, theme, true).FixedWidth(80f);
			prompt.Add(promptOk);
			UiButton promptCancel = UiButton.Create(prompt.ContentTransform, "Cancel", null, theme, false).FixedWidth(80f);
			prompt.Add(promptCancel);
			prompt.Visible(false);

			UiStack actions = UiStack.Horizontal(column.ContentTransform, theme, spacing: 8f, padding: 0f);
			column.Add(actions.FixedHeight(theme.ControlHeight));
			UiButton newFolder = UiButton.Create(actions.ContentTransform, "New Folder", null, theme, false).FixedWidth(110f);
			actions.Add(newFolder);
			UiLabel spacer = UiLabel.Create(actions.ContentTransform, string.Empty, theme);
			spacer.Grow();
			actions.Add(spacer);
			UiButton select = UiButton.Create(actions.ContentTransform, "Select", null, theme, true).FixedWidth(100f);
			actions.Add(select);
			UiButton cancel = UiButton.Create(actions.ContentTransform, "Cancel", null, theme, false).FixedWidth(100f);
			actions.Add(cancel);

			Transform canvas = parent;
			Canvas rootCanvas = UiLayoutUtil.FindRootCanvas(parent);
			if (rootCanvas != null)
			{
				canvas = rootCanvas.transform;
			}

			UiContextMenu menu = UiContextMenu.Create(canvas, theme);

			UiFileBrowser browser = new UiFileBrowser(
				root, theme, title, back, forward, up, path, crumbs, filter, hidden, places,
				nameHeader, sizeHeader, typeHeader, modifiedHeader, files, preview, previewCaption,
				status, promptField, promptOk, prompt, menu);

			back.OnClick(browser.GoBack);
			forward.OnClick(browser.GoForward);
			up.OnClick(browser.GoUp);
			reload.OnClick(browser.Reload);
			path.OnEndEdit(browser.OnPathCommitted);
			filter.InputField.onValueChanged.AddListener(browser.OnFilterChanged);
			hidden.OnValueChanged(browser.OnHiddenChanged);
			nameHeader.OnClick(() => browser.ToggleSort(UiFileBrowserSort.Name));
			sizeHeader.OnClick(() => browser.ToggleSort(UiFileBrowserSort.Size));
			typeHeader.OnClick(() => browser.ToggleSort(UiFileBrowserSort.Type));
			modifiedHeader.OnClick(() => browser.ToggleSort(UiFileBrowserSort.Modified));
			promptOk.OnClick(browser.CommitPrompt);
			promptCancel.OnClick(browser.HidePrompt);
			newFolder.OnClick(() => browser.BeginPrompt(PromptKind.NewFolder, null));
			select.OnClick(browser.Confirm);
			cancel.OnClick(browser.Cancel);

			UiFileBrowserInput input = root.AddComponent<UiFileBrowserInput>();
			input.Bind(browser);
			return browser;
		}

		/// <summary>Host Linux home or Pictures when running under Proton; otherwise null.</summary>
		public static string HostStartDirectory => UiFileBrowserHostPaths.PreferredStartDirectory();

		/// <summary>Opens the shared modal to pick a file. extensions are lowercase with a leading dot.</summary>
		public static void PickFile(Transform canvas, string title, string startPath, string[] extensions, Action<string> onSelected, IEnumerable<UiFileBrowserPlace> extraPlaces = null)
		{
			UiFileBrowser browser = EnsureModal(canvas);
			if (browser == null)
			{
				return;
			}

			browser.SetMode(UiFileBrowserMode.OpenFile)
				.SetTitle(string.IsNullOrEmpty(title) ? "Select a file" : title)
				.SetExtensions(extensions)
				.SetPlaces(extraPlaces)
				.OnSelected(onSelected)
				.OnCancelled(null)
				.Navigate(ResolveStartDirectory(startPath));
			sharedModal.Show();
		}

		/// <summary>Opens the shared modal to pick a folder.</summary>
		public static void PickFolder(Transform canvas, string title, string startPath, Action<string> onSelected, IEnumerable<UiFileBrowserPlace> extraPlaces = null)
		{
			UiFileBrowser browser = EnsureModal(canvas);
			if (browser == null)
			{
				return;
			}

			browser.SetMode(UiFileBrowserMode.OpenFolder)
				.SetTitle(string.IsNullOrEmpty(title) ? "Select a folder" : title)
				.SetExtensions(null)
				.SetPlaces(extraPlaces)
				.OnSelected(onSelected)
				.OnCancelled(null)
				.Navigate(ResolveStartDirectory(startPath));
			sharedModal.Show();
		}

		/// <summary>Builds the shared modal on canvas if it is missing or was destroyed.</summary>
		public static UiFileBrowser EnsureModal(Transform canvas)
		{
			if (sharedBrowser != null && sharedBrowser.GameObject != null && sharedModal != null && sharedModal.GameObject != null)
			{
				return sharedBrowser;
			}

			if (canvas == null)
			{
				return null;
			}

			sharedModal = UiModal.Create(canvas, UiTheme.Default, null, 1280f, 800f);
			sharedBrowser = Create(sharedModal.ContentParent);
			sharedBrowser.Grow();
			sharedModal.Add(sharedBrowser);
			return sharedBrowser;
		}

		/// <summary>Drops the shared modal refs when the host scene is destroyed.</summary>
		public static void ReleaseModal()
		{
			if (sharedBrowser != null)
			{
				sharedBrowser.DisposePreview();
			}

			sharedModal = null;
			sharedBrowser = null;
		}

		/// <summary>Sets whether Select confirms a file or a folder.</summary>
		public UiFileBrowser SetMode(UiFileBrowserMode value)
		{
			mode = value;
			return this;
		}

		/// <summary>Replaces the title label.</summary>
		public UiFileBrowser SetTitle(string title)
		{
			titleLabel.SetText(title ?? "Browse");
			return this;
		}

		/// <summary>Restricts visible files to these extensions (lowercase, leading dot). Null or empty shows every file.</summary>
		public UiFileBrowser SetExtensions(params string[] values)
		{
			if (values == null || values.Length == 0)
			{
				extensions = null;
				return this;
			}

			extensions = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				string value = values[i] ?? string.Empty;
				extensions[i] = value.StartsWith(".") ? value.ToLowerInvariant() : "." + value.ToLowerInvariant();
			}

			return this;
		}

		/// <summary>Replaces caller-supplied Places (merged with Home / drives / bookmarks on the next refresh).</summary>
		public UiFileBrowser SetPlaces(IEnumerable<UiFileBrowserPlace> places)
		{
			extraPlaces.Clear();
			if (places == null)
			{
				return this;
			}

			foreach (UiFileBrowserPlace place in places)
			{
				if (place != null)
				{
					extraPlaces.Add(place);
				}
			}

			return this;
		}

		/// <summary>Replaces the confirm callback.</summary>
		public UiFileBrowser OnSelected(Action<string> callback)
		{
			onSelected = callback;
			return this;
		}

		/// <summary>Replaces the cancel callback.</summary>
		public UiFileBrowser OnCancelled(Action callback)
		{
			onCancelled = callback;
			return this;
		}

		/// <summary>Lists directory, recording history unless this is a back/forward move.</summary>
		public UiFileBrowser Navigate(string path)
		{
			return Navigate(path, recordHistory: true);
		}

		/// <summary>Confirms the current selection (or the current folder in folder mode).</summary>
		public void Confirm()
		{
			string chosen = mode == UiFileBrowserMode.OpenFolder
				? (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath) ? selectedPath : currentPath)
				: selectedPath;
			if (string.IsNullOrEmpty(chosen))
			{
				status.SetText(mode == UiFileBrowserMode.OpenFile ? "Select a file first." : "No folder selected.");
				return;
			}

			if (mode == UiFileBrowserMode.OpenFile && !File.Exists(chosen))
			{
				status.SetText("Select a file first.");
				return;
			}

			Action<string> callback = onSelected;
			HideSharedModalIfOwner();
			callback?.Invoke(chosen);
		}

		/// <summary>Closes without confirming.</summary>
		public void Cancel()
		{
			HideSharedModalIfOwner();
			onCancelled?.Invoke();
		}

		private void HideSharedModalIfOwner()
		{
			if (this == sharedBrowser && sharedModal != null && sharedModal.GameObject != null)
			{
				sharedModal.Hide();
			}
		}

		internal void HandleKey(KeyCode key, bool alt, bool control)
		{
			if (!GameObject.activeInHierarchy)
			{
				return;
			}

			if (IsTyping())
			{
				if (key == KeyCode.Escape)
				{
					HidePrompt();
				}

				return;
			}

			if (key == KeyCode.Escape)
			{
				if (promptKind != PromptKind.None)
				{
					HidePrompt();
					return;
				}

				Cancel();
				return;
			}

			if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
			{
				ActivateSelected();
				return;
			}

			if (key == KeyCode.Backspace)
			{
				GoUp();
				return;
			}

			if (alt && key == KeyCode.LeftArrow)
			{
				GoBack();
				return;
			}

			if (alt && key == KeyCode.RightArrow)
			{
				GoForward();
				return;
			}

			if (key == KeyCode.F5)
			{
				Reload();
				return;
			}

			if (key == KeyCode.F2)
			{
				BeginPrompt(PromptKind.Rename, selectedPath);
				return;
			}

			if (key == KeyCode.Delete)
			{
				BeginPrompt(PromptKind.Delete, selectedPath);
				return;
			}

			if (control && key == KeyCode.L)
			{
				if (EventSystem.current != null)
				{
					EventSystem.current.SetSelectedGameObject(pathField.GameObject);
				}

				return;
			}

			if (control && key == KeyCode.F)
			{
				if (EventSystem.current != null)
				{
					EventSystem.current.SetSelectedGameObject(filterField.GameObject);
				}

				return;
			}

			if (control && key == KeyCode.H)
			{
				hiddenToggle.Toggle.isOn = !hiddenToggle.Toggle.isOn;
				return;
			}

			if (control && key == KeyCode.C)
			{
				CopySelection(cut: false);
				return;
			}

			if (control && key == KeyCode.X)
			{
				CopySelection(cut: true);
				return;
			}

			if (control && key == KeyCode.V)
			{
				PasteClipboard();
			}
		}

		private UiFileBrowser Navigate(string path, bool recordHistory)
		{
			string resolved = ResolveExistingDirectory(path);
			if (resolved == null)
			{
				status.SetText("Not a valid folder: " + path);
				return this;
			}

			if (recordHistory && !string.IsNullOrEmpty(currentPath)
				&& !string.Equals(currentPath, resolved, StringComparison.OrdinalIgnoreCase))
			{
				backStack.Add(currentPath);
				forwardStack.Clear();
			}

			currentPath = resolved;
			selectedPath = null;
			pathField.SetText(resolved);
			Reload();
			return this;
		}

		private void Reload()
		{
			directoryEntries.Clear();
			if (string.IsNullOrEmpty(currentPath))
			{
				return;
			}

			try
			{
				foreach (string directory in Directory.GetDirectories(currentPath))
				{
					TryAddEntry(directory, isDirectory: true);
				}

				foreach (string file in Directory.GetFiles(currentPath))
				{
					if (!MatchesExtension(file))
					{
						continue;
					}

					TryAddEntry(file, isDirectory: false);
				}
			}
			catch (Exception ex)
			{
				status.SetText("Can't list " + currentPath + ": " + ex.Message);
			}

			RefreshPlaces();
			RefreshCrumbs();
			RefreshList();
			RefreshChrome();
			RefreshPreview();
		}

		private void TryAddEntry(string path, bool isDirectory)
		{
			try
			{
				FileSystemInfo info = isDirectory ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
				if (!showHidden && IsHidden(info))
				{
					return;
				}

				Entry entry = new Entry
				{
					FullPath = path,
					Name = info.Name,
					IsDirectory = isDirectory,
					Size = isDirectory ? 0L : ((FileInfo)info).Length,
					Modified = info.LastWriteTime,
					TypeLabel = isDirectory ? "Folder" : TypeLabelFor(path)
				};
				directoryEntries.Add(entry);
			}
			catch (Exception)
			{
			}
		}

		private void RefreshList()
		{
			List<Entry> visible = new List<Entry>();
			for (int i = 0; i < directoryEntries.Count; i++)
			{
				Entry entry = directoryEntries[i];
				if (!string.IsNullOrEmpty(filterText)
					&& entry.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				visible.Add(entry);
			}

			visible.Sort(CompareEntries);
			fileList.SetItems(visible, entry => entry.FullPath, BuildFileRow);
			UpdateRowColors();
			status.SetText(visible.Count + " item(s)"
				+ (string.IsNullOrEmpty(selectedPath) ? string.Empty : " — " + Path.GetFileName(selectedPath)));
			status.SetRightText(currentPath ?? string.Empty);
		}

		private int CompareEntries(Entry left, Entry right)
		{
			int folderOrder = right.IsDirectory.CompareTo(left.IsDirectory);
			if (folderOrder != 0)
			{
				return folderOrder;
			}

			int compared;
			switch (sort)
			{
				case UiFileBrowserSort.Size:
					compared = left.Size.CompareTo(right.Size);
					break;
				case UiFileBrowserSort.Type:
					compared = string.Compare(left.TypeLabel, right.TypeLabel, StringComparison.OrdinalIgnoreCase);
					break;
				case UiFileBrowserSort.Modified:
					compared = left.Modified.CompareTo(right.Modified);
					break;
				default:
					compared = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
					break;
			}

			if (compared == 0)
			{
				compared = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
			}

			return sortAscending ? compared : -compared;
		}

		private UiElement BuildFileRow(Transform parent, Entry entry)
		{
			GameObject row = new GameObject("FileRow", typeof(Image), typeof(LayoutElement));
			row.transform.SetParent(parent, false);
			row.GetComponent<Image>().color = Theme.RowButtonColor;
			LayoutElement layout = row.GetComponent<LayoutElement>();
			layout.minHeight = RowHeight;
			layout.preferredHeight = RowHeight;
			layout.flexibleWidth = 1f;

			UiFileBrowserRowClick click = row.AddComponent<UiFileBrowserRowClick>();
			string path = entry.FullPath;
			bool isDirectory = entry.IsDirectory;
			click.OnLeft = () => SelectPath(path);
			click.OnDouble = () => ActivatePath(path, isDirectory);
			click.OnRight = () => ShowContext(path, isDirectory);

			UiStack cells = UiStack.Horizontal(row.transform, Theme, spacing: 4f, padding: 2f);
			string namePrefix = entry.IsDirectory ? "[Dir] " : (IsImage(path) ? "[Img] " : "");
			UiLabel name = UiLabel.Create(cells.ContentTransform, namePrefix + entry.Name, Theme, Theme.BodyFontSize);
			name.Grow();
			cells.Add(name);
			cells.Add(UiLabel.Create(cells.ContentTransform, entry.IsDirectory ? string.Empty : FormatSize(entry.Size), Theme, Theme.BodyFontSize).FixedWidth(80f));
			cells.Add(UiLabel.Create(cells.ContentTransform, entry.TypeLabel, Theme, Theme.BodyFontSize).FixedWidth(70f));
			cells.Add(UiLabel.Create(cells.ContentTransform, entry.Modified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Theme, Theme.BodyFontSize).FixedWidth(140f));
			return new Host(row, Theme);
		}

		private void UpdateRowColors()
		{
			for (int i = 0; i < directoryEntries.Count; i++)
			{
				Entry entry = directoryEntries[i];
				if (!fileList.TryGetRow(entry.FullPath, out GameObject row) || row == null)
				{
					continue;
				}

				Image image = row.GetComponent<Image>();
				if (image != null)
				{
					image.color = string.Equals(entry.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase)
						? Theme.AccentColor
						: Theme.RowButtonColor;
				}
			}
		}

		private void RefreshPlaces()
		{
			List<UiFileBrowserPlace> places = BuildDefaultPlaces();
			for (int i = 0; i < extraPlaces.Count; i++)
			{
				AddUniquePlace(places, extraPlaces[i]);
			}

			placeList.SetItems(places, place => place.Path, BuildPlaceRow);
		}

		private UiElement BuildPlaceRow(Transform parent, UiFileBrowserPlace place)
		{
			string path = place.Path;
			return UiButton.Create(parent, place.Label, () => Navigate(path), Theme, false).FixedHeight(RowHeight);
		}

		private void RefreshCrumbs()
		{
			crumbRow.Clear();
			if (string.IsNullOrEmpty(currentPath))
			{
				return;
			}

			List<UiFileBrowserPlace> segments = SplitBreadcrumbs(currentPath);
			for (int i = 0; i < segments.Count; i++)
			{
				UiFileBrowserPlace segment = segments[i];
				string path = segment.Path;
				string label = i == 0 ? segment.Label : segment.Label + " /";
				crumbRow.Add(UiButton.Create(crumbRow.ContentTransform, label, () => Navigate(path), Theme, false).FixedWidth(Mathf.Clamp(14f * label.Length, 48f, 180f)));
			}
		}

		private void RefreshChrome()
		{
			backButton.Interactable(backStack.Count > 0);
			forwardButton.Interactable(forwardStack.Count > 0);
			upButton.Interactable(Directory.GetParent(currentPath ?? string.Empty) != null);
			nameHeader.SetLabel(HeaderLabel("Name", UiFileBrowserSort.Name));
			sizeHeader.SetLabel(HeaderLabel("Size", UiFileBrowserSort.Size));
			typeHeader.SetLabel(HeaderLabel("Type", UiFileBrowserSort.Type));
			modifiedHeader.SetLabel(HeaderLabel("Modified", UiFileBrowserSort.Modified));
		}

		private string HeaderLabel(string label, UiFileBrowserSort column)
		{
			if (sort != column)
			{
				return label;
			}

			return label + (sortAscending ? " ^" : " v");
		}

		private void RefreshPreview()
		{
			DisposePreview();
			if (string.IsNullOrEmpty(selectedPath) || !File.Exists(selectedPath) || !IsImage(selectedPath))
			{
				previewImage.SetSprite(null);
				previewLabel.SetText(string.IsNullOrEmpty(selectedPath) ? "Select a file" : Path.GetFileName(selectedPath));
				return;
			}

			try
			{
				byte[] bytes = File.ReadAllBytes(selectedPath);
				previewTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				if (!previewTexture.LoadImage(bytes))
				{
					DisposePreview();
					previewLabel.SetText("Can't preview " + Path.GetFileName(selectedPath));
					return;
				}

				previewSprite = Sprite.Create(previewTexture, new Rect(0f, 0f, previewTexture.width, previewTexture.height), new Vector2(0.5f, 0.5f));
				previewImage.SetSprite(previewSprite);
				previewLabel.SetText(Path.GetFileName(selectedPath) + "\n" + previewTexture.width + " x " + previewTexture.height);
			}
			catch (Exception ex)
			{
				DisposePreview();
				previewLabel.SetText(ex.Message);
			}
		}

		private void SelectPath(string path)
		{
			selectedPath = path;
			UpdateRowColors();
			RefreshPreview();
			status.SetText(Path.GetFileName(path));
		}

		private void ActivateSelected()
		{
			if (string.IsNullOrEmpty(selectedPath))
			{
				if (mode == UiFileBrowserMode.OpenFolder)
				{
					Confirm();
				}

				return;
			}

			ActivatePath(selectedPath, Directory.Exists(selectedPath));
		}

		private void ActivatePath(string path, bool isDirectory)
		{
			if (isDirectory)
			{
				Navigate(path);
				return;
			}

			selectedPath = path;
			if (mode == UiFileBrowserMode.OpenFile)
			{
				Confirm();
			}
		}

		private void ShowContext(string path, bool isDirectory)
		{
			SelectPath(path);
			contextMenu.ClearItems();
			contextMenu.AddItem(isDirectory ? "Open" : "Select", () => ActivatePath(path, isDirectory));
			contextMenu.AddItem("Copy Path", () => GUIUtility.systemCopyBuffer = path);
			contextMenu.AddSeparator();
			contextMenu.AddItem("New Folder", () => BeginPrompt(PromptKind.NewFolder, null));
			contextMenu.AddItem("Rename", () => BeginPrompt(PromptKind.Rename, path));
			contextMenu.AddItem("Delete", () => BeginPrompt(PromptKind.Delete, path));
			contextMenu.AddSeparator();
			contextMenu.AddItem("Copy", () => CopySelection(cut: false));
			contextMenu.AddItem("Cut", () => CopySelection(cut: true));
			contextMenu.AddItem("Paste", PasteClipboard, clipboardPaths.Count > 0);
			contextMenu.AddSeparator();
			contextMenu.AddItem("Add to Places", () => Bookmark(isDirectory ? path : Path.GetDirectoryName(path)));
			contextMenu.Show(Input.mousePosition);
		}

		private void BeginPrompt(PromptKind kind, string targetPath)
		{
			if (kind == PromptKind.Rename || kind == PromptKind.Delete)
			{
				if (string.IsNullOrEmpty(targetPath))
				{
					status.SetText("Select an item first.");
					return;
				}
			}

			promptKind = kind;
			promptTargetPath = targetPath;
			promptRow.Visible(true);
			if (kind == PromptKind.Delete)
			{
				promptField.SetText("Delete " + Path.GetFileName(targetPath) + "?");
				promptOk.SetLabel("Delete");
				return;
			}

			promptOk.SetLabel("OK");
			promptField.SetText(kind == PromptKind.Rename ? Path.GetFileName(targetPath) : "New Folder");
			if (EventSystem.current != null)
			{
				EventSystem.current.SetSelectedGameObject(promptField.GameObject);
			}
		}

		private void HidePrompt()
		{
			promptKind = PromptKind.None;
			promptTargetPath = null;
			promptRow.Visible(false);
		}

		private void CommitPrompt()
		{
			string text = promptField.InputField.text;
			PromptKind kind = promptKind;
			string target = promptTargetPath;
			HidePrompt();
			try
			{
				if (kind == PromptKind.NewFolder)
				{
					string name = string.IsNullOrEmpty(text) ? "New Folder" : text;
					Directory.CreateDirectory(Path.Combine(currentPath, name));
				}
				else if (kind == PromptKind.Rename && !string.IsNullOrEmpty(target))
				{
					string destination = Path.Combine(Path.GetDirectoryName(target) ?? currentPath, text);
					if (Directory.Exists(target))
					{
						Directory.Move(target, destination);
					}
					else
					{
						File.Move(target, destination);
					}

					selectedPath = destination;
				}
				else if (kind == PromptKind.Delete && !string.IsNullOrEmpty(target))
				{
					if (Directory.Exists(target))
					{
						Directory.Delete(target, true);
					}
					else
					{
						File.Delete(target);
					}

					selectedPath = null;
				}

				Reload();
			}
			catch (Exception ex)
			{
				status.SetText(ex.Message);
			}
		}

		private void CopySelection(bool cut)
		{
			clipboardPaths.Clear();
			if (!string.IsNullOrEmpty(selectedPath))
			{
				clipboardPaths.Add(selectedPath);
			}

			clipboardCut = cut;
			status.SetText((cut ? "Cut " : "Copied ") + clipboardPaths.Count + " item(s).");
		}

		private void PasteClipboard()
		{
			if (clipboardPaths.Count == 0 || string.IsNullOrEmpty(currentPath))
			{
				return;
			}

			try
			{
				for (int i = 0; i < clipboardPaths.Count; i++)
				{
					string source = clipboardPaths[i];
					string name = Path.GetFileName(source);
					string destination = UniqueDestination(Path.Combine(currentPath, name));
					if (Directory.Exists(source))
					{
						CopyDirectory(source, destination);
						if (clipboardCut)
						{
							Directory.Delete(source, true);
						}
					}
					else if (File.Exists(source))
					{
						File.Copy(source, destination);
						if (clipboardCut)
						{
							File.Delete(source);
						}
					}
				}

				if (clipboardCut)
				{
					clipboardPaths.Clear();
					clipboardCut = false;
				}

				Reload();
			}
			catch (Exception ex)
			{
				status.SetText(ex.Message);
			}
		}

		private void Bookmark(string path)
		{
			if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
			{
				return;
			}

			string stored = PlayerPrefs.GetString(BookmarkPrefsKey, string.Empty);
			if (stored.IndexOf(path, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				status.SetText("Already in Places.");
				return;
			}

			PlayerPrefs.SetString(BookmarkPrefsKey, string.IsNullOrEmpty(stored) ? path : stored + "\n" + path);
			PlayerPrefs.Save();
			RefreshPlaces();
			status.SetText("Added to Places.");
		}

		private void GoBack()
		{
			if (backStack.Count == 0)
			{
				return;
			}

			string previous = backStack[backStack.Count - 1];
			backStack.RemoveAt(backStack.Count - 1);
			if (!string.IsNullOrEmpty(currentPath))
			{
				forwardStack.Add(currentPath);
			}

			Navigate(previous, recordHistory: false);
		}

		private void GoForward()
		{
			if (forwardStack.Count == 0)
			{
				return;
			}

			string next = forwardStack[forwardStack.Count - 1];
			forwardStack.RemoveAt(forwardStack.Count - 1);
			if (!string.IsNullOrEmpty(currentPath))
			{
				backStack.Add(currentPath);
			}

			Navigate(next, recordHistory: false);
		}

		private void GoUp()
		{
			DirectoryInfo parent = string.IsNullOrEmpty(currentPath) ? null : Directory.GetParent(currentPath);
			if (parent != null)
			{
				Navigate(parent.FullName);
			}
		}

		private void OnPathCommitted(string text)
		{
			if (Directory.Exists(text))
			{
				Navigate(text);
			}
		}

		private void OnFilterChanged(string text)
		{
			filterText = text ?? string.Empty;
			RefreshList();
		}

		private void OnHiddenChanged(bool value)
		{
			showHidden = value;
			Reload();
		}

		private void ToggleSort(UiFileBrowserSort column)
		{
			if (sort == column)
			{
				sortAscending = !sortAscending;
			}
			else
			{
				sort = column;
				sortAscending = true;
			}

			RefreshList();
			RefreshChrome();
		}

		private bool MatchesExtension(string filePath)
		{
			if (extensions == null || extensions.Length == 0)
			{
				return true;
			}

			string extension = Path.GetExtension(filePath).ToLowerInvariant();
			return Array.IndexOf(extensions, extension) >= 0;
		}

		private bool IsTyping()
		{
			GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			return selected != null && selected.GetComponent<InputField>() != null;
		}

		private void DisposePreview()
		{
			if (previewImage != null)
			{
				previewImage.SetSprite(null);
			}

			if (previewSprite != null)
			{
				UnityEngine.Object.Destroy(previewSprite);
				previewSprite = null;
			}

			if (previewTexture != null)
			{
				UnityEngine.Object.Destroy(previewTexture);
				previewTexture = null;
			}
		}

		private static UiElement WrapSplit(UiSplit split)
		{
			return new Host(split.Slots[0].GameObject.transform.parent.gameObject, UiTheme.Default).Grow();
		}

		private static string ResolveStartDirectory(string candidate)
		{
			if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
			{
				candidate = Path.GetDirectoryName(candidate);
			}

			return ResolveExistingDirectory(candidate)
				?? UiFileBrowserHostPaths.PreferredStartDirectory()
				?? Application.dataPath;
		}

		private static string ResolveExistingDirectory(string candidate)
		{
			if (string.IsNullOrEmpty(candidate))
			{
				return null;
			}

			try
			{
				string full = Path.GetFullPath(candidate);
				return Directory.Exists(full) ? full : null;
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static List<UiFileBrowserPlace> BuildDefaultPlaces()
		{
			List<UiFileBrowserPlace> places = new List<UiFileBrowserPlace>();
			if (UiFileBrowserHostPaths.IsProton)
			{
				AddExistingPlace(places, "Linux Home", UiFileBrowserHostPaths.Home);
				AddExistingPlace(places, "Linux Pictures", UiFileBrowserHostPaths.Pictures);
				AddExistingPlace(places, "Linux Root", UiFileBrowserHostPaths.Root);
				AddExistingPlace(places, "Proton Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
			}
			else
			{
				AddExistingPlace(places, "Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
			}

			AddExistingPlace(places, "Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
			AddExistingPlace(places, "Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
			AddExistingPlace(places, "Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
			string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			AddExistingPlace(places, "Downloads", downloads);
			AddExistingPlace(places, "Game", Directory.GetParent(Application.dataPath)?.FullName);
			AddExistingPlace(places, "Game Data", Application.dataPath);
			AddExistingPlace(places, "Mods", Path.Combine(Application.dataPath, "Mods"));

			try
			{
				DriveInfo[] drives = DriveInfo.GetDrives();
				for (int i = 0; i < drives.Length; i++)
				{
					DriveInfo drive = drives[i];
					if (drive.IsReady)
					{
						AddUniquePlace(places, new UiFileBrowserPlace(drive.Name, drive.RootDirectory.FullName));
					}
				}
			}
			catch (Exception)
			{
			}

			string bookmarks = PlayerPrefs.GetString(BookmarkPrefsKey, string.Empty);
			if (!string.IsNullOrEmpty(bookmarks))
			{
				string[] lines = bookmarks.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < lines.Length; i++)
				{
					AddExistingPlace(places, Path.GetFileName(lines[i].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), lines[i]);
				}
			}

			return places;
		}

		private static void AddExistingPlace(List<UiFileBrowserPlace> places, string label, string path)
		{
			if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
			{
				AddUniquePlace(places, new UiFileBrowserPlace(label, path));
			}
		}

		private static void AddUniquePlace(List<UiFileBrowserPlace> places, UiFileBrowserPlace place)
		{
			for (int i = 0; i < places.Count; i++)
			{
				if (string.Equals(places[i].Path, place.Path, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
			}

			places.Add(place);
		}

		private static List<UiFileBrowserPlace> SplitBreadcrumbs(string path)
		{
			List<UiFileBrowserPlace> segments = new List<UiFileBrowserPlace>();
			string full = Path.GetFullPath(path);
			string root = Path.GetPathRoot(full);
			segments.Add(new UiFileBrowserPlace(string.IsNullOrEmpty(root) ? full : root, root));
			string relative = full.Substring(root.Length).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (string.IsNullOrEmpty(relative))
			{
				return segments;
			}

			string accum = root;
			string[] parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < parts.Length; i++)
			{
				accum = Path.Combine(accum, parts[i]);
				segments.Add(new UiFileBrowserPlace(parts[i], accum));
			}

			return segments;
		}

		private static bool IsHidden(FileSystemInfo info)
		{
			if (info.Name.StartsWith("."))
			{
				return true;
			}

			try
			{
				return (info.Attributes & FileAttributes.Hidden) != 0;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool IsImage(string path)
		{
			string extension = Path.GetExtension(path).ToLowerInvariant();
			return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
		}

		private static string TypeLabelFor(string path)
		{
			string extension = Path.GetExtension(path);
			return string.IsNullOrEmpty(extension) ? "File" : extension.TrimStart('.').ToUpperInvariant();
		}

		private static string FormatSize(long bytes)
		{
			if (bytes < 1024)
			{
				return bytes + " B";
			}

			if (bytes < 1024 * 1024)
			{
				return (bytes / 1024f).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
			}

			if (bytes < 1024L * 1024 * 1024)
			{
				return (bytes / (1024f * 1024f)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
			}

			return (bytes / (1024f * 1024f * 1024f)).ToString("0.#", CultureInfo.InvariantCulture) + " GB";
		}

		private static string UniqueDestination(string path)
		{
			if (!File.Exists(path) && !Directory.Exists(path))
			{
				return path;
			}

			string directory = Path.GetDirectoryName(path);
			string name = Path.GetFileNameWithoutExtension(path);
			string extension = Path.GetExtension(path);
			int index = 2;
			string candidate;
			do
			{
				candidate = Path.Combine(directory ?? string.Empty, name + " (" + index + ")" + extension);
				index++;
			}
			while (File.Exists(candidate) || Directory.Exists(candidate));

			return candidate;
		}

		private static void CopyDirectory(string source, string destination)
		{
			Directory.CreateDirectory(destination);
			foreach (string file in Directory.GetFiles(source))
			{
				File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
			}

			foreach (string directory in Directory.GetDirectories(source))
			{
				CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
			}
		}

		private sealed class UiFileBrowserRowClick : MonoBehaviour, IPointerClickHandler
		{
			internal Action OnLeft;
			internal Action OnDouble;
			internal Action OnRight;

			public void OnPointerClick(PointerEventData eventData)
			{
				if (eventData.button == PointerEventData.InputButton.Right)
				{
					OnRight?.Invoke();
					return;
				}

				if (eventData.clickCount >= 2)
				{
					OnDouble?.Invoke();
					return;
				}

				OnLeft?.Invoke();
			}
		}

		private sealed class UiFileBrowserInput : MonoBehaviour
		{
			private UiFileBrowser browser;

			internal void Bind(UiFileBrowser owner)
			{
				browser = owner;
			}

			private void Update()
			{
				if (browser == null || !Input.anyKeyDown)
				{
					return;
				}

				bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
				bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
				if (Input.GetKeyDown(KeyCode.Escape))
				{
					browser.HandleKey(KeyCode.Escape, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
				{
					browser.HandleKey(KeyCode.Return, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.Backspace))
				{
					browser.HandleKey(KeyCode.Backspace, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.LeftArrow))
				{
					browser.HandleKey(KeyCode.LeftArrow, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.RightArrow))
				{
					browser.HandleKey(KeyCode.RightArrow, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.F5))
				{
					browser.HandleKey(KeyCode.F5, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.F2))
				{
					browser.HandleKey(KeyCode.F2, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.Delete))
				{
					browser.HandleKey(KeyCode.Delete, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.L))
				{
					browser.HandleKey(KeyCode.L, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.F))
				{
					browser.HandleKey(KeyCode.F, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.H))
				{
					browser.HandleKey(KeyCode.H, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.C))
				{
					browser.HandleKey(KeyCode.C, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.X))
				{
					browser.HandleKey(KeyCode.X, alt, control);
				}
				else if (Input.GetKeyDown(KeyCode.V))
				{
					browser.HandleKey(KeyCode.V, alt, control);
				}
			}
		}
	}
}
