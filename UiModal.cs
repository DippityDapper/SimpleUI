using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A centered popup with a dimmed, click-to-close backdrop. Starts hidden; call Show()/Hide() to toggle.</summary>
	/// <remarks>Formalizes EditorUiHelpers.CreateModal/ShowModal/HideModal. Content is added the same way as UiPanel (it wraps one internally).</remarks>
	public sealed class UiModal : UiElement<UiModal>
	{
		/// <summary>The Transform child content should be parented under (via Add).</summary>
		public Transform ContentParent => panel.ContentParent;

		private readonly GameObject backdrop;
		private readonly UiPanel panel;

		/// <summary>Wraps the already-built backdrop and centered panel.</summary>
		/// <remarks>Base GameObject is the centered host ("Modal"), not the UiPanel it wraps -- Show/Hide must toggle the host (which carries the actual screen position/size) or the panel would stay invisible regardless of the inner UiPanel object's own active state.</remarks>
		private UiModal(GameObject centerHost, GameObject backdrop, UiPanel panel, UiTheme theme) : base(centerHost, theme)
		{
			this.backdrop = backdrop;
			this.panel = panel;
		}

		/// <summary>Creates a hidden modal with a dimmed backdrop and a centered, fixed-size panel.</summary>
		public static UiModal Create(Transform canvas, UiTheme theme = null, string title = null,
			float widthPx = 600f, float heightPx = 400f)
		{
			theme = theme ?? UiTheme.Default;

			GameObject backdropObject = new GameObject("ModalBackdrop", typeof(Image), typeof(Button));
			backdropObject.transform.SetParent(canvas, false);
			RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
			backdropRect.anchorMin = Vector2.zero;
			backdropRect.anchorMax = Vector2.one;
			backdropRect.offsetMin = Vector2.zero;
			backdropRect.offsetMax = Vector2.zero;
			backdropObject.GetComponent<Image>().color = theme.ModalBackdrop;

			GameObject centerHost = new GameObject("Modal", typeof(RectTransform));
			centerHost.transform.SetParent(canvas, false);
			RectTransform centerRect = centerHost.GetComponent<RectTransform>();
			centerRect.anchorMin = new Vector2(0.5f, 0.5f);
			centerRect.anchorMax = new Vector2(0.5f, 0.5f);
			centerRect.sizeDelta = new Vector2(widthPx, heightPx);
			centerRect.anchoredPosition = Vector2.zero;

			UiPanel panel = UiPanel.Create(centerHost.transform, theme, title);
			panel.GameObject.GetComponent<Image>().color = theme.ModalPanelBackground;

			UiModal modal = new UiModal(centerHost, backdropObject, panel, theme);
			backdropObject.GetComponent<Button>().onClick.AddListener(modal.Hide);

			backdropObject.SetActive(false);
			centerHost.SetActive(false);

			return modal;
		}

		/// <summary>Parents a child element under this modal's content area.</summary>
		public UiModal Add(UiElement content)
		{
			panel.Add(content);
			return this;
		}

		/// <summary>Shows the modal and brings it to the front (above every static panel/dropdown).</summary>
		public void Show()
		{
			backdrop.SetActive(true);
			GameObject.SetActive(true);
			backdrop.transform.SetAsLastSibling();
			GameObject.transform.SetAsLastSibling();
		}

		/// <summary>Hides the modal and its backdrop.</summary>
		public void Hide()
		{
			backdrop.SetActive(false);
			GameObject.SetActive(false);
		}
	}
}
