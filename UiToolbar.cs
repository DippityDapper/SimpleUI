using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>A horizontal icon-button strip with grouping separators and an optional active-tool highlight.</summary>
	/// <remarks>Generalizes ToolbarPanel's hand-built tool-mode button row so other workspaces can build a toolbar without re-deriving it.</remarks>
	public sealed class UiToolbar : UiElement<UiToolbar>
	{
		private readonly UiStack row;
		private readonly Dictionary<string, UiButton> buttons = new Dictionary<string, UiButton>();
		private string activeId;

		/// <summary>Id of the currently highlighted tool button, or null if none is active.</summary>
		public string ActiveId => activeId;

		/// <summary>Wraps an already-built horizontal stack.</summary>
		private UiToolbar(GameObject gameObject, UiTheme theme, UiStack row) : base(gameObject, theme)
		{
			this.row = row;
		}

		/// <summary>Creates a toolbar filling its parent as a horizontal strip.</summary>
		public static UiToolbar Create(Transform parent, UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject root = new GameObject("UiToolbar", typeof(Image));
			root.transform.SetParent(parent, false);
			UiLayoutUtil.Stretch(root.GetComponent<RectTransform>());
			root.GetComponent<Image>().color = theme.PanelBackground;
			LayoutElement rootLayout = root.AddComponent<LayoutElement>();
			rootLayout.minHeight = theme.ToolbarHeight;
			rootLayout.preferredHeight = theme.ToolbarHeight;

			UiStack row = UiStack.Horizontal(root.transform, theme, spacing: 6f, padding: 6f);
			return new UiToolbar(root, theme, row);
		}

		/// <summary>Adds a labeled button. Returns the button for further chaining (FixedWidth, etc.).</summary>
		public UiButton AddButton(string id, string label, Action onClick)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("Toolbar button id must be non-empty.", nameof(id));
			}
			if (buttons.ContainsKey(id))
			{
				throw new ArgumentException("A toolbar button with id '" + id + "' already exists.", nameof(id));
			}

			UiButton button = UiButton.Create(row.ContentTransform, label ?? string.Empty, () =>
			{
				onClick?.Invoke();
			}, Theme, primary: false).FixedHeight(Theme.ControlHeight);
			row.Add(button);
			buttons[id] = button;
			return button;
		}

		/// <summary>Adds a labeled toggle.</summary>
		public UiToggle AddToggle(string id, string label, bool initial, Action<bool> onChange)
		{
			UiToggle toggle = UiToggle.Create(row.ContentTransform, label ?? string.Empty, initial, Theme)
				.Name("Toggle_" + id);
			if (onChange != null)
			{
				toggle.OnValueChanged(value => onChange(value));
			}
			row.Add(toggle.FixedWidth(120f).FixedHeight(Theme.ControlHeight));
			return toggle;
		}

		/// <summary>Adds a thin vertical separator between button groups.</summary>
		public UiToolbar AddSeparator()
		{
			GameObject line = new GameObject("Separator", typeof(Image));
			line.transform.SetParent(row.ContentTransform, false);
			line.GetComponent<Image>().color = Theme.SeparatorColor;
			LayoutElement layout = line.AddComponent<LayoutElement>();
			layout.minWidth = 2f;
			layout.preferredWidth = 2f;
			layout.flexibleHeight = 1f;
			layout.minHeight = 16f;
			return this;
		}

		/// <summary>Adds a growing spacer so subsequent controls sit on the right side of the strip.</summary>
		public UiToolbar AddSpacer()
		{
			UiLabel spacer = UiLabel.Create(row.ContentTransform, string.Empty, Theme);
			spacer.Grow();
			row.Add(spacer);
			return this;
		}

		/// <summary>Adds a text label to the strip. Use after <see cref="AddSpacer"/> to right-align it.</summary>
		public UiLabel AddLabel(string text, TextAnchor alignment = TextAnchor.MiddleRight)
		{
			UiLabel label = UiLabel.Create(row.ContentTransform, text ?? string.Empty, Theme, Theme.BodyFontSize, alignment);
			row.Add(label.FixedHeight(Theme.ControlHeight));
			return label;
		}

		/// <summary>Highlights the named button as the active tool (others return to the row-button color).</summary>
		public UiToolbar SetActive(string id)
		{
			activeId = id;
			foreach (KeyValuePair<string, UiButton> pair in buttons)
			{
				pair.Value.SetColor(pair.Key == id ? Theme.AccentColor : Theme.RowButtonColor);
			}
			return this;
		}

		/// <summary>Removes every button, toggle, and spacer so the strip can be rebuilt for a different project type.</summary>
		public UiToolbar Clear()
		{
			row.Clear();
			buttons.Clear();
			activeId = null;
			return this;
		}

		/// <summary>Clears the active-tool highlight.</summary>
		public UiToolbar ClearActive()
		{
			activeId = null;
			foreach (KeyValuePair<string, UiButton> pair in buttons)
			{
				pair.Value.SetColor(Theme.RowButtonColor);
			}
			return this;
		}

		/// <summary>Looks up a previously added button by id.</summary>
		public bool TryGetButton(string id, out UiButton button)
		{
			return buttons.TryGetValue(id, out button);
		}
	}
}
