using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>Shared RectTransform/canvas helpers used by docking widgets.</summary>
	internal static class UiLayoutUtil
	{
		/// <summary>Stretches a RectTransform to fill its parent with zero pixel offsets.</summary>
		internal static void Stretch(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		/// <summary>Creates a full-stretch Image GameObject under parent.</summary>
		internal static Image StretchImage(Transform parent, string name, Color color, bool raycastTarget = true)
		{
			GameObject go = new GameObject(name, typeof(Image));
			go.transform.SetParent(parent, false);
			Stretch(go.GetComponent<RectTransform>());
			Image image = go.GetComponent<Image>();
			image.color = color;
			image.raycastTarget = raycastTarget;
			return image;
		}

		/// <summary>Finds the root canvas for a transform, or null if it is not under a Canvas.</summary>
		internal static Canvas FindRootCanvas(Transform transform)
		{
			Canvas canvas = transform.GetComponentInParent<Canvas>();
			return canvas != null ? canvas.rootCanvas : null;
		}

		/// <summary>Converts a screen point into local coordinates of the given rect (works for overlay canvases whose camera is null).</summary>
		internal static bool ScreenToLocal(RectTransform rect, Vector2 screenPoint, Camera eventCamera, out Vector2 localPoint)
		{
			return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out localPoint);
		}

		/// <summary>Clamps a pixel size so a pair of min-pixel constraints can still fit inside parentSize.</summary>
		internal static float ClampSplitFraction(float fraction, float parentSize, float minFirstPx, float minSecondPx)
		{
			if (parentSize <= 0f)
			{
				return fraction;
			}
			float minFraction = minFirstPx / parentSize;
			float maxFraction = 1f - (minSecondPx / parentSize);
			if (minFraction >= maxFraction)
			{
				return 0.5f;
			}
			return Mathf.Clamp(fraction, minFraction, maxFraction);
		}
	}
}
