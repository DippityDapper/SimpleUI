using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>Floating thumbnail that follows the cursor while a catalogue card is pulled out of the list.</summary>
	internal static class UiCatalogueDragGhost
	{
		private static RectTransform root;
		private static Image thumb;
		private static Text label;
		private static Canvas canvas;
		private static UiCatalogueItem current;

		/// <summary>Shows or refreshes the ghost at this screen point.</summary>
		internal static void Show(UiCatalogueItem item, Vector2 screen, Canvas hostCanvas)
		{
			Ensure(hostCanvas);
			if (root == null)
			{
				return;
			}

			current = item;
			string name = item != null && !string.IsNullOrEmpty(item.Name)
				? item.Name
				: item != null ? item.Id : string.Empty;
			Sprite sprite = item != null ? item.Image : null;
			thumb.sprite = sprite;
			thumb.color = sprite != null ? Color.white : new Color(0.12f, 0.13f, 0.16f, 1f);
			label.text = name ?? string.Empty;
			root.gameObject.SetActive(true);
			root.SetAsLastSibling();
			Move(screen);
		}

		/// <summary>Moves the ghost so its top-left sits under the cursor.</summary>
		internal static void Move(Vector2 screen)
		{
			if (root == null || !root.gameObject.activeSelf || canvas == null)
			{
				return;
			}

			if (current != null && current.Image != null && thumb != null && thumb.sprite != current.Image)
			{
				thumb.sprite = current.Image;
				thumb.color = Color.white;
			}

			Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
				? null
				: canvas.worldCamera;
			RectTransform canvasRect = canvas.transform as RectTransform;
			Vector2 local;
			if (canvasRect == null
				|| !RectTransformUtility.ScreenPointToLocalPointInRectangle(
					canvasRect, screen, uiCamera, out local))
			{
				return;
			}

			root.anchoredPosition = local + new Vector2(12f, -12f);
		}

		/// <summary>Hides the ghost without destroying it.</summary>
		internal static void Hide()
		{
			if (root != null)
			{
				root.gameObject.SetActive(false);
			}

			current = null;
		}

		private static void Ensure(Canvas hostCanvas)
		{
			if (hostCanvas == null)
			{
				return;
			}

			if (root != null && canvas == hostCanvas)
			{
				return;
			}

			if (root != null)
			{
				Object.Destroy(root.gameObject);
				root = null;
				thumb = null;
				label = null;
			}

			canvas = hostCanvas;

			GameObject box = new GameObject("UiCatalogueDragGhost", typeof(RectTransform));
			box.transform.SetParent(canvas.transform, false);
			root = box.GetComponent<RectTransform>();
			root.anchorMin = new Vector2(0.5f, 0.5f);
			root.anchorMax = new Vector2(0.5f, 0.5f);
			root.pivot = new Vector2(0f, 1f);
			root.sizeDelta = new Vector2(72f, 90f);
			Image background = box.AddComponent<Image>();
			background.color = new Color(0.1f, 0.11f, 0.14f, 0.92f);
			background.raycastTarget = false;

			GameObject thumbObject = new GameObject("Thumb", typeof(RectTransform));
			thumbObject.transform.SetParent(root, false);
			RectTransform thumbRect = thumbObject.GetComponent<RectTransform>();
			thumbRect.anchorMin = new Vector2(0.5f, 1f);
			thumbRect.anchorMax = new Vector2(0.5f, 1f);
			thumbRect.pivot = new Vector2(0.5f, 1f);
			thumbRect.anchoredPosition = new Vector2(0f, -6f);
			thumbRect.sizeDelta = new Vector2(56f, 56f);
			thumb = thumbObject.AddComponent<Image>();
			thumb.raycastTarget = false;
			thumb.preserveAspect = true;

			GameObject labelObject = new GameObject("Label", typeof(RectTransform));
			labelObject.transform.SetParent(root, false);
			RectTransform labelRect = labelObject.GetComponent<RectTransform>();
			labelRect.anchorMin = new Vector2(0f, 0f);
			labelRect.anchorMax = new Vector2(1f, 0f);
			labelRect.pivot = new Vector2(0.5f, 0f);
			labelRect.anchoredPosition = new Vector2(0f, 4f);
			labelRect.sizeDelta = new Vector2(-8f, 20f);
			label = labelObject.AddComponent<Text>();
			label.font = UiTheme.Default.Font;
			label.fontSize = 11;
			label.alignment = TextAnchor.MiddleCenter;
			label.color = Color.white;
			label.raycastTarget = false;
			label.horizontalOverflow = HorizontalWrapMode.Overflow;
			label.verticalOverflow = VerticalWrapMode.Truncate;
		}
	}
}
