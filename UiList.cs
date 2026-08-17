using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleUI
{
	/// <summary>A data-bound row list, built on UiStack, that diffs by key across refreshes instead of destroying and rebuilding every row.</summary>
	/// <remarks>
	/// The shared building block that replaces the ~6 duplicated "destroy every child, rebuild from
	/// scratch" refresh loops found across CharacterListPanel/ReadinessChecklistPanel/
	/// AnimationTimelinePanel/AnimationsPanel/FileBrowserPanel/the picker panels. SetItems diffs by
	/// key: rows for keys no longer present are destroyed, rows for brand-new keys are created via
	/// `build`, and rows for keys that persist are left completely untouched -- a row's GameObject
	/// identity survives across refreshes as long as its key does, which is what actually matters
	/// for not breaking a click event mid-flight (the bug SceneTreePanel had to special-case itself
	/// against).
	///
	/// `build` returns whatever single UiElement it constructs as the row's root (a UiButton, a
	/// UiStack of several widgets, anything) -- that element's own GameObject IS the row, no extra
	/// wrapper needed, and its own LayoutElement/ContentSizeFitter (if any) is what the surrounding
	/// list's LayoutGroup reads to size it.
	///
	/// If a row's on-screen content needs to change on a later refresh without its key changing
	/// (SceneTreePanel's own text/color/sibling-index updates on rename), use TryGetRow to reach
	/// that row's root GameObject directly and mutate it yourself -- SetItems only decides
	/// creation/destruction, not in-place mutation, since only the caller knows what "unchanged"
	/// means for T.
	/// </remarks>
	public sealed class UiList<T> : UiElement<UiList<T>>
	{
		private readonly UiStack stack;
		private readonly Dictionary<string, GameObject> rowsByKey = new Dictionary<string, GameObject>();

		/// <summary>Wraps an already-built backing UiStack.</summary>
		private UiList(UiStack stack, UiTheme theme) : base(stack.GameObject, theme)
		{
			this.stack = stack;
		}

		/// <summary>Creates an empty list backed by a vertical or horizontal UiStack, scrollable by default.</summary>
		/// <remarks>
		/// Default scrollable:true means no self-sizing — the list fills whatever height its parent
		/// assigns. Inside a ContentSizeFitter section (a Properties category, a non-scrollable
		/// inspector form) pass scrollable:false, or Add() the list with FixedHeight/Grow(). Nesting
		/// a default-scrollable list inside another ScrollRect is the same zero-height collapse as
		/// a nested UiStack.
		/// </remarks>
		public static UiList<T> Create(Transform parent, UiOrientation orientation = UiOrientation.Vertical,
			UiTheme theme = null, float spacing = -1f, float padding = -1f, bool scrollable = true)
		{
			UiStack stack = orientation == UiOrientation.Vertical
				? UiStack.Vertical(parent, theme, spacing, padding, scrollable)
				: UiStack.Horizontal(parent, theme, spacing, padding, scrollable);
			return new UiList<T>(stack, theme ?? UiTheme.Default);
		}

		/// <summary>Diffs the list against its current rows by key: destroys stale rows, creates new ones via `build`, and reorders all of them to match `items`.</summary>
		public void SetItems(IEnumerable<T> items, Func<T, string> keyFn, Func<Transform, T, UiElement> build)
		{
			List<T> itemList = items is List<T> list ? list : new List<T>(items);

			HashSet<string> newKeys = new HashSet<string>();
			foreach (T item in itemList)
			{
				newKeys.Add(keyFn(item));
			}

			List<string> staleKeys = null;
			foreach (string existingKey in rowsByKey.Keys)
			{
				if (!newKeys.Contains(existingKey))
				{
					(staleKeys ?? (staleKeys = new List<string>())).Add(existingKey);
				}
			}
			if (staleKeys != null)
			{
				foreach (string staleKey in staleKeys)
				{
					UnityEngine.Object.Destroy(rowsByKey[staleKey]);
					rowsByKey.Remove(staleKey);
				}
			}

			int siblingIndex = 0;
			foreach (T item in itemList)
			{
				string key = keyFn(item);
				if (!rowsByKey.TryGetValue(key, out GameObject rowObject))
				{
					UiElement created = build(stack.ContentTransform, item);
					rowObject = created.GameObject;
					rowsByKey[key] = rowObject;
				}
				rowObject.transform.SetSiblingIndex(siblingIndex);
				siblingIndex++;
			}
		}

		/// <summary>Looks up a row's root GameObject by key, for in-place mutation without a full SetItems refresh.</summary>
		public bool TryGetRow(string key, out GameObject rowObject)
		{
			return rowsByKey.TryGetValue(key, out rowObject);
		}

		/// <summary>Destroys every row.</summary>
		public void Clear()
		{
			foreach (GameObject rowObject in rowsByKey.Values)
			{
				UnityEngine.Object.Destroy(rowObject);
			}
			rowsByKey.Clear();
		}
	}
}
