using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A clickable button widget with a single OnClick callback.</summary>
	public sealed class UiButton : UiElement<UiButton>
	{
		/// <summary>The underlying Unity Button component.</summary>
		public Button Button { get; }
		/// <summary>The button's background Image component.</summary>
		public Image Image { get; }
		/// <summary>The button's label Text component.</summary>
		public Text Label { get; }

		private UiButton(GameObject gameObject, UiTheme theme, Button button, Image image, Text label) : base(gameObject, theme)
		{
			Button = button;
			Image = image;
			Label = label;
		}

		/// <summary>Creates a button with a label and optional click handler, styled as primary or row-quiet.</summary>
		/// <remarks>primary=true uses Theme.ButtonColor (prominent actions); primary=false uses Theme.RowButtonColor (quieter buttons packed inside a panel's own rows).</remarks>
		public static UiButton Create(Transform parent, string label, UnityAction onClick = null,
			UiTheme theme = null, bool primary = true)
		{
			theme = theme ?? UiTheme.Default;

			GameObject go = new GameObject("UiButton", typeof(Image), typeof(Button));
			go.transform.SetParent(parent, false);
			Image image = go.GetComponent<Image>();
			image.color = primary ? theme.ButtonColor : theme.RowButtonColor;
			Button button = go.GetComponent<Button>();
			if (onClick != null)
			{
				button.onClick.AddListener(onClick);
			}

			GameObject labelObject = new GameObject("Label", typeof(Text));
			labelObject.transform.SetParent(go.transform, false);
			RectTransform labelRect = labelObject.GetComponent<RectTransform>();
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = Vector2.zero;
			labelRect.offsetMax = Vector2.zero;
			Text labelText = labelObject.GetComponent<Text>();
			labelText.text = label ?? string.Empty;
			labelText.font = theme.Font;
			labelText.fontSize = theme.BodyFontSize;
			labelText.alignment = TextAnchor.MiddleCenter;
			labelText.color = theme.LabelColor;

			return new UiButton(go, theme, button, image, labelText);
		}

		/// <summary>Adds a click handler (additive -- does not replace previously registered handlers).</summary>
		public UiButton OnClick(UnityAction action)
		{
			Button.onClick.AddListener(action);
			return this;
		}

		/// <summary>Replaces the button's label text.</summary>
		public UiButton SetLabel(string text)
		{
			Label.text = text ?? string.Empty;
			return this;
		}

		/// <summary>Replaces the button's background color.</summary>
		public UiButton SetColor(Color color)
		{
			Image.color = color;
			return this;
		}

		/// <summary>Enables or disables the button's interactivity.</summary>
		public UiButton Interactable(bool value)
		{
			Button.interactable = value;
			return this;
		}
	}
}
