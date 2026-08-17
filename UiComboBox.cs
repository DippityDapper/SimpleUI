using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>An editable text field with a dropdown arrow that opens a scrollable list of known options.</summary>
	/// <remarks>
	/// Unlike <see cref="UiDropdown"/>, the caption is a real <see cref="InputField"/> so the user can
	/// type a value that is not in the list. Picking a list item commits that string the same way
	/// pressing Enter or losing focus does. The option list reparents to the root canvas while open
	/// so it renders above sibling fields and is not clipped by nested scroll views; it closes
	/// automatically when this combobox is hidden or destroyed, or when the user clicks outside
	/// the field and popup.
	/// </remarks>
	public sealed class UiComboBox : UiElement<UiComboBox>
	{
		/// <summary>The underlying Unity InputField component.</summary>
		public InputField InputField { get; }

		private readonly List<string> options = new List<string>();
		private readonly UnityEvent<string> onEndEdit = new UnityEvent<string>();
		private readonly GameObject popupRoot;
		private readonly RectTransform popupContent;
		private readonly Transform popupOriginalParent;
		private readonly PopupRectState popupRestRect;
		private bool suppressEvents;
		private bool popupOpen;

		private struct PopupRectState
		{
			internal Vector2 AnchorMin;
			internal Vector2 AnchorMax;
			internal Vector2 Pivot;
			internal Vector2 AnchoredPosition;
			internal Vector2 SizeDelta;
		}

		private UiComboBox(GameObject gameObject, UiTheme theme, InputField inputField, GameObject popupRoot, RectTransform popupContent, Transform popupOriginalParent, PopupRectState popupRestRect)
			: base(gameObject, theme)
		{
			InputField = inputField;
			this.popupRoot = popupRoot;
			this.popupContent = popupContent;
			this.popupOriginalParent = popupOriginalParent;
			this.popupRestRect = popupRestRect;
		}

		/// <summary>Creates a combobox pre-populated with the given options.</summary>
		public static UiComboBox Create(Transform parent, IEnumerable<string> options, string defaultText = "", UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject root = new GameObject("UiComboBox", typeof(Image));
			root.transform.SetParent(parent, false);
			root.GetComponent<Image>().color = theme.FieldBackground;

			HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 0f;
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = true;
			layout.padding = new RectOffset(0, 0, 0, 0);

			GameObject inputObject = new GameObject("Input", typeof(Image), typeof(InputField));
			inputObject.transform.SetParent(root.transform, false);
			Image inputBackground = inputObject.GetComponent<Image>();
			inputBackground.color = Color.clear;

			GameObject textObject = new GameObject("Text", typeof(Text));
			textObject.transform.SetParent(inputObject.transform, false);
			RectTransform textRect = textObject.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = new Vector2(6f, 2f);
			textRect.offsetMax = new Vector2(-4f, -2f);
			Text text = textObject.GetComponent<Text>();
			text.font = theme.Font;
			text.fontSize = theme.BodyFontSize;
			text.alignment = TextAnchor.MiddleLeft;
			text.color = theme.FieldTextColor;
			text.supportRichText = false;

			InputField inputField = inputObject.GetComponent<InputField>();
			inputField.textComponent = text;
			inputField.text = defaultText ?? string.Empty;

			LayoutElement inputLayout = inputObject.AddComponent<LayoutElement>();
			inputLayout.flexibleWidth = 1f;
			inputLayout.minHeight = 24f;

			GameObject popupRoot = BuildPopup(root.transform, theme, out RectTransform popupContent);
			popupRoot.SetActive(false);
			PopupRectState popupRestRect = CapturePopupRect(popupRoot.GetComponent<RectTransform>());

			GameObject arrowObject = new GameObject("Arrow", typeof(Image), typeof(Button));
			arrowObject.transform.SetParent(root.transform, false);
			arrowObject.GetComponent<Image>().color = theme.RowButtonColor;
			LayoutElement arrowLayout = arrowObject.AddComponent<LayoutElement>();
			arrowLayout.preferredWidth = 22f;
			arrowLayout.minWidth = 22f;

			GameObject arrowLabelObject = new GameObject("Label", typeof(Text));
			arrowLabelObject.transform.SetParent(arrowObject.transform, false);
			RectTransform arrowLabelRect = arrowLabelObject.GetComponent<RectTransform>();
			arrowLabelRect.anchorMin = Vector2.zero;
			arrowLabelRect.anchorMax = Vector2.one;
			arrowLabelRect.offsetMin = Vector2.zero;
			arrowLabelRect.offsetMax = Vector2.zero;
			Text arrowLabel = arrowLabelObject.GetComponent<Text>();
			arrowLabel.text = "\u25BE";
			arrowLabel.font = theme.Font;
			arrowLabel.fontSize = theme.BodyFontSize;
			arrowLabel.alignment = TextAnchor.MiddleCenter;
			arrowLabel.color = theme.LabelColor;

			UiComboBox comboBox = new UiComboBox(root, theme, inputField, popupRoot, popupContent, root.transform, popupRestRect);
			comboBox.SetOptions(options);
			inputField.onEndEdit.AddListener(comboBox.HandleInputEndEdit);

			Button arrowButton = arrowObject.GetComponent<Button>();
			arrowButton.onClick.AddListener(comboBox.TogglePopup);

			root.AddComponent<UiComboBoxLifecycle>().Bind(comboBox);

			return comboBox;
		}

		/// <summary>Closes an open popup when the combobox is hidden or destroyed (called from <see cref="UiComboBoxLifecycle"/>).</summary>
		internal void ClosePopupDueToHide()
		{
			ClosePopup();
		}

		internal bool IsPopupOpen => popupOpen;

		internal bool IsPointerOverOwnerOrPopup()
		{
			EventSystem eventSystem = EventSystem.current;
			if (eventSystem == null)
			{
				return false;
			}

			PointerEventData pointerData = new PointerEventData(eventSystem)
			{
				position = Input.mousePosition
			};
			List<RaycastResult> hits = new List<RaycastResult>();
			eventSystem.RaycastAll(pointerData, hits);
			Transform ownerRoot = RectTransform;
			Transform popupTransform = popupRoot.transform;
			for (int i = 0; i < hits.Count; i++)
			{
				Transform hit = hits[i].gameObject.transform;
				if (hit.IsChildOf(ownerRoot) || hit.IsChildOf(popupTransform))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Replaces the dropdown option list.</summary>
		public UiComboBox SetOptions(IEnumerable<string> newOptions)
		{
			options.Clear();
			if (newOptions != null)
			{
				options.AddRange(newOptions.Where(option => !string.IsNullOrEmpty(option)).Distinct());
			}
			if (popupOpen)
			{
				RebuildPopupItems();
			}
			return this;
		}

		/// <summary>Adds a handler fired when the value is committed (list pick, Enter, or focus loss).</summary>
		public UiComboBox OnEndEdit(UnityAction<string> action)
		{
			onEndEdit.AddListener(action);
			return this;
		}

		/// <summary>Replaces the field's current text without firing OnEndEdit handlers.</summary>
		public UiComboBox SetText(string text)
		{
			suppressEvents = true;
			InputField.text = text ?? string.Empty;
			suppressEvents = false;
			return this;
		}

		private void HandleInputEndEdit(string value)
		{
			ClosePopup();
			if (!suppressEvents)
			{
				onEndEdit.Invoke(value ?? string.Empty);
			}
		}

		private void TogglePopup()
		{
			if (popupOpen)
			{
				ClosePopup();
				return;
			}
			OpenPopup();
		}

		private void OpenPopup()
		{
			RebuildPopupItems();

			Canvas rootCanvas = GetRootCanvas();
			RectTransform popupRect = popupRoot.GetComponent<RectTransform>();
			if (rootCanvas != null)
			{
				Transform canvasTransform = rootCanvas.transform;
				RectTransform canvasRect = canvasTransform as RectTransform;
				Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

				popupRoot.transform.SetParent(canvasTransform, false);
				popupRoot.transform.SetAsLastSibling();

				Vector3[] corners = new Vector3[4];
				RectTransform.GetWorldCorners(corners);

				Vector2 bottomLeft;
				Vector2 bottomRight;
				RectTransformUtility.ScreenPointToLocalPointInRectangle(
					canvasRect,
					RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]),
					eventCamera,
					out bottomLeft);
				RectTransformUtility.ScreenPointToLocalPointInRectangle(
					canvasRect,
					RectTransformUtility.WorldToScreenPoint(eventCamera, corners[3]),
					eventCamera,
					out bottomRight);

				float width = bottomRight.x - bottomLeft.x;
				popupRect.anchorMin = new Vector2(0.5f, 0.5f);
				popupRect.anchorMax = new Vector2(0.5f, 0.5f);
				popupRect.pivot = new Vector2(0.5f, 1f);
				popupRect.sizeDelta = new Vector2(width, popupRestRect.SizeDelta.y);
				popupRect.anchoredPosition = new Vector2((bottomLeft.x + bottomRight.x) * 0.5f, bottomLeft.y - 2f);
			}

			popupRoot.SetActive(true);
			popupOpen = true;
		}

		private void ClosePopup()
		{
			if (!popupOpen && !popupRoot.activeSelf)
			{
				return;
			}

			popupRoot.SetActive(false);
			popupOpen = false;

			if (popupOriginalParent != null && popupRoot.transform.parent != popupOriginalParent)
			{
				popupRoot.transform.SetParent(popupOriginalParent, false);
				RestorePopupRect();
			}
		}

		private Canvas GetRootCanvas()
		{
			Canvas canvas = RectTransform.GetComponentInParent<Canvas>();
			return canvas != null ? canvas.rootCanvas : null;
		}

		private void RestorePopupRect()
		{
			RectTransform popupRect = popupRoot.GetComponent<RectTransform>();
			popupRect.anchorMin = popupRestRect.AnchorMin;
			popupRect.anchorMax = popupRestRect.AnchorMax;
			popupRect.pivot = popupRestRect.Pivot;
			popupRect.anchoredPosition = popupRestRect.AnchoredPosition;
			popupRect.sizeDelta = popupRestRect.SizeDelta;
		}

		private static PopupRectState CapturePopupRect(RectTransform popupRect)
		{
			return new PopupRectState
			{
				AnchorMin = popupRect.anchorMin,
				AnchorMax = popupRect.anchorMax,
				Pivot = popupRect.pivot,
				AnchoredPosition = popupRect.anchoredPosition,
				SizeDelta = popupRect.sizeDelta
			};
		}

		private void CommitOption(string option)
		{
			ClosePopup();
			string value = option ?? string.Empty;
			if (InputField.text == value)
			{
				return;
			}
			suppressEvents = true;
			InputField.text = value;
			suppressEvents = false;
			onEndEdit.Invoke(value);
		}

		private void RebuildPopupItems()
		{
			for (int i = popupContent.childCount - 1; i >= 0; i--)
			{
				Object.Destroy(popupContent.GetChild(i).gameObject);
			}

			foreach (string option in options)
			{
				GameObject item = new GameObject("Item", typeof(Image), typeof(Button));
				item.transform.SetParent(popupContent, false);
				RectTransform itemRect = item.GetComponent<RectTransform>();
				itemRect.sizeDelta = new Vector2(0f, 24f);
				item.GetComponent<Image>().color = Theme.RowButtonColor;

				GameObject labelObject = new GameObject("Label", typeof(Text));
				labelObject.transform.SetParent(item.transform, false);
				RectTransform labelRect = labelObject.GetComponent<RectTransform>();
				labelRect.anchorMin = Vector2.zero;
				labelRect.anchorMax = Vector2.one;
				labelRect.offsetMin = new Vector2(8f, 1f);
				labelRect.offsetMax = new Vector2(-8f, -1f);
				Text label = labelObject.GetComponent<Text>();
				label.text = option;
				label.font = Theme.Font;
				label.fontSize = Theme.BodyFontSize;
				label.alignment = TextAnchor.MiddleLeft;
				label.color = Theme.LabelColor;

				string captured = option;
				item.GetComponent<Button>().onClick.AddListener(() => CommitOption(captured));
			}
		}

		private static GameObject BuildPopup(Transform parent, UiTheme theme, out RectTransform contentRect)
		{
			GameObject popup = new GameObject("Popup", typeof(Image), typeof(ScrollRect));
			popup.transform.SetParent(parent, false);
			LayoutElement popupLayout = popup.AddComponent<LayoutElement>();
			popupLayout.ignoreLayout = true;
			RectTransform popupRect = popup.GetComponent<RectTransform>();
			popupRect.anchorMin = new Vector2(0f, 0f);
			popupRect.anchorMax = new Vector2(1f, 0f);
			popupRect.pivot = new Vector2(0.5f, 1f);
			popupRect.anchoredPosition = new Vector2(0f, 0f);
			popupRect.sizeDelta = new Vector2(0f, 150f);
			popup.GetComponent<Image>().color = theme.ModalPanelBackground;

			GameObject viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
			viewport.transform.SetParent(popup.transform, false);
			RectTransform viewportRect = viewport.GetComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.offsetMin = Vector2.zero;
			viewportRect.offsetMax = Vector2.zero;
			viewport.GetComponent<Image>().color = Color.white;
			viewport.GetComponent<Mask>().showMaskGraphic = false;

			GameObject content = new GameObject("Content", typeof(RectTransform));
			content.transform.SetParent(viewport.transform, false);
			contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0.5f, 1f);
			contentRect.anchoredPosition = Vector2.zero;
			contentRect.sizeDelta = new Vector2(0f, 24f);
			VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
			contentLayout.childControlHeight = false;
			contentLayout.childControlWidth = true;
			contentLayout.childForceExpandWidth = true;
			ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
			contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			ScrollRect scrollRect = popup.GetComponent<ScrollRect>();
			scrollRect.content = contentRect;
			scrollRect.viewport = viewportRect;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;

			return popup;
		}

		/// <summary>Hides the canvas-reparented popup when this combobox leaves the active hierarchy, or when the user clicks outside it.</summary>
		private sealed class UiComboBoxLifecycle : MonoBehaviour
		{
			private UiComboBox owner;

			internal void Bind(UiComboBox owner)
			{
				this.owner = owner;
			}

			private void OnDisable()
			{
				owner?.ClosePopupDueToHide();
			}

			private void LateUpdate()
			{
				if (owner == null || !owner.IsPopupOpen)
				{
					return;
				}
				if (Input.GetMouseButtonDown(0) && !owner.IsPointerOverOwnerOrPopup())
				{
					owner.ClosePopupDueToHide();
				}
			}
		}
	}
}
