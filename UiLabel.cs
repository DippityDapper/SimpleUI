using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>Static or dynamic read-only text display.</summary>
	public sealed class UiLabel : UiElement<UiLabel>
	{
		/// <summary>The underlying Unity Text component.</summary>
		public Text Text { get; }

		private UiLabel(GameObject gameObject, UiTheme theme, Text text) : base(gameObject, theme)
		{
			Text = text;
		}

		/// <summary>Creates a text label with optional font size and alignment.</summary>
		public static UiLabel Create(Transform parent, string text, UiTheme theme = null,
			int? fontSize = null, TextAnchor alignment = TextAnchor.MiddleLeft)
		{
			theme = theme ?? UiTheme.Default;

			GameObject go = new GameObject("UiLabel", typeof(Text));
			go.transform.SetParent(parent, false);
			Text textComponent = go.GetComponent<Text>();
			textComponent.text = text ?? string.Empty;
			textComponent.font = theme.Font;
			textComponent.fontSize = fontSize ?? theme.BodyFontSize;
			textComponent.alignment = alignment;
			textComponent.color = theme.LabelColor;

			return new UiLabel(go, theme, textComponent);
		}

		/// <summary>Replaces the label's text.</summary>
		public UiLabel SetText(string text)
		{
			Text.text = text ?? string.Empty;
			return this;
		}

		/// <summary>Replaces the label's text color.</summary>
		public UiLabel SetColor(Color color)
		{
			Text.color = color;
			return this;
		}
	}
}
