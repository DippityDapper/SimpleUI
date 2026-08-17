using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>Non-generic base holding just the GameObject/RectTransform any widget needs to be a child.</summary>
	/// <remarks>Just enough for a container to accept ANY concrete widget as a child without caring what it is. See UiElement{TSelf} below for the fluent-chaining subtype every concrete widget actually derives from.</remarks>
	public abstract class UiElement
	{
		/// <summary>The widget's underlying GameObject.</summary>
		public GameObject GameObject { get; protected set; }
		/// <summary>The widget's underlying RectTransform.</summary>
		public RectTransform RectTransform { get; protected set; }
	}

	/// <summary>CRTP base class every concrete widget derives from, providing chainable Grow/FixedWidth/FixedHeight/Margin/Padding.</summary>
	/// <remarks>
	/// Generic on the concrete subtype (the "curiously recurring" pattern) purely so chained calls
	/// keep returning the caller's own real type (e.g. `UiButton.Create(...).Grow().OnClick(...)` --
	/// OnClick is UiButton-only, so Grow() must hand back a UiButton, not a plain UiElement).
	///
	/// Sizing here is expressed via a LayoutElement component -- the same hint mechanism Unity's own
	/// LayoutGroup reads from every child -- so it means something inside a UiStack (content-driven)
	/// parent. Inside a UiSplit (weight-driven) parent, sizing instead comes from the ColumnSpec
	/// passed to UiSplit itself; Grow/FixedWidth/FixedHeight here are simply inert there, not an
	/// error, since a UiSplit slot's own RectTransform is positioned directly rather than through a
	/// LayoutGroup.
	/// </remarks>
	public abstract class UiElement<TSelf> : UiElement where TSelf : UiElement<TSelf>
	{
		protected readonly UiTheme Theme;

		/// <summary>Initializes the element's GameObject/RectTransform/Theme (adding a RectTransform if the GameObject doesn't already have one).</summary>
		protected UiElement(GameObject gameObject, UiTheme theme)
		{
			GameObject = gameObject;
			RectTransform = gameObject.GetComponent<RectTransform>();
			if (RectTransform == null)
			{
				RectTransform = gameObject.AddComponent<RectTransform>();
			}
			Theme = theme ?? UiTheme.Default;
		}

		/// <summary>Gets (or lazily adds) the GameObject's LayoutElement component.</summary>
		private LayoutElement EnsureLayoutElement()
		{
			LayoutElement element = GameObject.GetComponent<LayoutElement>();
			return element != null ? element : GameObject.AddComponent<LayoutElement>();
		}

		/// <summary>Takes up any leftover space along the parent UiStack's axis, proportionally to weight against sibling Grow() elements.</summary>
		/// <remarks>The LayoutGroup equivalent of a flexible spacer.</remarks>
		public TSelf Grow(float weight = 1f)
		{
			LayoutElement element = EnsureLayoutElement();
			element.flexibleWidth = weight;
			element.flexibleHeight = weight;
			return (TSelf)this;
		}

		/// <summary>Fixes this element's width to an exact pixel size, overriding any Grow.</summary>
		public TSelf FixedWidth(float pixels)
		{
			LayoutElement element = EnsureLayoutElement();
			element.preferredWidth = pixels;
			element.minWidth = pixels;
			element.flexibleWidth = 0f;
			return (TSelf)this;
		}

		/// <summary>Fixes this element's height to an exact pixel size, overriding any Grow.</summary>
		public TSelf FixedHeight(float pixels)
		{
			LayoutElement element = EnsureLayoutElement();
			element.preferredHeight = pixels;
			element.minHeight = pixels;
			element.flexibleHeight = 0f;
			return (TSelf)this;
		}

		/// <summary>Sets the underlying GameObject's name (useful for debugging the hierarchy).</summary>
		public TSelf Name(string name)
		{
			GameObject.name = name;
			return (TSelf)this;
		}

		/// <summary>Shows or hides the element by activating/deactivating its GameObject.</summary>
		public TSelf Visible(bool visible)
		{
			GameObject.SetActive(visible);
			return (TSelf)this;
		}
	}
}
