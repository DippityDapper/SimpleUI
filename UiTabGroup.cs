using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>Host that can accept a tab dragged out of a UiTabGroup (used by UiDockSpace for redocking).</summary>
	internal interface ITabDragHost
	{
		/// <summary>Called when a tab drag begins so the host can show drop targets.</summary>
		void OnTabDragBegin(UiTabGroup source, string tabId);

		/// <summary>Called each drag frame so the host can highlight the zone under the pointer.</summary>
		void OnTabDrag(UiTabGroup source, string tabId, PointerEventData eventData);

		/// <summary>Called when the drag ends; return true if the host consumed the drop (redocked the panel).</summary>
		bool OnTabDragEnd(UiTabGroup source, string tabId, PointerEventData eventData);
	}

	/// <summary>A visible, reorderable tab strip over one shared content area.</summary>
	/// <remarks>
	/// Generalizes <see cref="UiScreenSwitcher"/> into an interactive tab strip: clicking a tab
	/// shows its content, dragging a tab reorders it within the strip, and dragging it out of the
	/// strip is forwarded to an optional <see cref="ITabDragHost"/> (dock redocking). Unlike
	/// UiScreenSwitcher, this is a real widget with a GameObject of its own. Close and pin live
	/// on the tab itself (right-click menu; middle-click closes). Pin also shows a left accent
	/// when set, and only when the tab is closable. There is no separate x or P chrome button.
	/// </remarks>
	public sealed class UiTabGroup : UiElement<UiTabGroup>
	{
		private readonly Transform strip;
		private readonly Transform contentHost;
		private readonly List<TabEntry> tabs = new List<TabEntry>();
		private Action<string> onChanged;
		private Action<string> onClose;
		private Action<string, bool> onPinChanged;
		private Action<string, PointerEventData> onTabRightClick;
		private ITabDragHost dragHost;
		private UiContextMenu tabMenu;
		private string selectedId;
		private bool reorderable = true;

		/// <summary>Id of the currently visible tab, or null if the group is empty.</summary>
		public string SelectedId => selectedId;

		/// <summary>Number of tabs currently in this group.</summary>
		public int TabCount => tabs.Count;

		/// <summary>Tab ids in current visual order.</summary>
		public IReadOnlyList<string> TabIds
		{
			get
			{
				List<string> ids = new List<string>(tabs.Count);
				foreach (TabEntry tab in tabs)
				{
					ids.Add(tab.Id);
				}
				return ids;
			}
		}

		/// <summary>Wraps an already-built tab group.</summary>
		private UiTabGroup(GameObject gameObject, UiTheme theme, Transform strip, Transform contentHost)
			: base(gameObject, theme)
		{
			this.strip = strip;
			this.contentHost = contentHost;
		}

		/// <summary>Creates an empty tab group filling its parent (tab strip on top, content below).</summary>
		public static UiTabGroup Create(Transform parent, UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject root = new GameObject("UiTabGroup", typeof(RectTransform));
			root.transform.SetParent(parent, false);
			UiLayoutUtil.Stretch(root.GetComponent<RectTransform>());

			GameObject stripObject = new GameObject("TabStrip", typeof(Image));
			stripObject.transform.SetParent(root.transform, false);
			RectTransform stripRect = stripObject.GetComponent<RectTransform>();
			stripRect.anchorMin = new Vector2(0f, 1f);
			stripRect.anchorMax = Vector2.one;
			stripRect.pivot = new Vector2(0.5f, 1f);
			stripRect.sizeDelta = new Vector2(0f, theme.TabHeight);
			stripRect.anchoredPosition = Vector2.zero;
			stripObject.GetComponent<Image>().color = new Color(theme.PanelBackground.r, theme.PanelBackground.g, theme.PanelBackground.b, 1f);
			HorizontalLayoutGroup stripLayout = stripObject.AddComponent<HorizontalLayoutGroup>();
			stripLayout.childAlignment = TextAnchor.MiddleLeft;
			stripLayout.childControlWidth = false;
			stripLayout.childControlHeight = true;
			stripLayout.childForceExpandWidth = false;
			stripLayout.childForceExpandHeight = true;
			stripLayout.spacing = 2f;
			stripLayout.padding = new RectOffset(2, 2, 2, 2);

			GameObject contentObject = new GameObject("Content", typeof(RectTransform));
			contentObject.transform.SetParent(root.transform, false);
			RectTransform contentRect = contentObject.GetComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			contentRect.offsetMin = Vector2.zero;
			contentRect.offsetMax = new Vector2(0f, -theme.TabHeight);

			return new UiTabGroup(root, theme, stripObject.transform, contentObject.transform);
		}

		/// <summary>Enables or disables drag-to-reorder within this strip (default true).</summary>
		public UiTabGroup SetReorderable(bool value)
		{
			reorderable = value;
			return this;
		}

		/// <summary>Registers a callback invoked when the selected tab changes.</summary>
		public UiTabGroup OnChanged(Action<string> callback)
		{
			onChanged = callback;
			return this;
		}

		/// <summary>Registers a callback invoked when a tab's close button is clicked.</summary>
		public UiTabGroup OnClose(Action<string> callback)
		{
			onClose = callback;
			return this;
		}

		/// <summary>Registers a callback invoked when a tab's pin state changes.</summary>
		public UiTabGroup OnPinChanged(Action<string, bool> callback)
		{
			onPinChanged = callback;
			return this;
		}

		/// <summary>Registers a callback invoked on right-click of a tab.</summary>
		public UiTabGroup OnTabRightClick(Action<string, PointerEventData> callback)
		{
			onTabRightClick = callback;
			return this;
		}

		/// <summary>Attaches the dock-space host that can accept a tab dragged out of this strip.</summary>
		internal UiTabGroup SetDragHost(ITabDragHost host)
		{
			dragHost = host;
			return this;
		}

		/// <summary>Adds a tab. Content is reparented into this group's content host and shown if this is the first tab.</summary>
		public UiTabGroup AddTab(string id, string title, UiElement content, bool closable = false, bool pinnable = false)
		{
			return InsertTab(id, title, content, tabs.Count, closable, pinnable);
		}

		/// <summary>Inserts a tab at the given index (clamped to the current range).</summary>
		public UiTabGroup InsertTab(string id, string title, UiElement content, int index, bool closable = false, bool pinnable = false)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("Tab id must be non-empty.", nameof(id));
			}
			if (Find(id) != null)
			{
				throw new ArgumentException("A tab with id '" + id + "' already exists in this group.", nameof(id));
			}
			if (content == null)
			{
				throw new ArgumentNullException(nameof(content));
			}

			index = Mathf.Clamp(index, 0, tabs.Count);

			content.RectTransform.SetParent(contentHost, false);
			UiLayoutUtil.Stretch(content.RectTransform);
			content.GameObject.SetActive(false);

			GameObject tabObject = BuildTabButton(id, title, closable, pinnable);
			tabObject.transform.SetParent(strip, false);
			tabObject.transform.SetSiblingIndex(index);

			TabEntry entry = new TabEntry
			{
				Id = id,
				Title = title ?? string.Empty,
				Content = content,
				TabObject = tabObject,
				Closable = closable,
				Pinnable = pinnable,
				Pinned = false
			};
			tabs.Insert(index, entry);
			RefreshTabChrome(entry);

			if (tabs.Count == 1)
			{
				Select(id);
			}
			return this;
		}

		/// <summary>Removes a tab from this group without destroying its content GameObject (the caller reparents or destroys it).</summary>
		public UiElement RemoveTab(string id)
		{
			TabEntry entry = Find(id);
			if (entry == null)
			{
				return null;
			}

			tabs.Remove(entry);
			UnityEngine.Object.Destroy(entry.TabObject);
			entry.Content.GameObject.SetActive(false);

			if (selectedId == id)
			{
				selectedId = null;
				if (tabs.Count > 0)
				{
					Select(tabs[0].Id);
				}
			}
			return entry.Content;
		}

		/// <summary>Looks up a tab's content widget by id.</summary>
		public bool TryGetContent(string id, out UiElement content)
		{
			TabEntry entry = Find(id);
			content = entry != null ? entry.Content : null;
			return entry != null;
		}

		/// <summary>Shows the named tab and hides every other tab's content.</summary>
		public UiTabGroup Select(string id)
		{
			TabEntry match = Find(id);
			if (match == null)
			{
				return this;
			}

			selectedId = id;
			foreach (TabEntry tab in tabs)
			{
				bool active = tab.Id == id;
				tab.Content.GameObject.SetActive(active);
				RefreshTabChrome(tab);
			}
			onChanged?.Invoke(id);
			return this;
		}

		/// <summary>Reorders an existing tab to a new visual index.</summary>
		public UiTabGroup MoveTab(string id, int newIndex)
		{
			TabEntry entry = Find(id);
			if (entry == null)
			{
				return this;
			}
			newIndex = Mathf.Clamp(newIndex, 0, tabs.Count - 1);
			int oldIndex = tabs.IndexOf(entry);
			if (oldIndex == newIndex)
			{
				return this;
			}
			tabs.RemoveAt(oldIndex);
			tabs.Insert(newIndex, entry);
			entry.TabObject.transform.SetSiblingIndex(newIndex);
			return this;
		}

		/// <summary>Sets a tab's pinned state. A pinned tab cannot be closed. No-ops unless the tab is closable and pinnable.</summary>
		public UiTabGroup SetPinned(string id, bool pinned)
		{
			TabEntry entry = Find(id);
			if (entry == null || !ShowsPin(entry) || entry.Pinned == pinned)
			{
				return this;
			}
			entry.Pinned = pinned;
			RefreshTabChrome(entry);
			onPinChanged?.Invoke(id, pinned);
			return this;
		}

		/// <summary>True if the named tab is currently pinned.</summary>
		public bool IsPinned(string id)
		{
			TabEntry entry = Find(id);
			return entry != null && entry.Pinned;
		}

		/// <summary>Computes the insert index for a pointer over this tab strip, or -1 if the pointer is not over the strip.</summary>
		internal int IndexAtPointer(PointerEventData eventData)
		{
			if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)strip, eventData.position, eventData.pressEventCamera))
			{
				return -1;
			}
			for (int i = 0; i < tabs.Count; i++)
			{
				RectTransform tabRect = tabs[i].TabObject.GetComponent<RectTransform>();
				if (!UiLayoutUtil.ScreenToLocal(tabRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
				{
					continue;
				}
				if (local.x < tabRect.rect.center.x)
				{
					return i;
				}
			}
			return tabs.Count;
		}

		/// <summary>Whether a drag that ends at this pointer should be treated as an in-strip reorder rather than a redock.</summary>
		internal bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
		{
			return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)strip, screenPoint, eventCamera)
				|| RectTransformUtility.RectangleContainsScreenPoint((RectTransform)contentHost, screenPoint, eventCamera);
		}

		private TabEntry Find(string id)
		{
			foreach (TabEntry tab in tabs)
			{
				if (tab.Id == id)
				{
					return tab;
				}
			}
			return null;
		}

		private GameObject BuildTabButton(string id, string title, bool closable, bool pinnable)
		{
			GameObject tabObject = new GameObject("Tab_" + id, typeof(Image));
			Image background = tabObject.GetComponent<Image>();
			background.color = Theme.TabBackground;
			LayoutElement layout = tabObject.AddComponent<LayoutElement>();
			bool showPin = closable && pinnable;
			layout.minWidth = 80f;
			layout.preferredWidth = Mathf.Max(80f, 18f + (title != null ? title.Length * 8f : 0f) + (showPin ? 6f : 0f));
			layout.flexibleWidth = 0f;

			HorizontalLayoutGroup row = tabObject.AddComponent<HorizontalLayoutGroup>();
			row.childAlignment = TextAnchor.MiddleLeft;
			row.childControlWidth = false;
			row.childControlHeight = true;
			row.childForceExpandWidth = false;
			row.childForceExpandHeight = true;
			row.padding = new RectOffset(showPin ? 4 : 8, 4, 0, 0);
			row.spacing = 2f;

			if (showPin)
			{
				AddPinMark(tabObject.transform);
			}

			GameObject labelObject = new GameObject("Label", typeof(Text));
			labelObject.transform.SetParent(tabObject.transform, false);
			Text label = labelObject.GetComponent<Text>();
			label.text = title ?? string.Empty;
			label.font = Theme.Font;
			label.fontSize = Theme.BodyFontSize;
			label.color = Theme.LabelColor;
			label.alignment = TextAnchor.MiddleLeft;
			label.raycastTarget = false;
			LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
			labelLayout.minWidth = 48f;
			labelLayout.preferredWidth = Mathf.Max(48f, (title != null ? title.Length * 8f : 48f));
			labelLayout.flexibleWidth = 0f;

			UiTabHandle handle = tabObject.AddComponent<UiTabHandle>();
			handle.Bind(this, id);
			return tabObject;
		}

		private static bool ShowsPin(TabEntry entry)
		{
			return entry != null && entry.Closable && entry.Pinnable;
		}

		private static void AddPinMark(Transform parent)
		{
			GameObject mark = new GameObject("PinMark", typeof(Image));
			mark.transform.SetParent(parent, false);
			mark.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
			LayoutElement layout = mark.AddComponent<LayoutElement>();
			layout.minWidth = 3f;
			layout.preferredWidth = 3f;
			layout.minHeight = 12f;
			layout.preferredHeight = 12f;
			layout.flexibleWidth = 0f;
		}

		private void RefreshTabChrome(TabEntry tab)
		{
			Image background = tab.TabObject.GetComponent<Image>();
			background.color = tab.Id == selectedId ? Theme.TabActiveBackground : Theme.TabBackground;

			Transform pinMark = tab.TabObject.transform.Find("PinMark");
			if (pinMark != null)
			{
				Image markImage = pinMark.GetComponent<Image>();
				if (markImage != null)
				{
					markImage.color = tab.Pinned ? Theme.AccentColor : new Color(1f, 1f, 1f, 0f);
				}
			}
		}

		private void RequestClose(string id)
		{
			TabEntry entry = Find(id);
			if (entry == null || entry.Pinned || !entry.Closable)
			{
				return;
			}
			onClose?.Invoke(id);
		}

		internal void HandlePointerClick(string id, PointerEventData eventData)
		{
			if (eventData.button == PointerEventData.InputButton.Middle)
			{
				RequestClose(id);
				return;
			}
			if (eventData.button == PointerEventData.InputButton.Right)
			{
				ShowTabMenu(id, eventData.position);
				onTabRightClick?.Invoke(id, eventData);
				return;
			}
			if (eventData.button == PointerEventData.InputButton.Left)
			{
				Select(id);
			}
		}

		private void ShowTabMenu(string id, Vector2 screenPosition)
		{
			TabEntry entry = Find(id);
			if (entry == null || (!entry.Closable && !ShowsPin(entry)))
			{
				return;
			}

			if (tabMenu == null)
			{
				Canvas canvas = UiLayoutUtil.FindRootCanvas(GameObject.transform);
				Transform parent = canvas != null ? canvas.transform : GameObject.transform;
				tabMenu = UiContextMenu.Create(parent, Theme);
			}

			tabMenu.ClearItems();
			if (ShowsPin(entry))
			{
				tabMenu.AddItem(entry.Pinned ? "Unpin" : "Pin", () => SetPinned(id, !entry.Pinned));
			}
			if (entry.Closable)
			{
				tabMenu.AddItem("Close", () => RequestClose(id), enabled: !entry.Pinned);
			}
			tabMenu.Show(screenPosition);
		}

		internal void HandleDragBegin(string id, PointerEventData eventData)
		{
			if (!reorderable && dragHost == null)
			{
				return;
			}
			dragHost?.OnTabDragBegin(this, id);
		}

		internal void HandleDrag(string id, PointerEventData eventData)
		{
			if (reorderable)
			{
				int index = IndexAtPointer(eventData);
				if (index >= 0)
				{
					int current = -1;
					for (int i = 0; i < tabs.Count; i++)
					{
						if (tabs[i].Id == id)
						{
							current = i;
							break;
						}
					}
					if (current >= 0)
					{
						int target = index > current ? index - 1 : index;
						MoveTab(id, target);
					}
				}
			}
			dragHost?.OnTabDrag(this, id, eventData);
		}

		internal void HandleDragEnd(string id, PointerEventData eventData)
		{
			if (dragHost != null && dragHost.OnTabDragEnd(this, id, eventData))
			{
				return;
			}
		}

		private sealed class TabEntry
		{
			internal string Id;
			internal string Title;
			internal UiElement Content;
			internal GameObject TabObject;
			internal bool Closable;
			internal bool Pinnable;
			internal bool Pinned;
		}

		/// <summary>Pointer target on a tab button: click to select, drag to reorder or redock.</summary>
		internal sealed class UiTabHandle : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
		{
			private UiTabGroup owner;
			private string tabId;
			private bool didDrag;

			/// <summary>Binds this handle to a tab in its group.</summary>
			internal void Bind(UiTabGroup group, string id)
			{
				owner = group;
				tabId = id;
			}

			/// <summary>Selects the tab, unless this pointer press turned into a drag.</summary>
			public void OnPointerClick(PointerEventData eventData)
			{
				if (didDrag)
				{
					return;
				}
				owner?.HandlePointerClick(tabId, eventData);
			}

			/// <summary>Starts a tab drag.</summary>
			public void OnBeginDrag(PointerEventData eventData)
			{
				didDrag = true;
				owner?.HandleDragBegin(tabId, eventData);
			}

			/// <summary>Continues a tab drag (in-strip reorder and/or host highlight).</summary>
			public void OnDrag(PointerEventData eventData)
			{
				owner?.HandleDrag(tabId, eventData);
			}

			/// <summary>Ends a tab drag and resets the click-suppression flag.</summary>
			public void OnEndDrag(PointerEventData eventData)
			{
				owner?.HandleDragEnd(tabId, eventData);
				didDrag = false;
			}
		}
	}
}
