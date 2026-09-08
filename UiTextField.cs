using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A single-line text input field with OnChange and OnEndEdit events.</summary>
	public sealed class UiTextField : UiElement<UiTextField>
	{
		private const float VariantChipWidth = 46f;
		private const float VariantChipHeight = 16f;
		private const float VariantChipMargin = 2f;
		private const int VariantChipFontSize = 9;

		/// <summary>The underlying Unity InputField component.</summary>
		public InputField InputField { get; }

		private IReadOnlyList<string> variantOptions;
		private Func<string, string> variantLoad;
		private Action<string, string> variantCommit;
		private UiButton variantChip;
		private UiContextMenu variantMenu;

		/// <summary>The variant/locale currently shown by this field, or null if <see cref="WithVariants"/> was never called.</summary>
		public string CurrentVariant { get; private set; }

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

		/// <summary>
		/// Attaches a small variant/locale chip to this field's bottom-right corner (e.g. a
		/// character's Name field showing "en_US"). Clicking it opens a compact list of
		/// <paramref name="options"/>; picking one swaps the field to that option's own text.
		/// </summary>
		/// <remarks>
		/// SimpleUI has no idea what an "option" means -- it is an opaque string the caller supplied
		/// (a locale suffix, a difficulty tier, anything with a small fixed set of variants). Picking
		/// a new option first calls <paramref name="commit"/> with the option being left and this
		/// field's current text (even mid-edit, unsaved by <see cref="OnEndEdit"/> yet), so the
		/// caller can persist it before the box's content changes out from under it. The field then
		/// calls <paramref name="load"/> for the newly picked option and shows whatever comes back
		/// (empty when null, meaning "no value authored for this option yet"). <see cref="OnChange"/>
		/// and <see cref="OnEndEdit"/> still fire exactly as they do for user typing -- including
		/// during the swap's own <see cref="SetText"/> call, by design, since <see cref="CurrentVariant"/>
		/// is already updated to the new option by the time they fire, so a caller that reacts to
		/// every change writes to the right place either way (in the swap case, writing back the same
		/// text <paramref name="load"/> just returned, a harmless no-op). Reserves right/bottom
		/// padding on the field's own text so typed content never renders under the chip.
		///
		/// Calls <paramref name="load"/> for <paramref name="initial"/> immediately to populate the
		/// field's starting text -- this used to be left entirely to the caller's own follow-up
		/// refresh, which meant any host that built the field and populated it from the SAME code
		/// path that just attached the chip (rather than a guaranteed-later Refresh call) saw a
		/// permanently blank field for whatever this call's own <c>defaultText</c> happened to be.
		///
		/// Only carves a bottom strip out of the text rect's own vertical space for a multiline
		/// field, which has height to spare -- a single-line field (the common case: Name, a
		/// comment's Text, a brief Title, all sized around 28-30px) previously had that same fixed
		/// 18px strip subtracted from both edges regardless, leaving as little as ~8px of vertical
		/// room for one line of text and its caret -- invisible in practice, and easily mistaken for
		/// "the field didn't load" or "typing doesn't show" when it was actually rendering (and
		/// accepting input) inside a sliver too short to see. Horizontal space for the chip is still
		/// reserved on every field either way, since that's what actually keeps typed characters
		/// from running underneath it.
		/// </remarks>
		public UiTextField WithVariants(IReadOnlyList<string> options, string initial, Func<string, string> load, Action<string, string> commit)
		{
			if (options == null || options.Count == 0)
			{
				return this;
			}

			variantOptions = options;
			variantLoad = load;
			variantCommit = commit;
			CurrentVariant = ContainsOption(options, initial) ? initial : options[0];
			SetText(load != null ? load(CurrentVariant) : string.Empty);

			RectTransform textRect = InputField.textComponent.rectTransform;
			Vector2 offsetMax = textRect.offsetMax;
			offsetMax.x -= VariantChipWidth + VariantChipMargin;
			textRect.offsetMax = offsetMax;
			if (InputField.lineType != InputField.LineType.SingleLine)
			{
				Vector2 offsetMin = textRect.offsetMin;
				offsetMin.y += VariantChipHeight + VariantChipMargin;
				textRect.offsetMin = offsetMin;
			}

			variantChip = UiButton.Create(GameObject.transform, CurrentVariant, null, Theme, primary: false);
			RectTransform chipRect = variantChip.RectTransform;
			chipRect.anchorMin = new Vector2(1f, 0f);
			chipRect.anchorMax = new Vector2(1f, 0f);
			chipRect.pivot = new Vector2(1f, 0f);
			chipRect.sizeDelta = new Vector2(VariantChipWidth, VariantChipHeight);
			chipRect.anchoredPosition = new Vector2(-VariantChipMargin, VariantChipMargin);
			variantChip.Label.fontSize = VariantChipFontSize;

			variantMenu = UiContextMenu.Create(GameObject.transform, Theme);
			RebuildVariantMenu();
			variantChip.OnClick(() =>
			{
				RebuildVariantMenu();
				variantMenu.Show(Input.mousePosition);
			});

			return this;
		}

		/// <summary>
		/// Forces this field back to a given variant/text pair without running <see cref="WithVariants"/>'s
		/// load/commit hooks -- for a host refreshing the whole field from a different backing object
		/// (e.g. switching which character/ability/encounter is being edited) rather than the user
		/// swapping locales on the same one. A no-op if <see cref="WithVariants"/> was never called.
		/// </summary>
		public UiTextField ResetVariant(string option, string text)
		{
			if (variantChip == null || string.IsNullOrEmpty(option))
			{
				return SetText(text);
			}

			CurrentVariant = option;
			variantChip.SetLabel(option);
			return SetText(text);
		}

		private static bool ContainsOption(IReadOnlyList<string> options, string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}

			for (int i = 0; i < options.Count; i++)
			{
				if (string.Equals(options[i], value, StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}

		private void RebuildVariantMenu()
		{
			variantMenu.ClearItems();
			for (int i = 0; i < variantOptions.Count; i++)
			{
				string option = variantOptions[i];
				variantMenu.AddItem(option, () => SwitchVariant(option), enabled: !string.Equals(option, CurrentVariant, StringComparison.Ordinal));
			}
		}

		private void SwitchVariant(string next)
		{
			if (string.Equals(next, CurrentVariant, StringComparison.Ordinal))
			{
				return;
			}

			variantCommit?.Invoke(CurrentVariant, InputField.text);
			CurrentVariant = next;
			variantChip.SetLabel(next);
			SetText(variantLoad != null ? variantLoad(next) : string.Empty);
		}
	}
}
