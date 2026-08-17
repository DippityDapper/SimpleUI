using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A dropdown selection widget wrapping Unity's own Dropdown component.</summary>
	/// <remarks>
	/// Replaces the "cycling button that advances through an enum" convention LokrCharacterLab's
	/// Easing field currently fakes a dropdown with. Builds the same Template/Viewport/Content/Item
	/// hierarchy Unity's own "Create &gt; UI &gt; Dropdown" menu command generates in-editor --
	/// there's no shortcut around it, Dropdown genuinely needs this structure to render its open list.
	/// </remarks>
	public sealed class UiDropdown : UiElement<UiDropdown>
	{
		/// <summary>The underlying Unity Dropdown component.</summary>
		public Dropdown Dropdown { get; }

		/// <summary>Wraps an already-built Dropdown GameObject.</summary>
		private UiDropdown(GameObject gameObject, UiTheme theme, Dropdown dropdown) : base(gameObject, theme)
		{
			Dropdown = dropdown;
		}

		/// <summary>Creates a dropdown pre-populated with the given options.</summary>
		public static UiDropdown Create(Transform parent, IEnumerable<string> options, UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject go = new GameObject("UiDropdown", typeof(Image), typeof(Dropdown));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = theme.FieldBackground;

			GameObject labelObject = new GameObject("Label", typeof(Text));
			labelObject.transform.SetParent(go.transform, false);
			RectTransform labelRect = labelObject.GetComponent<RectTransform>();
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = new Vector2(8f, 2f);
			labelRect.offsetMax = new Vector2(-24f, -2f);
			Text labelText = labelObject.GetComponent<Text>();
			labelText.font = theme.Font;
			labelText.fontSize = theme.BodyFontSize;
			labelText.color = theme.FieldTextColor;
			labelText.alignment = TextAnchor.MiddleLeft;

			GameObject template = BuildTemplate(go.transform, theme, out Text itemLabelText);
			template.SetActive(false);

			Dropdown dropdown = go.GetComponent<Dropdown>();
			dropdown.targetGraphic = go.GetComponent<Image>();
			dropdown.captionText = labelText;
			dropdown.itemText = itemLabelText;
			dropdown.template = template.GetComponent<RectTransform>();

			UiDropdown uiDropdown = new UiDropdown(go, theme, dropdown);
			uiDropdown.SetOptions(options);
			return uiDropdown;
		}

		/// <summary>Builds the Template/Viewport/Content/Item hierarchy Dropdown needs to render its open list.</summary>
		private static GameObject BuildTemplate(Transform parent, UiTheme theme, out Text itemLabelText)
		{
			GameObject template = new GameObject("Template", typeof(Image), typeof(ScrollRect));
			template.transform.SetParent(parent, false);
			RectTransform templateRect = template.GetComponent<RectTransform>();
			templateRect.anchorMin = new Vector2(0f, 0f);
			templateRect.anchorMax = new Vector2(1f, 0f);
			templateRect.pivot = new Vector2(0.5f, 1f);
			templateRect.anchoredPosition = new Vector2(0f, 2f);
			templateRect.sizeDelta = new Vector2(0f, 150f);
			template.GetComponent<Image>().color = theme.ModalPanelBackground;

			GameObject viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
			viewport.transform.SetParent(template.transform, false);
			RectTransform viewportRect = viewport.GetComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.offsetMin = Vector2.zero;
			viewportRect.offsetMax = Vector2.zero;
			viewport.GetComponent<Image>().color = Color.white;
			viewport.GetComponent<Mask>().showMaskGraphic = false;

			GameObject content = new GameObject("Content", typeof(RectTransform));
			content.transform.SetParent(viewport.transform, false);
			RectTransform contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0.5f, 1f);
			contentRect.anchoredPosition = Vector2.zero;
			contentRect.sizeDelta = new Vector2(0f, 28f);
			VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
			contentLayout.childControlHeight = false;
			contentLayout.childControlWidth = true;
			contentLayout.childForceExpandWidth = true;
			ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
			contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			GameObject item = new GameObject("Item", typeof(Toggle));
			item.transform.SetParent(content.transform, false);
			RectTransform itemRect = item.GetComponent<RectTransform>();
			itemRect.sizeDelta = new Vector2(0f, 28f);

			GameObject itemBackground = new GameObject("Item Background", typeof(Image));
			itemBackground.transform.SetParent(item.transform, false);
			RectTransform itemBackgroundRect = itemBackground.GetComponent<RectTransform>();
			itemBackgroundRect.anchorMin = Vector2.zero;
			itemBackgroundRect.anchorMax = Vector2.one;
			itemBackgroundRect.offsetMin = Vector2.zero;
			itemBackgroundRect.offsetMax = Vector2.zero;
			itemBackground.GetComponent<Image>().color = theme.RowButtonColor;

			GameObject itemCheckmark = new GameObject("Item Checkmark", typeof(Image));
			itemCheckmark.transform.SetParent(item.transform, false);
			RectTransform itemCheckmarkRect = itemCheckmark.GetComponent<RectTransform>();
			itemCheckmarkRect.anchorMin = new Vector2(0f, 0.5f);
			itemCheckmarkRect.anchorMax = new Vector2(0f, 0.5f);
			itemCheckmarkRect.sizeDelta = new Vector2(16f, 16f);
			itemCheckmarkRect.anchoredPosition = new Vector2(10f, 0f);
			itemCheckmark.GetComponent<Image>().color = theme.AccentColor;

			GameObject itemLabel = new GameObject("Item Label", typeof(Text));
			itemLabel.transform.SetParent(item.transform, false);
			RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
			itemLabelRect.anchorMin = Vector2.zero;
			itemLabelRect.anchorMax = Vector2.one;
			itemLabelRect.offsetMin = new Vector2(28f, 1f);
			itemLabelRect.offsetMax = new Vector2(-8f, -2f);
			itemLabelText = itemLabel.GetComponent<Text>();
			itemLabelText.font = theme.Font;
			itemLabelText.fontSize = theme.BodyFontSize;
			itemLabelText.color = theme.LabelColor;
			itemLabelText.alignment = TextAnchor.MiddleLeft;

			Toggle itemToggle = item.GetComponent<Toggle>();
			itemToggle.targetGraphic = itemBackground.GetComponent<Image>();
			itemToggle.graphic = itemCheckmark.GetComponent<Image>();

			ScrollRect scrollRect = template.GetComponent<ScrollRect>();
			scrollRect.content = contentRect;
			scrollRect.viewport = viewportRect;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;

			return template;
		}

		/// <summary>Replaces the dropdown's option list.</summary>
		public UiDropdown SetOptions(IEnumerable<string> options)
		{
			Dropdown.ClearOptions();
			Dropdown.AddOptions(options.ToList());
			return this;
		}

		/// <summary>Adds a handler fired when the selected index changes.</summary>
		public UiDropdown OnValueChanged(UnityAction<int> action)
		{
			Dropdown.onValueChanged.AddListener(action);
			return this;
		}

		/// <summary>Sets the selected index without firing OnValueChanged handlers.</summary>
		public UiDropdown SetValueSilently(int index)
		{
			Dropdown.DropdownEvent handlers = Dropdown.onValueChanged;
			Dropdown.onValueChanged = new Dropdown.DropdownEvent();
			Dropdown.value = index;
			Dropdown.onValueChanged = handlers;
			return this;
		}
	}
}
