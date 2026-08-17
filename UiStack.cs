using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>Layout axis for a UiStack.</summary>
	public enum UiOrientation
	{
		/// <summary>Children stack top to bottom.</summary>
		Vertical,
		/// <summary>Children stack left to right.</summary>
		Horizontal
	}

	/// <summary>A row or column layout group for content-driven, responsive child sizing.</summary>
	/// <remarks>
	/// A thin wrapper around Unity's own VerticalLayoutGroup/HorizontalLayoutGroup +
	/// ContentSizeFitter, which this codebase had never used anywhere before SimpleUI (every
	/// row/list was hand-positioned by float arithmetic instead). Children stack along one axis
	/// with spacing/padding and the container grows to fit them.
	///
	/// Pass scrollable:true to clip+scroll instead of growing unbounded when children overflow --
	/// implemented as a real ScrollRect+RectMask2D wrapping an inner, unclamped content stack, so
	/// there's no separate "does this need to scroll" decision to get wrong: with
	/// MovementType.Clamped, a ScrollRect whose content is smaller than its viewport simply doesn't
	/// move, which is exactly "no scrolling needed" with no extra logic.
	///
	/// Sizing and scroll-clipping are deliberately independent concerns: a scrollable UiStack's
	/// outer viewport is sized exactly like a non-scrollable one -- it fills whatever space its
	/// parent gives it, whether that's a plain rect's full bounds or a LayoutGroup-assigned size
	/// via the normal FixedWidth/FixedHeight/Grow calls on the returned UiStack. Whatever size it
	/// ends up being, the RectMask2D clips to exactly that and the ScrollRect scrolls within it.
	/// (An earlier version of this tried to also hand-set the viewport's own sizeDelta as a
	/// "fallback" for the plain-rect-parent case -- wrong, because sizeDelta on a stretch-anchored
	/// RectTransform is a delta from the stretched size, not an absolute size, so it silently
	/// produced "parent size plus that value" instead of the intended fixed size.)
	///
	/// A scrollable stack reports no preferred size on the scroll axis. Nesting one inside another
	/// scrollable stack, or inside a non-scrollable stack whose ContentSizeFitter is still on
	/// PreferredSize, collapses the inner form to zero height -- the widgets exist, the viewport
	/// is empty. Put the ScrollRect on the host that already has an assigned size; build inner
	/// content with scrollable:false. Add(child.Grow()) is what relaxes this stack's own fitter;
	/// Create(parent) alone does not. Hit in LokrLab's Ability / Animator / Properties inspectors
	/// (2026-08-13) and earlier in HomeNavPanel / CharacterLevelsPanel UiList nesting.
	/// </remarks>
	public sealed class UiStack : UiElement<UiStack>
	{
		private readonly Transform childParent;

		/// <summary>The Transform children actually get parented under (differs from GameObject.transform when scrollable).</summary>
		/// <remarks>UiList&lt;T&gt; needs this to parent rows.</remarks>
		public Transform ContentTransform => childParent;

		/// <summary>Wraps an already-built stack GameObject and its child-parenting Transform.</summary>
		private UiStack(GameObject gameObject, UiTheme theme, Transform childParent) : base(gameObject, theme)
		{
			this.childParent = childParent;
		}

		/// <summary>Creates a top-to-bottom stack, optionally scrollable.</summary>
		public static UiStack Vertical(Transform parent, UiTheme theme = null, float spacing = -1f,
			float padding = -1f, bool scrollable = false)
		{
			return Create(parent, UiOrientation.Vertical, theme, spacing, padding, scrollable);
		}

		/// <summary>Creates a left-to-right stack, optionally scrollable.</summary>
		public static UiStack Horizontal(Transform parent, UiTheme theme = null, float spacing = -1f,
			float padding = -1f, bool scrollable = false)
		{
			return Create(parent, UiOrientation.Horizontal, theme, spacing, padding, scrollable);
		}

		/// <summary>Builds the stack's GameObject(s) -- a single layout group, or a ScrollRect+RectMask2D wrapping one when scrollable.</summary>
		/// <remarks>
		/// The outer GameObject is the scroll viewport when scrollable -- masked, sized however its
		/// parent sizes it. A transparent Image is still needed as a raycast target for drag/wheel
		/// input, same as EditorUiHelpers.CreateScrollList already relied on. Leaving
		/// scrollRect.viewport unset (null) makes the ScrollRect use its own RectTransform as the
		/// viewport, the same simplified setup EditorUiHelpers.CreateScrollList already used.
		/// </remarks>
		private static UiStack Create(Transform parent, UiOrientation orientation, UiTheme theme,
			float spacing, float padding, bool scrollable)
		{
			theme = theme ?? UiTheme.Default;
			spacing = spacing >= 0f ? spacing : theme.Spacing;
			padding = padding >= 0f ? padding : theme.Padding;
			bool vertical = orientation == UiOrientation.Vertical;

			GameObject outer = new GameObject(vertical ? "VStack" : "HStack", typeof(RectTransform));
			outer.transform.SetParent(parent, false);
			RectTransform outerRect = outer.GetComponent<RectTransform>();
			outerRect.anchorMin = Vector2.zero;
			outerRect.anchorMax = Vector2.one;
			outerRect.offsetMin = Vector2.zero;
			outerRect.offsetMax = Vector2.zero;

			if (!scrollable)
			{
				AddLayoutGroupAndFitter(outer, vertical, spacing, padding);
				return new UiStack(outer, theme, outer.transform);
			}

			outer.AddComponent<RectMask2D>();
			outer.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
			ScrollRect scrollRect = outer.AddComponent<ScrollRect>();
			scrollRect.horizontal = !vertical;
			scrollRect.vertical = vertical;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			scrollRect.scrollSensitivity = theme.ScrollSensitivity;

			GameObject content = new GameObject("Content", typeof(RectTransform));
			content.transform.SetParent(outer.transform, false);
			RectTransform contentRect = content.GetComponent<RectTransform>();
			if (vertical)
			{
				contentRect.anchorMin = new Vector2(0f, 1f);
				contentRect.anchorMax = new Vector2(1f, 1f);
				contentRect.pivot = new Vector2(0.5f, 1f);
			}
			else
			{
				contentRect.anchorMin = new Vector2(0f, 0f);
				contentRect.anchorMax = new Vector2(0f, 1f);
				contentRect.pivot = new Vector2(0f, 0.5f);
			}
			contentRect.anchoredPosition = Vector2.zero;
			contentRect.sizeDelta = Vector2.zero;
			AddLayoutGroupAndFitter(content, vertical, spacing, padding);

			scrollRect.content = contentRect;

			return new UiStack(outer, theme, content.transform);
		}

		/// <summary>Adds a VerticalLayoutGroup or HorizontalLayoutGroup plus a matching ContentSizeFitter to a GameObject.</summary>
		/// <remarks>
		/// Cross-axis children stretch to fill the container by default (a vertical stack's rows
		/// span its full width; a horizontal stack's columns span its full height) -- the primary
		/// axis is NOT force-expanded (each child keeps its own preferred/fixed size there; Grow()
		/// is the explicit per-child opt-in for taking leftover primary-axis space instead).
		/// </remarks>
		private static void AddLayoutGroupAndFitter(GameObject go, bool vertical, float spacing, float padding)
		{
			HorizontalOrVerticalLayoutGroup group = vertical
				? (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>()
				: go.AddComponent<HorizontalLayoutGroup>();
			group.spacing = spacing;
			int pad = Mathf.RoundToInt(padding);
			group.padding = new RectOffset(pad, pad, pad, pad);
			group.childControlWidth = true;
			group.childControlHeight = true;
			group.childForceExpandWidth = vertical;
			group.childForceExpandHeight = !vertical;
			group.childAlignment = TextAnchor.UpperLeft;

			ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
			fitter.horizontalFit = !vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
			fitter.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
		}

		/// <summary>Parents a child under this stack, auto-relaxing the size-fitter on any axis the child is set to Grow along.</summary>
		/// <remarks>
		/// A container that hosts a Grow()n child can no longer sensibly self-fit to its own content
		/// on that axis -- "shrink-wrap to exactly fit my children" and "let this one child expand to
		/// claim leftover space" are contradictory for the same container (a self-fitting container
		/// has no leftover space by definition), and the self-fit losing that fight would otherwise
		/// silently starve the growing child down to near nothing instead of erroring. Detected here
		/// rather than left to the caller to remember: adding a child with a nonzero
		/// LayoutElement.flexibleWidth/Height flips this container's own ContentSizeFitter to
		/// Unconstrained for that axis, which hands sizing back to the anchor-stretch this container
		/// was already given at Create() time -- exactly what lets a LayoutGroup further up actually
		/// have real leftover space to distribute, or (with no enclosing LayoutGroup) lets this
		/// container fill its own parent's full bounds outright.
		/// </remarks>
		public UiStack Add(UiElement child)
		{
			child.RectTransform.SetParent(childParent, false);

			LayoutElement childLayoutElement = child.GameObject.GetComponent<LayoutElement>();
			if (childLayoutElement != null)
			{
				ContentSizeFitter fitter = childParent.GetComponent<ContentSizeFitter>();
				if (fitter != null)
				{
					if (childLayoutElement.flexibleWidth > 0f)
					{
						fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
					}
					if (childLayoutElement.flexibleHeight > 0f)
					{
						fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
					}
				}
			}
			return this;
		}

		/// <summary>Removes every child added so far.</summary>
		/// <remarks>
		/// The shared building block every list-refresh callsite used to hand-roll as its own
		/// `for (i = childCount-1; i &gt;= 0; i--) Destroy(...)` loop. UiList&lt;T&gt; (built on top
		/// of this) additionally diffs by key instead of always clearing everything; this raw
		/// Clear() stays available for stacks that really do want a full rebuild every refresh.
		/// Children are deactivated before Destroy so a same-frame Clear-then-measure does not
		/// still treat outgoing rows as live — Unity defers Destroy until after Update.
		/// </remarks>
		public UiStack Clear()
		{
			if (childParent == null)
			{
				return this;
			}

			for (int i = childParent.childCount - 1; i >= 0; i--)
			{
				GameObject child = childParent.GetChild(i).gameObject;
				child.SetActive(false);
				Object.Destroy(child);
			}
			return this;
		}
	}
}
