using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>Searchable card list of image / name / id rows with single-select and optional activate.</summary>
	/// <remarks>
	/// Built-in search matches name and/or id (substring, case-insensitive). When
	/// <c>scrollable</c> is true the card list is the only ScrollRect — parent this with
	/// Grow() in a non-scroll host. Cards are created in batches as the user scrolls so a
	/// thousand-row catalog does not instantiate every row on open. Do not nest inside
	/// another scrollable stack.
	/// </remarks>
	public sealed class UiCatalogue : UiElement<UiCatalogue>
	{
		/// <summary>How many cards are created in each scroll batch.</summary>
		public const int DefaultBatchSize = 24;

		private readonly UiTextField searchField;
		private readonly UiStack list;
		private readonly ScrollRect scrollRect;
		private readonly List<UiCatalogueItem> items = new List<UiCatalogueItem>();
		private readonly List<UiCatalogueItem> filtered = new List<UiCatalogueItem>();
		private readonly List<CardRow> cards = new List<CardRow>();
		private string filter = string.Empty;
		private string selectedId;
		private int revealed;
		private int batchSize = DefaultBatchSize;
		private Action<UiCatalogueItem> onSelected;
		private Action<UiCatalogueItem> onActivated;
		private Action<UiCatalogueItem> onItemShown;
		private Action<UiCatalogueItem, Vector2> onDropped;

		private sealed class CardRow
		{
			internal UiCatalogueItem Item;
			internal Image Background;
			internal UiImage Thumb;
		}

		private UiCatalogue(GameObject gameObject, UiTheme theme, UiTextField searchField, UiStack list, ScrollRect scrollRect)
			: base(gameObject, theme)
		{
			this.searchField = searchField;
			this.list = list;
			this.scrollRect = scrollRect;
		}

		/// <summary>The search field used to filter name and id.</summary>
		public UiTextField SearchField => searchField;

		/// <summary>Id of the highlighted card, or null when nothing is selected.</summary>
		public string SelectedId => selectedId;

		/// <summary>The highlighted item, or null when nothing is selected.</summary>
		public UiCatalogueItem SelectedItem
		{
			get
			{
				return FindItem(selectedId);
			}
		}

		/// <summary>Creates a catalogue. Pass scrollable false only when a parent stack already scrolls.</summary>
		public static UiCatalogue Create(Transform parent, UiTheme theme = null, bool scrollable = true)
		{
			theme = theme ?? UiTheme.Default;
			UiStack root = UiStack.Vertical(parent, theme, spacing: 4f, padding: 0f, scrollable: false);
			root.GameObject.name = "UiCatalogue";
			UiLabel hint = UiLabel.Create(root.ContentTransform, "Search name or id", theme, 11);
			root.Add(hint.FixedHeight(16f));
			UiTextField search = UiTextField.Create(root.ContentTransform, string.Empty, theme);
			root.Add(search.FixedHeight(28f));
			UiStack list = UiStack.Vertical(root.ContentTransform, theme, spacing: 4f, padding: 0f, scrollable: scrollable);
			if (scrollable)
			{
				root.Add(list.Grow());
			}
			else
			{
				root.Add(list);
			}

			ScrollRect scroll = scrollable ? list.GameObject.GetComponent<ScrollRect>() : null;
			UiCatalogue catalogue = new UiCatalogue(root.GameObject, theme, search, list, scroll);
			search.OnChange(catalogue.OnSearchChanged);
			if (scroll != null)
			{
				UiCataloguePager pager = list.GameObject.GetComponent<UiCataloguePager>();
				if (pager == null)
				{
					pager = list.GameObject.AddComponent<UiCataloguePager>();
				}

				pager.Configure(scroll, catalogue.HasMore, catalogue.TryRevealMore);
			}

			return catalogue;
		}

		/// <summary>True when query is empty or it appears in id and/or name (case-insensitive).</summary>
		public static bool Matches(string id, string name, string query)
		{
			if (string.IsNullOrWhiteSpace(query))
			{
				return true;
			}

			string needle = query.Trim();
			return ContainsIgnoreCase(id, needle) || ContainsIgnoreCase(name, needle);
		}

		/// <summary>How many filtered rows to instantiate per scroll batch. Values below 1 become 1.</summary>
		public UiCatalogue SetBatchSize(int size)
		{
			batchSize = size < 1 ? 1 : size;
			return this;
		}

		/// <summary>Replaces the full item list and rebuilds the first visible batch.</summary>
		public UiCatalogue SetItems(IReadOnlyList<UiCatalogueItem> next)
		{
			items.Clear();
			if (next != null)
			{
				for (int i = 0; i < next.Count; i++)
				{
					if (next[i] != null && !string.IsNullOrEmpty(next[i].Id))
					{
						items.Add(next[i]);
					}
				}
			}

			if (FindItem(selectedId) == null)
			{
				selectedId = null;
			}

			Rebuild();
			return this;
		}

		/// <summary>Applies a filter string and syncs the search field.</summary>
		public UiCatalogue SetFilter(string query)
		{
			filter = query ?? string.Empty;
			if (searchField.InputField.text != filter)
			{
				searchField.SetText(filter);
			}

			Rebuild();
			return this;
		}

		/// <summary>Highlights the card with this id without firing OnSelected.</summary>
		public UiCatalogue SetSelectedId(string id)
		{
			selectedId = FindItem(id) != null ? id : null;
			RefreshHighlights();
			return this;
		}

		/// <summary>Sets or replaces the thumbnail on a row, including rows not yet revealed.</summary>
		public UiCatalogue SetItemImage(string id, Sprite sprite)
		{
			UiCatalogueItem item = FindItem(id);
			if (item != null)
			{
				item.Image = sprite;
			}

			for (int i = 0; i < cards.Count; i++)
			{
				CardRow card = cards[i];
				if (card.Item == null || card.Thumb == null
					|| !string.Equals(card.Item.Id, id, StringComparison.Ordinal))
				{
					continue;
				}

				card.Thumb.SetSprite(sprite);
				card.Thumb.Image.color = sprite != null ? Color.white : new Color(0.12f, 0.13f, 0.16f, 1f);
			}

			return this;
		}

		/// <summary>Adds a handler fired when a card is clicked (single click).</summary>
		public UiCatalogue OnSelected(Action<UiCatalogueItem> handler)
		{
			onSelected += handler;
			return this;
		}

		/// <summary>Adds a handler fired when a card is double-clicked.</summary>
		public UiCatalogue OnActivated(Action<UiCatalogueItem> handler)
		{
			onActivated += handler;
			return this;
		}

		/// <summary>Adds a handler fired once when a card is first instantiated in a batch.</summary>
		public UiCatalogue OnItemShown(Action<UiCatalogueItem> handler)
		{
			onItemShown += handler;
			return this;
		}

		/// <summary>Adds a handler fired when a card is dragged out of the list and released.</summary>
		/// <remarks>
		/// Vertical drag inside the list still scrolls. Once the pointer leaves
		/// the list viewport (or pulls sideways), the card owns the drag so
		/// ScrollRect cannot swallow a place gesture. A click that stays on the
		/// card is still a select.
		/// </remarks>
		public UiCatalogue OnDropped(Action<UiCatalogueItem, Vector2> handler)
		{
			onDropped += handler;
			return this;
		}

		private void OnSearchChanged(string text)
		{
			filter = text ?? string.Empty;
			Rebuild();
		}

		private void Rebuild()
		{
			filtered.Clear();
			for (int i = 0; i < items.Count; i++)
			{
				UiCatalogueItem item = items[i];
				if (Matches(item.Id, item.Name, filter))
				{
					filtered.Add(item);
				}
			}

			list.Clear();
			cards.Clear();
			revealed = 0;
			if (filtered.Count == 0)
			{
				list.Add(UiLabel.Create(list.ContentTransform, "No matches.", Theme, 12)
					.FixedHeight(22f));
				return;
			}

			if (scrollRect == null)
			{
				RevealTo(filtered.Count);
				return;
			}

			RevealTo(NextBatchEnd(revealed, filtered.Count, batchSize));
		}

		private bool HasMore()
		{
			return revealed < filtered.Count;
		}

		private void TryRevealMore()
		{
			if (!HasMore())
			{
				return;
			}

			RevealTo(NextBatchEnd(revealed, filtered.Count, batchSize));
		}

		private void RevealTo(int next)
		{
			if (next > filtered.Count)
			{
				next = filtered.Count;
			}

			for (int i = revealed; i < next; i++)
			{
				UiCatalogueItem item = filtered[i];
				cards.Add(BuildCard(item));
				onItemShown?.Invoke(item);
			}

			revealed = next;
			RefreshHighlights();
		}

		private CardRow BuildCard(UiCatalogueItem item)
		{
			UiStack row = UiStack.Horizontal(list.ContentTransform, Theme, spacing: 8f, padding: 6f);
			Image background = row.GameObject.GetComponent<Image>();
			if (background == null)
			{
				background = row.GameObject.AddComponent<Image>();
			}

			background.color = Theme.RowButtonColor;
			background.raycastTarget = true;
			Button button = row.GameObject.GetComponent<Button>();
			if (button == null)
			{
				button = row.GameObject.AddComponent<Button>();
			}

			button.transition = Selectable.Transition.None;
			UiImage thumb = UiImage.Create(row.ContentTransform, item.Image, Theme);
			thumb.Image.color = item.Image != null ? Color.white : new Color(0.12f, 0.13f, 0.16f, 1f);
			row.Add(thumb.FixedWidth(48f).FixedHeight(48f));
			UiStack labels = UiStack.Vertical(row.ContentTransform, Theme, spacing: 2f, padding: 0f);
			string name = string.IsNullOrEmpty(item.Name) ? item.Id : item.Name;
			labels.Add(UiLabel.Create(labels.ContentTransform, name, Theme, 13, TextAnchor.MiddleLeft)
				.FixedHeight(20f));
			UiLabel idLabel = UiLabel.Create(labels.ContentTransform, item.Id, Theme, 11, TextAnchor.MiddleLeft);
			idLabel.Text.color = new Color(Theme.LabelColor.r, Theme.LabelColor.g, Theme.LabelColor.b, 0.7f);
			labels.Add(idLabel.FixedHeight(18f));
			row.Add(labels.Grow());
			list.Add(row.FixedHeight(60f));

			UiCatalogueClick click = row.GameObject.GetComponent<UiCatalogueClick>();
			if (click == null)
			{
				click = row.GameObject.AddComponent<UiCatalogueClick>();
			}

			RectTransform viewport = scrollRect != null
				? (scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform)
				: null;
			click.Configure(
				() => Select(item),
				() => Activate(item),
				screen => onDropped?.Invoke(item, screen),
				item,
				scrollRect,
				viewport);
			return new CardRow { Item = item, Background = background, Thumb = thumb };
		}

		private void Select(UiCatalogueItem item)
		{
			if (item == null)
			{
				return;
			}

			selectedId = item.Id;
			RefreshHighlights();
			onSelected?.Invoke(item);
		}

		private void Activate(UiCatalogueItem item)
		{
			if (item == null)
			{
				return;
			}

			if (selectedId != item.Id)
			{
				Select(item);
			}

			onActivated?.Invoke(item);
		}

		private void RefreshHighlights()
		{
			for (int i = 0; i < cards.Count; i++)
			{
				CardRow card = cards[i];
				if (card.Background == null || card.Item == null)
				{
					continue;
				}

				bool selected = string.Equals(card.Item.Id, selectedId, StringComparison.Ordinal);
				card.Background.color = selected ? Theme.AccentColor : Theme.RowButtonColor;
			}
		}

		private UiCatalogueItem FindItem(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}

			for (int i = 0; i < items.Count; i++)
			{
				if (string.Equals(items[i].Id, id, StringComparison.Ordinal))
				{
					return items[i];
				}
			}

			return null;
		}

		/// <summary>End index of the next reveal batch, clamped to total.</summary>
		internal static int NextBatchEnd(int revealedCount, int total, int size)
		{
			if (revealedCount < 0)
			{
				revealedCount = 0;
			}

			if (total < 0)
			{
				total = 0;
			}

			if (size < 1)
			{
				size = 1;
			}

			if (revealedCount >= total)
			{
				return total;
			}

			int next = revealedCount + size;
			return next > total ? total : next;
		}

		private static bool ContainsIgnoreCase(string value, string needle)
		{
			return !string.IsNullOrEmpty(value)
				&& value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}

	/// <summary>Reveals the next catalogue batch when the list is short or the scroll is near the bottom.</summary>
	internal sealed class UiCataloguePager : MonoBehaviour
	{
		private ScrollRect scroll;
		private Func<bool> hasMore;
		private Action revealMore;

		/// <summary>Binds the scroll view and the catalogue's reveal callbacks.</summary>
		internal void Configure(ScrollRect scrollRect, Func<bool> more, Action reveal)
		{
			scroll = scrollRect;
			hasMore = more;
			revealMore = reveal;
		}

		private void LateUpdate()
		{
			if (scroll == null || hasMore == null || revealMore == null || !hasMore())
			{
				return;
			}

			RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
			RectTransform content = scroll.content;
			if (viewport == null || content == null)
			{
				return;
			}

			float viewHeight = viewport.rect.height;
			float contentHeight = content.rect.height;
			if (contentHeight <= viewHeight + 8f || scroll.verticalNormalizedPosition <= 0.12f)
			{
				revealMore();
			}
		}
	}

	/// <summary>Click target that distinguishes select, double-click activate, in-list scroll, and drag-out drop.</summary>
	/// <remarks>
	/// The card implements drag so it can take the pointer away from the parent
	/// ScrollRect once the cursor leaves the list. In-list vertical drag is
	/// forwarded so the catalogue still scrolls.
	/// </remarks>
	internal sealed class UiCatalogueClick : MonoBehaviour,
		IPointerClickHandler,
		IPointerDownHandler,
		IInitializePotentialDragHandler,
		IBeginDragHandler,
		IDragHandler,
		IEndDragHandler
	{
		private const float PullOutPixels = 16f;

		private Action onSelect;
		private Action onActivate;
		private Action<Vector2> onDrop;
		private UiCatalogueItem item;
		private ScrollRect scroll;
		private RectTransform listViewport;
		private bool pullingOut;
		private bool forwardedScroll;
		private Vector2 startScreen;

		/// <summary>Sets the select, activate, drop, card, and optional list-scroll targets for this card.</summary>
		internal void Configure(
			Action select,
			Action activate,
			Action<Vector2> drop,
			UiCatalogueItem catalogueItem,
			ScrollRect scrollRect,
			RectTransform viewport)
		{
			onSelect = select;
			onActivate = activate;
			onDrop = drop;
			item = catalogueItem;
			scroll = scrollRect;
			listViewport = viewport;
		}

		/// <summary>Highlights the card as soon as the pointer goes down so a drag stays selected.</summary>
		public void OnPointerDown(PointerEventData eventData)
		{
			pullingOut = false;
			forwardedScroll = false;
			onSelect?.Invoke();
		}

		/// <summary>Forwards the potential-drag to the list ScrollRect when present.</summary>
		public void OnInitializePotentialDrag(PointerEventData eventData)
		{
			if (scroll != null)
			{
				scroll.OnInitializePotentialDrag(eventData);
			}
		}

		/// <summary>Starts a list scroll when the drag begins inside the catalogue.</summary>
		public void OnBeginDrag(PointerEventData eventData)
		{
			if (eventData == null)
			{
				return;
			}

			startScreen = eventData.position;
			if (scroll != null && IsInsideList(eventData.position))
			{
				forwardedScroll = true;
				scroll.OnBeginDrag(eventData);
				return;
			}

			BeginPullOut(eventData.position);
		}

		/// <summary>Scrolls the list until the pointer leaves it, then owns the drag for a drop.</summary>
		public void OnDrag(PointerEventData eventData)
		{
			if (eventData == null)
			{
				return;
			}

			if (!pullingOut && ShouldPullOut(eventData))
			{
				if (forwardedScroll && scroll != null)
				{
					scroll.OnEndDrag(eventData);
					forwardedScroll = false;
				}

				BeginPullOut(eventData.position);
			}

			if (pullingOut)
			{
				UiCatalogueDragGhost.Move(eventData.position);
				return;
			}

			if (forwardedScroll && scroll != null)
			{
				scroll.OnDrag(eventData);
			}
		}

		/// <summary>Ends a forwarded scroll, then reports a drop when the card was pulled out.</summary>
		public void OnEndDrag(PointerEventData eventData)
		{
			if (forwardedScroll && scroll != null)
			{
				scroll.OnEndDrag(eventData);
				forwardedScroll = false;
			}

			if (eventData != null && pullingOut)
			{
				onDrop?.Invoke(eventData.position);
			}

			UiCatalogueDragGhost.Hide();
			pullingOut = false;
		}

		private void BeginPullOut(Vector2 screen)
		{
			pullingOut = true;
			Canvas host = listViewport != null
				? listViewport.GetComponentInParent<Canvas>()
				: GetComponentInParent<Canvas>();
			UiCatalogueDragGhost.Show(item, screen, host);
		}

		/// <summary>Routes a pointer click to select or activate.</summary>
		public void OnPointerClick(PointerEventData eventData)
		{
			if (eventData != null && eventData.clickCount >= 2)
			{
				onActivate?.Invoke();
				return;
			}

			onSelect?.Invoke();
		}

		private bool ShouldPullOut(PointerEventData eventData)
		{
			if (scroll == null || !IsInsideList(eventData.position))
			{
				return true;
			}

			float dx = eventData.position.x - startScreen.x;
			float dy = eventData.position.y - startScreen.y;
			return Mathf.Abs(dx) >= PullOutPixels && Mathf.Abs(dx) > Mathf.Abs(dy);
		}

		private bool IsInsideList(Vector2 screen)
		{
			if (listViewport == null)
			{
				return false;
			}

			Canvas canvas = listViewport.GetComponentInParent<Canvas>();
			Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
				? canvas.worldCamera
				: null;
			return RectTransformUtility.RectangleContainsScreenPoint(listViewport, screen, uiCamera);
		}
	}
}
