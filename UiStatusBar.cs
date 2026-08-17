using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A thin strip for status text on the left and optional right-aligned indicators.</summary>
	/// <remarks>Promotes ToolbarPanel's ad hoc status Text reference into something workspace-agnostic.</remarks>
	public sealed class UiStatusBar : UiElement<UiStatusBar>
	{
		private readonly UiLabel leftLabel;
		private readonly UiLabel rightLabel;

		/// <summary>Wraps an already-built status bar.</summary>
		private UiStatusBar(GameObject gameObject, UiTheme theme, UiLabel leftLabel, UiLabel rightLabel)
			: base(gameObject, theme)
		{
			this.leftLabel = leftLabel;
			this.rightLabel = rightLabel;
		}

		/// <summary>Creates a status bar filling its parent as a thin horizontal strip.</summary>
		public static UiStatusBar Create(Transform parent, UiTheme theme = null, float heightPx = -1f)
		{
			theme = theme ?? UiTheme.Default;
			float height = heightPx > 0f ? heightPx : theme.StatusBarHeight;

			GameObject root = new GameObject("UiStatusBar", typeof(Image));
			root.transform.SetParent(parent, false);
			UiLayoutUtil.Stretch(root.GetComponent<RectTransform>());
			root.GetComponent<Image>().color = theme.PanelBackground;
			LayoutElement rootLayout = root.AddComponent<LayoutElement>();
			rootLayout.minHeight = height;
			rootLayout.preferredHeight = height;

			UiStack row = UiStack.Horizontal(root.transform, theme, spacing: 8f, padding: 6f);

			UiLabel left = UiLabel.Create(row.ContentTransform, string.Empty, theme, theme.BodyFontSize, TextAnchor.MiddleLeft);
			left.Grow();
			row.Add(left);

			UiLabel right = UiLabel.Create(row.ContentTransform, string.Empty, theme, theme.BodyFontSize, TextAnchor.MiddleRight);
			row.Add(right.FixedWidth(280f));

			return new UiStatusBar(root, theme, left, right);
		}

		/// <summary>Replaces the left-aligned status text.</summary>
		public UiStatusBar SetText(string text)
		{
			leftLabel.SetText(text ?? string.Empty);
			return this;
		}

		/// <summary>Replaces the right-aligned indicator text (e.g. project id / type).</summary>
		public UiStatusBar SetRightText(string text)
		{
			rightLabel.SetText(text ?? string.Empty);
			return this;
		}
	}
}
