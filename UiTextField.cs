using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A single-line text input field with OnChange and OnEndEdit events.</summary>
	public sealed class UiTextField : UiElement<UiTextField>
	{
		/// <summary>The underlying Unity InputField component.</summary>
		public InputField InputField { get; }

		private UiTextField(GameObject gameObject, UiTheme theme, InputField inputField) : base(gameObject, theme)
		{
			InputField = inputField;
		}

		/// <summary>Creates a single- or multi-line text field with optional default text.</summary>
		public static UiTextField Create(Transform parent, string defaultText = "", UiTheme theme = null, bool multiline = false)
		{
			theme = theme ?? UiTheme.Default;

			GameObject go = new GameObject("UiTextField", typeof(Image), typeof(InputField));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = theme.FieldBackground;

			GameObject textObject = new GameObject("Text", typeof(Text));
			textObject.transform.SetParent(go.transform, false);
			RectTransform textRect = textObject.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = new Vector2(6f, 2f);
			textRect.offsetMax = new Vector2(-6f, -2f);
			Text text = textObject.GetComponent<Text>();
			text.font = theme.Font;
			text.fontSize = theme.BodyFontSize;
			text.alignment = TextAnchor.MiddleLeft;
			text.color = theme.FieldTextColor;
			text.supportRichText = false;

			InputField inputField = go.GetComponent<InputField>();
			inputField.textComponent = text;
			inputField.text = defaultText ?? string.Empty;
			if (multiline)
			{
				inputField.lineType = InputField.LineType.MultiLineNewline;
			}

			return new UiTextField(go, theme, inputField);
		}

		/// <summary>Adds a handler fired on every text change, including each keystroke.</summary>
		public UiTextField OnChange(UnityAction<string> action)
		{
			InputField.onValueChanged.AddListener(action);
			return this;
		}

		/// <summary>Adds a handler fired when editing ends (Enter pressed or field loses focus).</summary>
		public UiTextField OnEndEdit(UnityAction<string> action)
		{
			InputField.onEndEdit.AddListener(action);
			return this;
		}

		/// <summary>Replaces the field's current text.</summary>
		public UiTextField SetText(string text)
		{
			InputField.text = text ?? string.Empty;
			return this;
		}
	}
}
