using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>An image display widget with aspect-preserving sizing and optional UV-coordinate click handling.</summary>
	public sealed class UiImage : UiElement<UiImage>
	{
		/// <summary>The underlying Unity Image component.</summary>
		public Image Image { get; }

		/// <summary>Wraps an already-built Image GameObject.</summary>
		private UiImage(GameObject gameObject, UiTheme theme, Image image) : base(gameObject, theme)
		{
			Image = image;
		}

		/// <summary>Creates an aspect-preserving image, optionally with an initial sprite.</summary>
		/// <remarks>Uses Unity's own Image.preserveAspect, which already does the letterboxed aspect-fit math IslandAtlasPickerPanel used to hand-roll itself (Mathf.Min scale + centered offset).</remarks>
		public static UiImage Create(Transform parent, Sprite sprite = null, UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject go = new GameObject("UiImage", typeof(Image));
			go.transform.SetParent(parent, false);
			RectTransform rect = go.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			Image image = go.GetComponent<Image>();
			image.sprite = sprite;
			image.preserveAspect = true;

			return new UiImage(go, theme, image);
		}

		/// <summary>Replaces the displayed sprite.</summary>
		public UiImage SetSprite(Sprite sprite)
		{
			Image.sprite = sprite;
			return this;
		}

		/// <summary>Adds a handler fired with the normalized UV coordinate (0,0 = bottom-left) of a click within the actually-displayed, aspect-fit-letterboxed image area.</summary>
		/// <remarks>
		/// Generalizes IslandAtlasPickerPanel's bespoke screen-&gt;local-&gt;UV click math into a
		/// reusable primitive. The handler is never called for a click that lands in the
		/// letterboxed margin.
		/// </remarks>
		public UiImage OnClickAtUv(Action<Vector2> handler)
		{
			UiImageClickTarget target = GameObject.GetComponent<UiImageClickTarget>();
			if (target == null)
			{
				target = GameObject.AddComponent<UiImageClickTarget>();
			}
			target.Configure(Image, handler);
			return this;
		}
	}

	/// <summary>Click target component backing UiImage.OnClickAtUv -- converts a click into a normalized UV coordinate.</summary>
	internal sealed class UiImageClickTarget : MonoBehaviour, IPointerClickHandler
	{
		private Image image;
		private Action<Vector2> handler;

		/// <summary>Sets the image and handler this target should report clicks against.</summary>
		internal void Configure(Image targetImage, Action<Vector2> onClick)
		{
			image = targetImage;
			handler = onClick;
		}

		/// <summary>Converts the click position to a UV coordinate within the displayed image area and invokes the handler, unless it landed in the letterboxed margin.</summary>
		public void OnPointerClick(PointerEventData eventData)
		{
			if (image == null || image.sprite == null || handler == null)
			{
				return;
			}
			RectTransform rect = (RectTransform)transform;
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
			{
				return;
			}

			Rect bounds = rect.rect;
			Rect displayRect = bounds;
			if (image.preserveAspect)
			{
				float spriteAspect = image.sprite.rect.width / image.sprite.rect.height;
				float boundsAspect = bounds.width / bounds.height;
				if (spriteAspect > boundsAspect)
				{
					float displayHeight = bounds.width / spriteAspect;
					float yOffset = (bounds.height - displayHeight) * 0.5f;
					displayRect = new Rect(bounds.x, bounds.y + yOffset, bounds.width, displayHeight);
				}
				else
				{
					float displayWidth = bounds.height * spriteAspect;
					float xOffset = (bounds.width - displayWidth) * 0.5f;
					displayRect = new Rect(bounds.x + xOffset, bounds.y, displayWidth, bounds.height);
				}
			}

			if (!displayRect.Contains(localPoint))
			{
				return;
			}

			float u = (localPoint.x - displayRect.x) / displayRect.width;
			float v = (localPoint.y - displayRect.y) / displayRect.height;
			handler(new Vector2(u, v));
		}
	}
}
