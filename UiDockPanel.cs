using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A titled panel meant to live inside a UiDockSpace zone (or stand alone as a titled box).</summary>
	/// <remarks>
	/// When hosted by a <see cref="UiTabGroup"/> the title bar is hidden — the tab strip is the
	/// title. Close and pin live on the tab (right-click menu; middle-click closes). Standalone
	/// (not yet docked) the title bar uses the same gestures. There is no separate x or P button.
	/// </remarks>
	public sealed class UiDockPanel : UiElement<UiDockPanel>
	{
		private readonly Transform titleBar;
		private readonly Text titleLabel;
		private readonly Image pinMark;
		private Action<UiDockPanel> onClose;
		private Action<UiDockPanel, bool> onPinChanged;
		private UiContextMenu titleMenu;
		private bool pinned;

		/// <summary>Stable id used by UiDockSpace layout snapshots.</summary>
		public string Id { get; }

		/// <summary>Display title shown on the title bar and on the hosting tab.</summary>
		public string Title => titleLabel != null ? titleLabel.text : string.Empty;

		/// <summary>Transform children should be parented under (via Add).</summary>
		public Transform ContentParent { get; }

		/// <summary>Whether this panel may be closed from chrome.</summary>
		public bool Closable { get; }

		/// <summary>Whether this panel can be pinned. Pin chrome only appears when also <see cref="Closable"/>.</summary>
		public bool Pinnable { get; }

		/// <summary>True while pinned; a pinned panel cannot be closed.</summary>
		public bool IsPinned => pinned;

		/// <summary>Wraps an already-built dock panel.</summary>
		private UiDockPanel(GameObject gameObject, UiTheme theme, string id, Transform titleBar, Text titleLabel,
			Transform contentParent, Image pinMark, bool closable, bool pinnable)
			: base(gameObject, theme)
		{
			Id = id;
			this.titleBar = titleBar;
			this.titleLabel = titleLabel;
			ContentParent = contentParent;
			this.pinMark = pinMark;
			Closable = closable;
			Pinnable = pinnable;
		}

		/// <summary>Creates a dock panel filling its parent, with an optional close/pin title bar.</summary>
		public static UiDockPanel Create(Transform parent, string id, string title, UiTheme theme = null,
			bool closable = true, bool pinnable = true)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("Dock panel id must be non-empty.", nameof(id));
			}
			theme = theme ?? UiTheme.Default;
			float titleHeight = theme.TabHeight;

			GameObject root = new GameObject("UiDockPanel_" + id, typeof(Image));
			root.transform.SetParent(parent, false);
			UiLayoutUtil.Stretch(root.GetComponent<RectTransform>());
			root.GetComponent<Image>().color = theme.PanelBackground;

			GameObject titleObject = new GameObject("TitleBar", typeof(Image));
			titleObject.transform.SetParent(root.transform, false);
			RectTransform titleRect = titleObject.GetComponent<RectTransform>();
			titleRect.anchorMin = new Vector2(0f, 1f);
			titleRect.anchorMax = Vector2.one;
			titleRect.pivot = new Vector2(0.5f, 1f);
			titleRect.sizeDelta = new Vector2(0f, titleHeight);
			titleRect.anchoredPosition = Vector2.zero;
			titleObject.GetComponent<Image>().color = theme.TabBackground;
			HorizontalLayoutGroup titleRow = titleObject.AddComponent<HorizontalLayoutGroup>();
			titleRow.childAlignment = TextAnchor.MiddleLeft;
			titleRow.childControlWidth = false;
			titleRow.childControlHeight = true;
			titleRow.childForceExpandHeight = true;
			bool showPin = closable && pinnable;
			titleRow.padding = new RectOffset(showPin ? 4 : 8, 4, 0, 0);
			titleRow.spacing = 4f;

			Image pinMark = null;
			if (showPin)
			{
				GameObject markObject = new GameObject("PinMark", typeof(Image));
				markObject.transform.SetParent(titleObject.transform, false);
				pinMark = markObject.GetComponent<Image>();
				pinMark.color = new Color(1f, 1f, 1f, 0f);
				LayoutElement markLayout = markObject.AddComponent<LayoutElement>();
				markLayout.minWidth = 3f;
				markLayout.preferredWidth = 3f;
				markLayout.minHeight = 12f;
				markLayout.preferredHeight = 12f;
			}

			GameObject labelObject = new GameObject("Title", typeof(Text));
			labelObject.transform.SetParent(titleObject.transform, false);
			Text titleText = labelObject.GetComponent<Text>();
			titleText.text = title ?? string.Empty;
			titleText.font = theme.Font;
			titleText.fontSize = theme.TitleFontSize;
			titleText.color = theme.LabelColor;
			titleText.alignment = TextAnchor.MiddleLeft;
			LayoutElement titleLayout = labelObject.AddComponent<LayoutElement>();
			titleLayout.flexibleWidth = 1f;
			titleLayout.minWidth = 40f;

			GameObject content = new GameObject("Content", typeof(RectTransform));
			content.transform.SetParent(root.transform, false);
			RectTransform contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			contentRect.offsetMin = Vector2.zero;
			contentRect.offsetMax = new Vector2(0f, -titleHeight);

			UiDockPanel panel = new UiDockPanel(root, theme, id, titleObject.transform, titleText,
				content.transform, pinMark, closable, pinnable);
			if (closable || showPin)
			{
				TitleBarHandle handle = titleObject.AddComponent<TitleBarHandle>();
				handle.Bind(panel);
			}
			return panel;
		}

		/// <summary>Parents a child element under this panel's content area.</summary>
		public UiDockPanel Add(UiElement content)
		{
			content.RectTransform.SetParent(ContentParent, false);
			return this;
		}

		/// <summary>Replaces the panel's title text.</summary>
		public UiDockPanel SetTitle(string title)
		{
			titleLabel.text = title ?? string.Empty;
			return this;
		}

		/// <summary>Registers a callback invoked when the user closes this panel (not fired when pinned).</summary>
		public UiDockPanel OnClose(Action<UiDockPanel> callback)
		{
			onClose = callback;
			return this;
		}

		/// <summary>Registers a callback invoked when pin state changes.</summary>
		public UiDockPanel OnPinChanged(Action<UiDockPanel, bool> callback)
		{
			onPinChanged = callback;
			return this;
		}

		/// <summary>Shows or hides the standalone title bar (hidden when a tab strip is already showing the title).</summary>
		public UiDockPanel SetTitleBarVisible(bool visible)
		{
			titleBar.gameObject.SetActive(visible);
			RectTransform contentRect = ContentParent as RectTransform;
			if (contentRect != null)
			{
				contentRect.offsetMax = visible ? new Vector2(0f, -Theme.TabHeight) : Vector2.zero;
			}
			return this;
		}

		/// <summary>Sets pin state. No-ops unless this panel is closable and pinnable.</summary>
		public UiDockPanel SetPinned(bool value)
		{
			if (!Closable || !Pinnable || pinned == value)
			{
				return this;
			}
			pinned = value;
			RefreshPinChrome();
			onPinChanged?.Invoke(this, pinned);
			return this;
		}

		internal void RequestClose()
		{
			if (pinned || !Closable)
			{
				return;
			}
			onClose?.Invoke(this);
		}

		internal void TogglePin()
		{
			SetPinned(!pinned);
		}

		private void RefreshPinChrome()
		{
			if (pinMark != null)
			{
				pinMark.color = pinned ? Theme.AccentColor : new Color(1f, 1f, 1f, 0f);
			}
		}

		internal void ShowTitleMenu(Vector2 screenPosition)
		{
			if (!Closable)
			{
				return;
			}

			if (titleMenu == null)
			{
				Canvas canvas = UiLayoutUtil.FindRootCanvas(GameObject.transform);
				Transform parent = canvas != null ? canvas.transform : GameObject.transform;
				titleMenu = UiContextMenu.Create(parent, Theme);
			}

			titleMenu.ClearItems();
			if (Closable && Pinnable)
			{
				titleMenu.AddItem(pinned ? "Unpin" : "Pin", TogglePin);
			}
			if (Closable)
			{
				titleMenu.AddItem("Close", RequestClose, enabled: !pinned);
			}
			titleMenu.Show(screenPosition);
		}

		/// <summary>Right-click opens Pin/Close; middle-click closes.</summary>
		private sealed class TitleBarHandle : MonoBehaviour, IPointerClickHandler
		{
			private UiDockPanel owner;

			/// <summary>Binds this handle to its dock panel.</summary>
			internal void Bind(UiDockPanel panel)
			{
				owner = panel;
			}

			/// <summary>Right-click opens the tab menu; middle-click closes when allowed.</summary>
			public void OnPointerClick(PointerEventData eventData)
			{
				if (owner == null)
				{
					return;
				}
				if (eventData.button == PointerEventData.InputButton.Middle)
				{
					owner.RequestClose();
					return;
				}
				if (eventData.button == PointerEventData.InputButton.Right)
				{
					owner.ShowTitleMenu(eventData.position);
				}
			}
		}
	}
}
