using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A checkbox-style toggle widget with a label, wrapping Unity's own Toggle component.</summary>
	/// <remarks>Every "boolean simulated by swapping a button's label text" convention in LokrCharacterLab (e.g. CharacterIdentityPanel's own Locked button) replaces itself with this instead once migrated.</remarks>
	public sealed class UiToggle : UiElement<UiToggle>
	{
		/// <summary>The underlying Unity Toggle component.</summary>
		public Toggle Toggle { get; }

		/// <summary>Wraps an already-built Toggle GameObject.</summary>
		private UiToggle(GameObject gameObject, UiTheme theme, Toggle toggle) : base(gameObject, theme)
		{
			Toggle = toggle;
		}

		/// <summary>Creates a labeled toggle with the given initial value.</summary>
		public static UiToggle Create(Transform parent, string label, bool initialValue, UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject go = new GameObject("UiToggle", typeof(Toggle));
			go.transform.SetParent(parent, false);

			GameObject background = new GameObject("Background", typeof(Image));
			background.transform.SetParent(go.transform, false);
			RectTransform backgroundRect = background.GetComponent<RectTransform>();
			backgroundRect.anchorMin = new Vector2(0f, 0.5f);
			backgroundRect.anchorMax = new Vector2(0f, 0.5f);
			backgroundRect.sizeDelta = new Vector2(20f, 20f);
			backgroundRect.anchoredPosition = new Vector2(10f, 0f);
			Image backgroundImage = background.GetComponent<Image>();
			backgroundImage.color = theme.RowButtonColor;

			GameObject checkmark = new GameObject("Checkmark", typeof(Image));
			checkmark.transform.SetParent(background.transform, false);
			RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
			checkmarkRect.anchorMin = Vector2.zero;
			checkmarkRect.anchorMax = Vector2.one;
			checkmarkRect.offsetMin = new Vector2(4f, 4f);
			checkmarkRect.offsetMax = new Vector2(-4f, -4f);
			Image checkmarkImage = checkmark.GetComponent<Image>();
			checkmarkImage.color = theme.AccentColor;

			GameObject labelObject = new GameObject("Label", typeof(Text));
			labelObject.transform.SetParent(go.transform, false);
			RectTransform labelRect = labelObject.GetComponent<RectTransform>();
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = new Vector2(28f, 0f);
			labelRect.offsetMax = Vector2.zero;
			Text labelText = labelObject.GetComponent<Text>();
			labelText.text = label ?? string.Empty;
			labelText.font = theme.Font;
			labelText.fontSize = theme.BodyFontSize;
			labelText.color = theme.LabelColor;
			labelText.alignment = TextAnchor.MiddleLeft;

			Toggle toggle = go.GetComponent<Toggle>();
			toggle.targetGraphic = backgroundImage;
			toggle.graphic = checkmarkImage;
			toggle.isOn = initialValue;

			return new UiToggle(go, theme, toggle);
		}

		/// <summary>Adds a handler fired when the toggle's value changes.</summary>
		public UiToggle OnValueChanged(UnityAction<bool> action)
		{
			Toggle.onValueChanged.AddListener(action);
			return this;
		}

		/// <summary>Sets the value without firing OnValueChanged handlers, for refreshing a field from data without re-triggering the same save path that reading it caused.</summary>
		/// <remarks>
		/// The "suppressFieldEvents" flag pattern CharacterIdentityPanel currently hand-rolls for
		/// exactly this. Swaps the listener list out and back in rather than relying on
		/// Toggle.SetIsOnWithoutNotify, which isn't available on every uGUI version this game's
		/// referenced UnityEngine.UI.dll might be.
		/// </remarks>
		public UiToggle SetValueSilently(bool value)
		{
			Toggle.ToggleEvent handlers = Toggle.onValueChanged;
			Toggle.onValueChanged = new Toggle.ToggleEvent();
			Toggle.isOn = value;
			Toggle.onValueChanged = handlers;
			return this;
		}
	}
}
