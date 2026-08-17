using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A right-click popup menu. Starts hidden; call Show at a screen position.</summary>
		/// <remarks>
	/// Reparents to the root canvas while open so it is not clipped by nested masks, matching
	/// UiComboBox's popup. A transparent full-canvas catcher behind the menu closes it on any
	/// outside click. There is no floating-panel analogue — this is a transient popup, not a dock.
	/// Size is set explicitly in Show() from the current items (width hugs the longest
	/// label; height is padding plus rows). A ContentSizeFitter on the menu root cannot work
	/// here: the root has no LayoutGroup, so the fitter would read the bare Image's preferred
	/// size (0) and collapse the menu to a few pixels — which is what made "Add Part" render
	/// as a lone "A".
	/// </remarks>
	public sealed class UiContextMenu : UiElement<UiContextMenu>
	{
		private const float StackPadding = 6f;
		private const float ItemTextPadding = 8f;
		private const float ItemSpacing = 2f;
		private const float MinMenuWidth = 80f;

		private readonly GameObject catcher;
		private readonly UiStack itemStack;
		private readonly Transform originalParent;
		private readonly List<Action> itemActions = new List<Action>();
		private bool isOpen;

		/// <summary>True while the menu is visible.</summary>
		public bool IsOpen => isOpen;

		/// <summary>Wraps an already-built menu and its outside-click catcher.</summary>
		private UiContextMenu(GameObject gameObject, UiTheme theme, GameObject catcher, UiStack itemStack, Transform originalParent)
			: base(gameObject, theme)
		{
			this.catcher = catcher;
			this.itemStack = itemStack;
			this.originalParent = originalParent;
		}

		/// <summary>Creates a hidden context menu under canvas (typically the root canvas transform).</summary>
		public static UiContextMenu Create(Transform canvas, UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject catcherObject = new GameObject("ContextMenuCatcher", typeof(Image), typeof(Button));
			catcherObject.transform.SetParent(canvas, false);
			UiLayoutUtil.Stretch(catcherObject.GetComponent<RectTransform>());
			Image catcherImage = catcherObject.GetComponent<Image>();
			catcherImage.color = new Color(0f, 0f, 0f, 0.01f);
			catcherObject.SetActive(false);

			GameObject menuObject = new GameObject("UiContextMenu", typeof(Image));
			menuObject.transform.SetParent(canvas, false);
			RectTransform menuRect = menuObject.GetComponent<RectTransform>();
			menuRect.anchorMin = new Vector2(0f, 1f);
			menuRect.anchorMax = new Vector2(0f, 1f);
			menuRect.pivot = new Vector2(0f, 1f);
			menuRect.sizeDelta = new Vector2(200f, 40f);
			menuObject.GetComponent<Image>().color = theme.ContextMenuBackground;
			menuObject.SetActive(false);

			UiStack stack = UiStack.Vertical(menuObject.transform, theme, spacing: ItemSpacing, padding: StackPadding);

			UiContextMenu menu = new UiContextMenu(menuObject, theme, catcherObject, stack, canvas);
			catcherObject.GetComponent<Button>().onClick.AddListener(menu.Hide);
			return menu;
		}

		/// <summary>Removes every item added so far.</summary>
		/// <remarks>
		/// DestroyImmediate, not UiStack.Clear's deferred Destroy. File/Edit/View share one menu
		/// and rebuild it in the same click; deferred Destroy left the previous menu's rows in
		/// childCount, so FitToItems kept File's height when opening Edit's single item.
		/// </remarks>
		public UiContextMenu ClearItems()
		{
			Transform content = itemStack.ContentTransform;
			if (content != null)
			{
				for (int i = content.childCount - 1; i >= 0; i--)
				{
					UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
				}
			}

			itemActions.Clear();
			return this;
		}

		/// <summary>Adds a clickable item. Disabled items render but do not fire.</summary>
		public UiContextMenu AddItem(string label, Action onClick, bool enabled = true)
		{
			UiButton button = UiButton.Create(itemStack.ContentTransform, label ?? string.Empty, null, Theme, primary: false)
				.FixedHeight(Theme.ControlHeight);
			RectTransform labelRect = button.Label.rectTransform;
			labelRect.offsetMin = new Vector2(ItemTextPadding, 0f);
			labelRect.offsetMax = new Vector2(-ItemTextPadding, 0f);
			button.Label.alignment = TextAnchor.MiddleLeft;
			button.Interactable(enabled);
			if (enabled && onClick != null)
			{
				button.OnClick(() =>
				{
					Hide();
					onClick();
				});
			}
			itemStack.Add(button);
			itemActions.Add(onClick);
			return this;
		}

		/// <summary>Adds a non-interactive separator line.</summary>
		public UiContextMenu AddSeparator()
		{
			GameObject line = new GameObject("Separator", typeof(Image));
			line.transform.SetParent(itemStack.ContentTransform, false);
			line.GetComponent<Image>().color = Theme.SeparatorColor;
			LayoutElement layout = line.AddComponent<LayoutElement>();
			layout.minHeight = 2f;
			layout.preferredHeight = 2f;
			layout.flexibleWidth = 1f;
			itemActions.Add(null);
			return this;
		}

		/// <summary>Shows the menu at a screen position, reparented to the root canvas so it stacks above everything else.</summary>
		public void Show(Vector2 screenPosition)
		{
			Canvas canvas = UiLayoutUtil.FindRootCanvas(originalParent);
			Transform canvasTransform = canvas != null ? canvas.transform : originalParent;
			catcher.transform.SetParent(canvasTransform, false);
			GameObject.transform.SetParent(canvasTransform, false);
			catcher.transform.SetAsLastSibling();
			GameObject.transform.SetAsLastSibling();

			catcher.SetActive(true);
			GameObject.SetActive(true);
			isOpen = true;

			RectTransform canvasRect = canvasTransform as RectTransform;
			Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			Vector2 local = Vector2.zero;
			if (canvasRect != null)
			{
				UiLayoutUtil.ScreenToLocal(canvasRect, screenPosition, eventCamera, out local);
			}

			RectTransform menuRect = RectTransform;
			menuRect.anchorMin = new Vector2(0.5f, 0.5f);
			menuRect.anchorMax = new Vector2(0.5f, 0.5f);
			menuRect.pivot = new Vector2(0f, 1f);
			menuRect.anchoredPosition = local;
			FitToItems(menuRect);

			Canvas.ForceUpdateCanvases();
			ClampToCanvas(canvasRect);
		}

		/// <summary>Hides the menu and its outside-click catcher.</summary>
		public void Hide()
		{
			isOpen = false;
			catcher.SetActive(false);
			GameObject.SetActive(false);
			if (originalParent != null)
			{
				catcher.transform.SetParent(originalParent, false);
				GameObject.transform.SetParent(originalParent, false);
			}
		}

		/// <summary>Sets the menu's pixel size from its current items so the stretch-fill item stack has a real parent to fill.</summary>
		/// <remarks>
		/// Width is the longest label plus stack and text padding, not a fixed 200px — a hardcoded
		/// panel left a dark empty band beside the File menu's shorter item buttons. Inactive rows
		/// are skipped so a deferred Destroy from another clear path cannot keep the previous
		/// menu's height.
		/// </remarks>
		private void FitToItems(RectTransform menuRect)
		{
			float padding = StackPadding * 2f;
			float width = MinMenuWidth;
			float height = padding;
			int liveCount = 0;
			Transform content = itemStack.ContentTransform;
			for (int i = 0; i < content.childCount; i++)
			{
				Transform row = content.GetChild(i);
				if (!row.gameObject.activeSelf)
				{
					continue;
				}

				if (liveCount > 0)
				{
					height += ItemSpacing;
				}

				LayoutElement layout = row.GetComponent<LayoutElement>();
				float rowHeight = layout != null && layout.preferredHeight > 0f
					? layout.preferredHeight
					: Theme.ControlHeight;
				height += rowHeight;
				width = Mathf.Max(width, MeasureItemWidth(row) + padding);
				liveCount++;
			}

			if (liveCount == 0)
			{
				height = Theme.ControlHeight + padding;
			}

			menuRect.sizeDelta = new Vector2(width, height);
		}

		/// <summary>Preferred width of a menu row from its label, including left/right text inset.</summary>
		private static float MeasureItemWidth(Transform row)
		{
			Text label = row.GetComponentInChildren<Text>();
			if (label == null || string.IsNullOrEmpty(label.text))
			{
				return 0f;
			}

			return label.preferredWidth + ItemTextPadding * 2f;
		}

		private void ClampToCanvas(RectTransform canvasRect)
		{
			if (canvasRect == null)
			{
				return;
			}
			RectTransform menuRect = RectTransform;
			Bounds menuBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, menuRect);
			Vector2 pos = menuRect.anchoredPosition;
			Rect canvasBounds = canvasRect.rect;
			float overflowX = (menuBounds.max.x) - canvasBounds.xMax;
			float overflowY = canvasBounds.yMin - menuBounds.min.y;
			if (overflowX > 0f)
			{
				pos.x -= overflowX;
			}
			if (overflowY > 0f)
			{
				pos.y += overflowY;
			}
			menuRect.anchoredPosition = pos;
		}
	}
}
