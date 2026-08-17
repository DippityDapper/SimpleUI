using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A background box with an optional title bar and one content area.</summary>
	/// <remarks>
	/// Content is a single child (typically a UiStack) added via Add(). Replaces every
	/// "new GameObject(typeof(Image)) then hand-set anchorMin/anchorMax" block that used to open
	/// each panel file in LokrCharacterLab.
	///
	/// Fills its parent by default. Pass `region` for the one legitimate case that still needs a
	/// hand-specified fraction-of-parent placement: a single panel centered with margin on every
	/// side, where -- unlike UiSplit's job of dividing space between several coordinated siblings --
	/// there are no siblings to coordinate with, so a direct fraction can't overlap anything.
	/// </remarks>
	public sealed class UiPanel : UiElement<UiPanel>
	{
		/// <summary>The Transform child content should be parented under (via Add).</summary>
		public Transform ContentParent { get; }

		private UiPanel(GameObject gameObject, UiTheme theme, Transform contentParent) : base(gameObject, theme)
		{
			ContentParent = contentParent;
		}

		/// <summary>Creates a panel filling its parent by default, or a fixed fraction of it via `region`.</summary>
		public static UiPanel Create(Transform parent, UiTheme theme = null, string title = null,
			float titleHeight = 30f, Rect? region = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject panelObject = new GameObject("UiPanel", typeof(Image));
			panelObject.transform.SetParent(parent, false);
			RectTransform panelRect = panelObject.GetComponent<RectTransform>();
			Rect r = region ?? new Rect(0f, 0f, 1f, 1f);
			panelRect.anchorMin = new Vector2(r.x, r.y);
			panelRect.anchorMax = new Vector2(r.x + r.width, r.y + r.height);
			panelRect.offsetMin = Vector2.zero;
			panelRect.offsetMax = Vector2.zero;
			panelObject.GetComponent<Image>().color = theme.PanelBackground;

			GameObject contentArea = new GameObject("Content", typeof(RectTransform));
			contentArea.transform.SetParent(panelObject.transform, false);
			RectTransform contentRect = contentArea.GetComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			contentRect.offsetMin = Vector2.zero;

			if (!string.IsNullOrEmpty(title))
			{
				GameObject titleObject = new GameObject("Title", typeof(Text));
				titleObject.transform.SetParent(panelObject.transform, false);
				RectTransform titleRect = titleObject.GetComponent<RectTransform>();
				titleRect.anchorMin = new Vector2(0f, 1f);
				titleRect.anchorMax = new Vector2(1f, 1f);
				titleRect.pivot = new Vector2(0.5f, 1f);
				titleRect.sizeDelta = new Vector2(0f, titleHeight);
				titleRect.anchoredPosition = Vector2.zero;
				Text titleText = titleObject.GetComponent<Text>();
				titleText.text = title;
				titleText.font = theme.Font;
				titleText.fontSize = theme.TitleFontSize;
				titleText.color = theme.LabelColor;
				titleText.alignment = TextAnchor.UpperLeft;

				contentRect.offsetMax = new Vector2(0f, -titleHeight);
			}
			else
			{
				contentRect.offsetMax = Vector2.zero;
			}

			return new UiPanel(panelObject, theme, contentArea.transform);
		}

		/// <summary>Parents a child element under this panel's content area.</summary>
		public UiPanel Add(UiElement content)
		{
			content.RectTransform.SetParent(ContentParent, false);
			return this;
		}
	}
}
