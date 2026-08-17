using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A draggable divider that reports a 0–1 split fraction along one axis of its parent.</summary>
	/// <remarks>
	/// Does not itself resize siblings — that would be hidden layout ownership. The consumer
	/// (typically <see cref="UiDockSpace"/>) applies the fraction to whatever panes it owns.
	/// Optional <see cref="Bind"/> is the convenience that does apply anchors to two sibling rects.
	/// </remarks>
	public sealed class UiSplitter : UiElement<UiSplitter>
	{
		private readonly UiOrientation orientation;
		private readonly Image image;
		private readonly float thickness;
		private Action<float> onFractionChanged;
		private RectTransform first;
		private RectTransform second;
		private float fraction = 0.5f;
		private float minFirstPx = 80f;
		private float minSecondPx = 80f;

		/// <summary>Current split fraction along the parent: 0 is the first edge (left or top), 1 is the second.</summary>
		public float Fraction => fraction;

		/// <summary>Wraps an already-built splitter handle.</summary>
		private UiSplitter(GameObject gameObject, UiTheme theme, UiOrientation orientation, Image image, float thickness)
			: base(gameObject, theme)
		{
			this.orientation = orientation;
			this.image = image;
			this.thickness = thickness;
		}

		/// <summary>Creates a splitter handle filling a thin strip of its parent; call SetFraction or Bind before use.</summary>
		public static UiSplitter Create(Transform parent, UiOrientation orientation, UiTheme theme = null, float thicknessPx = -1f)
		{
			theme = theme ?? UiTheme.Default;
			float thickness = thicknessPx > 0f ? thicknessPx : theme.SplitterThickness;

			GameObject go = new GameObject("UiSplitter", typeof(Image));
			go.transform.SetParent(parent, false);
			Image image = go.GetComponent<Image>();
			image.color = theme.SplitterColor;

			RectTransform rect = go.GetComponent<RectTransform>();
			if (orientation == UiOrientation.Horizontal)
			{
				rect.anchorMin = new Vector2(0.5f, 0f);
				rect.anchorMax = new Vector2(0.5f, 1f);
				rect.pivot = new Vector2(0.5f, 0.5f);
				rect.sizeDelta = new Vector2(thickness, 0f);
				rect.anchoredPosition = Vector2.zero;
			}
			else
			{
				rect.anchorMin = new Vector2(0f, 0.5f);
				rect.anchorMax = new Vector2(1f, 0.5f);
				rect.pivot = new Vector2(0.5f, 0.5f);
				rect.sizeDelta = new Vector2(0f, thickness);
				rect.anchoredPosition = Vector2.zero;
			}

			UiSplitter splitter = new UiSplitter(go, theme, orientation, image, thickness);
			UiSplitterDrag drag = go.AddComponent<UiSplitterDrag>();
			drag.Bind(splitter);
			return splitter;
		}

		/// <summary>Registers a callback invoked whenever the dragged fraction changes.</summary>
		public UiSplitter OnFractionChanged(Action<float> callback)
		{
			onFractionChanged = callback;
			return this;
		}

		/// <summary>Sets pixel minima for the two sides when converting a pointer position into a fraction.</summary>
		public UiSplitter SetMinPixels(float minFirst, float minSecond)
		{
			minFirstPx = Mathf.Max(0f, minFirst);
			minSecondPx = Mathf.Max(0f, minSecond);
			return this;
		}

		/// <summary>Moves the handle to the given fraction without firing OnFractionChanged.</summary>
		public UiSplitter SetFraction(float value, bool notify = false)
		{
			fraction = Mathf.Clamp01(value);
			ApplyHandlePosition();
			if (first != null && second != null)
			{
				ApplyBoundAnchors();
			}
			if (notify)
			{
				onFractionChanged?.Invoke(fraction);
			}
			return this;
		}

		/// <summary>Automatically keeps two sibling rects sized to this splitter's fraction (first = left/top).</summary>
		public UiSplitter Bind(RectTransform firstPane, RectTransform secondPane)
		{
			if (firstPane == null)
			{
				throw new ArgumentNullException(nameof(firstPane));
			}
			if (secondPane == null)
			{
				throw new ArgumentNullException(nameof(secondPane));
			}
			first = firstPane;
			second = secondPane;
			ApplyBoundAnchors();
			ApplyHandlePosition();
			return this;
		}

		/// <summary>Places the handle at the current fraction of its parent.</summary>
		private void ApplyHandlePosition()
		{
			RectTransform rect = RectTransform;
			if (orientation == UiOrientation.Horizontal)
			{
				rect.anchorMin = new Vector2(fraction, 0f);
				rect.anchorMax = new Vector2(fraction, 1f);
				rect.sizeDelta = new Vector2(thickness, 0f);
				rect.anchoredPosition = Vector2.zero;
			}
			else
			{
				float y = 1f - fraction;
				rect.anchorMin = new Vector2(0f, y);
				rect.anchorMax = new Vector2(1f, y);
				rect.sizeDelta = new Vector2(0f, thickness);
				rect.anchoredPosition = Vector2.zero;
			}
		}

		/// <summary>Stretches the two bound panes to either side of the current fraction, inset by half the handle thickness.</summary>
		private void ApplyBoundAnchors()
		{
			float half = thickness * 0.5f;
			if (orientation == UiOrientation.Horizontal)
			{
				first.anchorMin = Vector2.zero;
				first.anchorMax = new Vector2(fraction, 1f);
				first.offsetMin = Vector2.zero;
				first.offsetMax = new Vector2(-half, 0f);

				second.anchorMin = new Vector2(fraction, 0f);
				second.anchorMax = Vector2.one;
				second.offsetMin = new Vector2(half, 0f);
				second.offsetMax = Vector2.zero;
			}
			else
			{
				float splitY = 1f - fraction;
				first.anchorMin = new Vector2(0f, splitY);
				first.anchorMax = Vector2.one;
				first.offsetMin = new Vector2(0f, half);
				first.offsetMax = Vector2.zero;

				second.anchorMin = Vector2.zero;
				second.anchorMax = new Vector2(1f, splitY);
				second.offsetMin = Vector2.zero;
				second.offsetMax = new Vector2(0f, -half);
			}
		}

		/// <summary>Converts a pointer screen position into a clamped fraction of this splitter's parent.</summary>
		internal void DragTo(PointerEventData eventData)
		{
			RectTransform parentRect = RectTransform.parent as RectTransform;
			if (parentRect == null)
			{
				return;
			}
			if (!UiLayoutUtil.ScreenToLocal(parentRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
			{
				return;
			}

			Rect parentBounds = parentRect.rect;
			float raw;
			float parentSize;
			if (orientation == UiOrientation.Horizontal)
			{
				parentSize = parentBounds.width;
				raw = parentSize > 0f ? (local.x - parentBounds.xMin) / parentSize : fraction;
			}
			else
			{
				parentSize = parentBounds.height;
				raw = parentSize > 0f ? (parentBounds.yMax - local.y) / parentSize : fraction;
			}

			float clamped = UiLayoutUtil.ClampSplitFraction(raw, parentSize, minFirstPx, minSecondPx);
			if (Mathf.Abs(clamped - fraction) < 0.0001f)
			{
				return;
			}
			SetFraction(clamped, notify: true);
		}

		/// <summary>Applies the hover/idle handle color.</summary>
		internal void SetHovered(bool hovered)
		{
			image.color = hovered ? Theme.SplitterHoverColor : Theme.SplitterColor;
		}

		/// <summary>Pointer drag/hover target backing a UiSplitter handle.</summary>
		internal sealed class UiSplitterDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
		{
			private UiSplitter owner;

			/// <summary>Binds this handle to the widget that owns it.</summary>
			internal void Bind(UiSplitter splitter)
			{
				owner = splitter;
			}

			/// <summary>Highlights the handle when a drag starts.</summary>
			public void OnBeginDrag(PointerEventData eventData)
			{
				owner?.SetHovered(true);
			}

			/// <summary>Updates the split fraction from the pointer position.</summary>
			public void OnDrag(PointerEventData eventData)
			{
				owner?.DragTo(eventData);
			}

			/// <summary>Clears the hover highlight when the drag ends and the pointer is no longer over the handle.</summary>
			public void OnEndDrag(PointerEventData eventData)
			{
				owner?.SetHovered(false);
			}

			/// <summary>Highlights the handle on pointer enter.</summary>
			public void OnPointerEnter(PointerEventData eventData)
			{
				owner?.SetHovered(true);
			}

			/// <summary>Clears the hover highlight on pointer exit.</summary>
			public void OnPointerExit(PointerEventData eventData)
			{
				owner?.SetHovered(false);
			}
		}
	}
}
