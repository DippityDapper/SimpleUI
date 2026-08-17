using System.Collections.Generic;
using UnityEngine;

namespace SimpleUI
{
	/// <summary>A UiSplit slot's size along the split axis: a fixed pixel amount, or a weighted share of the remainder.</summary>
	/// <remarks>The weighted share is proportional to its own weight against sibling weighted slots, out of whatever space is left over after every fixed slot is subtracted.</remarks>
	public readonly struct ColumnSpec
	{
		/// <summary>Whether this slot uses an exact pixel size (true) or a weighted share of the remaining space (false).</summary>
		internal readonly bool IsFixedSize;
		/// <summary>The pixel size if IsFixedSize, otherwise the weight used to divide the remaining space among weighted slots.</summary>
		internal readonly float Value;

		private ColumnSpec(bool isFixedSize, float value)
		{
			IsFixedSize = isFixedSize;
			Value = value;
		}

		/// <summary>A slot with an exact pixel size.</summary>
		public static ColumnSpec Fixed(float pixels) => new ColumnSpec(true, pixels);
		/// <summary>A slot sized as a weighted share of whatever space remains after fixed slots.</summary>
		public static ColumnSpec Weighted(float weight) => new ColumnSpec(false, weight);
	}

	/// <summary>One slot produced by a UiSplit -- its GameObject plus the normalized rect it occupies.</summary>
	public sealed class UiSplitSlot : UiElement<UiSplitSlot>
	{
		/// <summary>This slot's fraction of its parent's rect, e.g. for a Camera.rect on a viewport slot.</summary>
		/// <remarks>anchorMin/anchorMax equivalent -- what a Camera.rect needs, for the two viewport slots that render camera content instead of UI.</remarks>
		public Rect NormalizedRect { get; }

		/// <summary>Wraps the already-created slot GameObject with its resolved normalized rect.</summary>
		internal UiSplitSlot(GameObject gameObject, UiTheme theme, Rect normalizedRect) : base(gameObject, theme)
		{
			NormalizedRect = normalizedRect;
		}
	}

	/// <summary>A weight-driven multi-column grid layout that keeps ratios stable on resize.</summary>
	/// <remarks>
	/// N columns or rows, each slot's fraction resolved synchronously in C# (not deferred to a
	/// Unity layout pass, unlike UiStack) -- the generalized replacement for EditorLayout's
	/// hand-summed dock-row Rect constants. Slot fractions always sum to exactly 1 by construction,
	/// so two siblings can never overlap or leave a gap the way independently hand-tuned Rects could.
	/// </remarks>
	public sealed class UiSplit
	{
		/// <summary>The slots created for this split, in order.</summary>
		public IReadOnlyList<UiSplitSlot> Slots { get; }

		/// <summary>Splits the parent into vertical columns per the given specs.</summary>
		public static UiSplit Columns(Transform parent, UiTheme theme, params ColumnSpec[] specs)
		{
			return Create(parent, theme, horizontal: true, specs);
		}

		/// <summary>Splits the parent into horizontal rows per the given specs.</summary>
		public static UiSplit Rows(Transform parent, UiTheme theme, params ColumnSpec[] specs)
		{
			return Create(parent, theme, horizontal: false, specs);
		}

		/// <summary>Computes each slot's normalized rect from the specs and creates its GameObject.</summary>
		/// <remarks>If the parent rect isn't resolved yet (e.g. still nested inside an unbuilt LayoutGroup), falls back to an even split rather than dividing by zero.</remarks>
		private static UiSplit Create(Transform parent, UiTheme theme, bool horizontal, ColumnSpec[] specs)
		{
			theme = theme ?? UiTheme.Default;

			GameObject container = new GameObject(horizontal ? "HSplit" : "VSplit", typeof(RectTransform));
			container.transform.SetParent(parent, false);
			RectTransform containerRect = container.GetComponent<RectTransform>();
			containerRect.anchorMin = Vector2.zero;
			containerRect.anchorMax = Vector2.one;
			containerRect.offsetMin = Vector2.zero;
			containerRect.offsetMax = Vector2.zero;

			RectTransform parentRect = parent as RectTransform;
			if (parentRect == null)
			{
				parentRect = parent.GetComponent<RectTransform>();
			}
			float totalPixels = parentRect != null
				? (horizontal ? parentRect.rect.width : parentRect.rect.height)
				: 0f;

			float fixedTotal = 0f;
			float weightTotal = 0f;
			foreach (ColumnSpec spec in specs)
			{
				if (spec.IsFixedSize)
				{
					fixedTotal += spec.Value;
				}
				else
				{
					weightTotal += spec.Value;
				}
			}
			float remainingPixels = Mathf.Max(0f, totalPixels - fixedTotal);

			List<UiSplitSlot> slots = new List<UiSplitSlot>(specs.Length);
			float cursor = 0f;
			for (int i = 0; i < specs.Length; i++)
			{
				ColumnSpec spec = specs[i];
				float fraction;
				if (totalPixels <= 0f)
				{
					fraction = 1f / specs.Length;
				}
				else if (spec.IsFixedSize)
				{
					fraction = spec.Value / totalPixels;
				}
				else
				{
					fraction = weightTotal > 0f ? (spec.Value / weightTotal) * (remainingPixels / totalPixels) : 0f;
				}

				Rect normalizedRect = horizontal
					? new Rect(cursor, 0f, fraction, 1f)
					: new Rect(0f, 1f - cursor - fraction, 1f, fraction);

				GameObject slotObject = new GameObject("Slot_" + i, typeof(RectTransform));
				slotObject.transform.SetParent(container.transform, false);
				RectTransform slotRect = slotObject.GetComponent<RectTransform>();
				slotRect.anchorMin = new Vector2(normalizedRect.x, normalizedRect.y);
				slotRect.anchorMax = new Vector2(normalizedRect.x + normalizedRect.width, normalizedRect.y + normalizedRect.height);
				slotRect.offsetMin = Vector2.zero;
				slotRect.offsetMax = Vector2.zero;

				slots.Add(new UiSplitSlot(slotObject, theme, normalizedRect));
				cursor += fraction;
			}

			return new UiSplit(slots);
		}

		/// <summary>Wraps the already-created slots as a UiSplit.</summary>
		private UiSplit(List<UiSplitSlot> slots)
		{
			Slots = slots;
		}
	}
}
