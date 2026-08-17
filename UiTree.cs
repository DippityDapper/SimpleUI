using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>One node in a UiTree: an id, label, optional icon key, payload, expansion state, and children.</summary>
	public sealed class UiTreeItem
	{
		/// <summary>Stable id within the tree (used as the row key).</summary>
		public string Id;
		/// <summary>Text shown on the row.</summary>
		public string Label;
		/// <summary>Optional icon key; when non-empty it is shown as a short prefix (the tree does not load sprites itself).</summary>
		public string IconKey;
		/// <summary>Caller-owned payload this row represents.</summary>
		public object UserData;
		/// <summary>Whether this node's children are currently visible.</summary>
		public bool Expanded = true;
		/// <summary>Child nodes. Never null after construction.</summary>
		public List<UiTreeItem> Children = new List<UiTreeItem>();
	}

	/// <summary>A generic indented tree list: expand/collapse, multi-select, and drag-to-reparent.</summary>
	/// <remarks>
	/// Promotes the row-list pattern duplicated across SceneTreePanel / InspectorPanel / EditHistoryPanel
	/// into one shared widget. The tree is a presentation of the caller's UiTreeItem graph — it does
	/// not persist anything. Drag-reorder reparents in that in-memory graph and fires OnReordered;
	/// the caller decides whether that maps onto an on-disk document.
	/// </remarks>
	public sealed class UiTree : UiElement<UiTree>
	{
		private readonly UiStack stack;
		private readonly List<UiTreeItem> roots = new List<UiTreeItem>();
		private readonly List<UiTreeItem> selected = new List<UiTreeItem>();
		private readonly Dictionary<string, GameObject> rowsById = new Dictionary<string, GameObject>();
		private Action<IReadOnlyList<UiTreeItem>> onSelectionChanged;
		private Action<UiTreeItem, UiTreeItem, int> onReordered;
		private Action<UiTreeItem, PointerEventData> onRowRightClick;
		private Action<UiTreeItem> onRowActivated;
		private UiTreeItem draggingItem;
		private bool reorderable = true;

		/// <summary>Currently selected items. Primary selection is index 0 when any are selected.</summary>
		public IReadOnlyList<UiTreeItem> SelectedItems => selected;

		/// <summary>Wraps an already-built scrollable stack.</summary>
		private UiTree(UiStack stack, UiTheme theme) : base(stack.GameObject, theme)
		{
			this.stack = stack;
		}

		/// <summary>Creates an empty scrollable tree filling its parent.</summary>
		public static UiTree Create(Transform parent, UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;
			UiStack stack = UiStack.Vertical(parent, theme, spacing: 0f, padding: 2f, scrollable: true);
			return new UiTree(stack, theme);
		}

		/// <summary>Replaces the tree's roots and rebuilds visible rows.</summary>
		public UiTree SetRoots(IEnumerable<UiTreeItem> items)
		{
			roots.Clear();
			if (items != null)
			{
				roots.AddRange(items);
			}
			Rebuild();
			return this;
		}

		/// <summary>Registers a callback invoked when the selection set changes.</summary>
		public UiTree OnSelectionChanged(Action<IReadOnlyList<UiTreeItem>> callback)
		{
			onSelectionChanged = callback;
			return this;
		}

		/// <summary>Registers a callback invoked after a drag-reparent (dragged, new parent or null for root, index in that parent).</summary>
		public UiTree OnReordered(Action<UiTreeItem, UiTreeItem, int> callback)
		{
			onReordered = callback;
			return this;
		}

		/// <summary>Registers a callback invoked on right-click of a row.</summary>
		public UiTree OnRowRightClick(Action<UiTreeItem, PointerEventData> callback)
		{
			onRowRightClick = callback;
			return this;
		}

		/// <summary>Registers a callback invoked on double-click of a row (after selection updates).</summary>
		public UiTree OnRowActivated(Action<UiTreeItem> callback)
		{
			onRowActivated = callback;
			return this;
		}

		/// <summary>Enables or disables drag-to-reparent (default true). File Tree turns this off — disk layout is not a document.</summary>
		public UiTree SetReorderable(bool value)
		{
			reorderable = value;
			return this;
		}

		/// <summary>Selects a single item by id, or clears selection when id is null/unknown.</summary>
		public UiTree Select(string id)
		{
			selected.Clear();
			UiTreeItem item = FindById(id);
			if (item != null)
			{
				selected.Add(item);
			}
			RefreshRowColors();
			onSelectionChanged?.Invoke(selected);
			return this;
		}

		/// <summary>Expands every node and rebuilds.</summary>
		public UiTree ExpandAll()
		{
			SetExpandedRecursive(roots, true);
			Rebuild();
			return this;
		}

		/// <summary>Collapses every node and rebuilds.</summary>
		public UiTree CollapseAll()
		{
			SetExpandedRecursive(roots, false);
			Rebuild();
			return this;
		}

		/// <summary>Rebuilds visible rows from the current in-memory graph without replacing the roots.</summary>
		public UiTree Refresh()
		{
			Rebuild();
			return this;
		}

		/// <summary>Looks up a node by id in the current tree.</summary>
		public UiTreeItem FindById(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}
			return FindById(roots, id);
		}

		private static UiTreeItem FindById(List<UiTreeItem> items, string id)
		{
			foreach (UiTreeItem item in items)
			{
				if (item.Id == id)
				{
					return item;
				}
				UiTreeItem child = FindById(item.Children, id);
				if (child != null)
				{
					return child;
				}
			}
			return null;
		}

		private static void SetExpandedRecursive(List<UiTreeItem> items, bool expanded)
		{
			foreach (UiTreeItem item in items)
			{
				item.Expanded = expanded;
				SetExpandedRecursive(item.Children, expanded);
			}
		}

		private void Rebuild()
		{
			stack.Clear();
			rowsById.Clear();
			List<VisibleRow> visible = new List<VisibleRow>();
			Flatten(roots, null, 0, visible);
			foreach (VisibleRow row in visible)
			{
				GameObject rowObject = BuildRow(row);
				rowsById[row.Item.Id] = rowObject;
			}
			RefreshRowColors();
		}

		private void Flatten(List<UiTreeItem> items, UiTreeItem parent, int depth, List<VisibleRow> into)
		{
			for (int i = 0; i < items.Count; i++)
			{
				UiTreeItem item = items[i];
				into.Add(new VisibleRow { Item = item, Parent = parent, Depth = depth, IndexInParent = i });
				if (item.Expanded && item.Children.Count > 0)
				{
					Flatten(item.Children, item, depth + 1, into);
				}
			}
		}

		private GameObject BuildRow(VisibleRow row)
		{
			UiTreeItem item = row.Item;
			GameObject rowObject = new GameObject("Row_" + item.Id, typeof(Image));
			rowObject.transform.SetParent(stack.ContentTransform, false);
			Image background = rowObject.GetComponent<Image>();
			background.color = Theme.TreeRowBackground;
			LayoutElement layout = rowObject.AddComponent<LayoutElement>();
			layout.minHeight = Theme.TreeRowHeight;
			layout.preferredHeight = Theme.TreeRowHeight;
			layout.flexibleWidth = 1f;

			HorizontalLayoutGroup rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
			rowLayout.childAlignment = TextAnchor.MiddleLeft;
			rowLayout.childControlWidth = true;
			rowLayout.childControlHeight = true;
			rowLayout.childForceExpandWidth = false;
			rowLayout.childForceExpandHeight = true;
			rowLayout.padding = new RectOffset(Mathf.RoundToInt(row.Depth * Theme.TreeIndent), 4, 0, 0);
			rowLayout.spacing = 2f;

			bool hasChildren = item.Children.Count > 0;
			GameObject expander = new GameObject("Expander", typeof(Image), typeof(Button));
			expander.transform.SetParent(rowObject.transform, false);
			expander.GetComponent<Image>().color = Color.clear;
			LayoutElement expanderLayout = expander.AddComponent<LayoutElement>();
			expanderLayout.minWidth = 16f;
			expanderLayout.preferredWidth = 16f;
			expanderLayout.flexibleWidth = 0f;
			GameObject expanderLabelObject = new GameObject("Label", typeof(Text));
			expanderLabelObject.transform.SetParent(expander.transform, false);
			UiLayoutUtil.Stretch(expanderLabelObject.GetComponent<RectTransform>());
			Text expanderLabel = expanderLabelObject.GetComponent<Text>();
			expanderLabel.font = Theme.Font;
			expanderLabel.fontSize = Theme.BodyFontSize;
			expanderLabel.alignment = TextAnchor.MiddleCenter;
			expanderLabel.color = Theme.LabelColor;
			expanderLabel.raycastTarget = false;
			expanderLabel.text = hasChildren ? (item.Expanded ? "-" : "+") : " ";
			if (hasChildren)
			{
				expander.GetComponent<Button>().onClick.AddListener(() =>
				{
					item.Expanded = !item.Expanded;
					Rebuild();
				});
			}

			string caption = string.IsNullOrEmpty(item.IconKey) ? item.Label : ("[" + item.IconKey + "] " + item.Label);
			GameObject labelObject = new GameObject("Label", typeof(Text));
			labelObject.transform.SetParent(rowObject.transform, false);
			Text label = labelObject.GetComponent<Text>();
			label.text = caption ?? string.Empty;
			label.font = Theme.Font;
			label.fontSize = Theme.BodyFontSize;
			label.alignment = TextAnchor.MiddleLeft;
			label.color = Theme.LabelColor;
			label.raycastTarget = false;
			label.horizontalOverflow = HorizontalWrapMode.Overflow;
			label.verticalOverflow = VerticalWrapMode.Truncate;
			LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
			labelLayout.flexibleWidth = 1f;
			labelLayout.minWidth = 40f;
			labelLayout.preferredWidth = 40f;

			UiTreeRowHandle handle = rowObject.AddComponent<UiTreeRowHandle>();
			handle.Bind(this, item);
			return rowObject;
		}

		private void RefreshRowColors()
		{
			foreach (KeyValuePair<string, GameObject> pair in rowsById)
			{
				bool isSelected = false;
				foreach (UiTreeItem item in selected)
				{
					if (item.Id == pair.Key)
					{
						isSelected = true;
						break;
					}
				}
				Image image = pair.Value.GetComponent<Image>();
				image.color = isSelected ? Theme.TreeSelection : Theme.TreeRowBackground;
			}
		}

		internal void HandleRowClick(UiTreeItem item, PointerEventData eventData)
		{
			if (eventData.button == PointerEventData.InputButton.Right)
			{
				if (!selected.Contains(item))
				{
					selected.Clear();
					selected.Add(item);
					RefreshRowColors();
					onSelectionChanged?.Invoke(selected);
				}
				onRowRightClick?.Invoke(item, eventData);
				return;
			}

			bool additive = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
			if (additive)
			{
				if (selected.Contains(item))
				{
					selected.Remove(item);
				}
				else
				{
					selected.Add(item);
				}
			}
			else
			{
				selected.Clear();
				selected.Add(item);
			}
			RefreshRowColors();
			onSelectionChanged?.Invoke(selected);

			if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
			{
				onRowActivated?.Invoke(item);
			}
		}

		internal void HandleDragBegin(UiTreeItem item)
		{
			if (!reorderable)
			{
				return;
			}

			draggingItem = item;
		}

		internal void HandleDragEnd(UiTreeItem item, PointerEventData eventData)
		{
			if (draggingItem == null)
			{
				return;
			}
			UiTreeItem target = ItemUnderPointer(eventData);
			draggingItem = null;
			if (target == null || target == item || IsAncestor(item, target))
			{
				return;
			}

			Detach(item);
			target.Children.Add(item);
			target.Expanded = true;
			Rebuild();
			onReordered?.Invoke(item, target, target.Children.Count - 1);
		}

		private UiTreeItem ItemUnderPointer(PointerEventData eventData)
		{
			foreach (KeyValuePair<string, GameObject> pair in rowsById)
			{
				RectTransform rect = pair.Value.GetComponent<RectTransform>();
				if (RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventData.pressEventCamera))
				{
					return FindById(pair.Key);
				}
			}
			return null;
		}

		private bool IsAncestor(UiTreeItem ancestor, UiTreeItem node)
		{
			foreach (UiTreeItem child in ancestor.Children)
			{
				if (child == node || IsAncestor(child, node))
				{
					return true;
				}
			}
			return false;
		}

		private void Detach(UiTreeItem item)
		{
			if (roots.Remove(item))
			{
				return;
			}
			DetachFrom(roots, item);
		}

		private static bool DetachFrom(List<UiTreeItem> items, UiTreeItem item)
		{
			foreach (UiTreeItem candidate in items)
			{
				if (candidate.Children.Remove(item))
				{
					return true;
				}
				if (DetachFrom(candidate.Children, item))
				{
					return true;
				}
			}
			return false;
		}

		private struct VisibleRow
		{
			internal UiTreeItem Item;
			internal UiTreeItem Parent;
			internal int Depth;
			internal int IndexInParent;
		}

		/// <summary>Pointer target on a tree row: click to select, drag to reparent onto another row.</summary>
		internal sealed class UiTreeRowHandle : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
		{
			private UiTree owner;
			private UiTreeItem item;
			private bool didDrag;

			/// <summary>Binds this handle to a tree row.</summary>
			internal void Bind(UiTree tree, UiTreeItem rowItem)
			{
				owner = tree;
				item = rowItem;
			}

			/// <summary>Selects the row unless this press became a drag.</summary>
			public void OnPointerClick(PointerEventData eventData)
			{
				if (didDrag)
				{
					return;
				}
				owner?.HandleRowClick(item, eventData);
			}

			/// <summary>Starts a row drag, unless the tree is not reorderable.</summary>
			public void OnBeginDrag(PointerEventData eventData)
			{
				if (owner == null || !owner.reorderable)
				{
					return;
				}

				didDrag = true;
				owner.HandleDragBegin(item);
			}

			/// <summary>Required by IDragHandler so Unity treats this as a drag rather than a click.</summary>
			public void OnDrag(PointerEventData eventData)
			{
			}

			/// <summary>Ends a row drag and attempts a reparent onto the row under the pointer.</summary>
			public void OnEndDrag(PointerEventData eventData)
			{
				owner?.HandleDragEnd(item, eventData);
				didDrag = false;
			}
		}
	}
}
