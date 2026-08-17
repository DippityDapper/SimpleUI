using UnityEngine;

namespace SimpleUI
{
	/// <summary>Color, font, and spacing constants shared by every widget.</summary>
	/// <remarks>
	/// One place holding every default color/font/spacing value every widget factory falls back to
	/// when the caller doesn't override it. Values here are copied verbatim from LokrCharacterLab's
	/// existing hand-built UI (EditorUiHelpers.cs/CharacterLabScene.cs) so migrating a screen onto
	/// SimpleUI is structural only -- nothing should look different on screen. Visual restyling is
	/// a deliberately separate, later conversation; change these defaults there, not while
	/// migrating a screen's construction code.
	/// </remarks>
	public sealed class UiTheme
	{
		/// <summary>The shared default theme instance every widget factory falls back to.</summary>
		public static readonly UiTheme Default = new UiTheme();

		/// <summary>Default font for labels, buttons, and text fields.</summary>
		public Font Font = Resources.GetBuiltinResource<Font>("Arial.ttf");

		/// <summary>Background color for UiPanel.</summary>
		public Color PanelBackground = new Color(0.07f, 0.08f, 0.11f, 0.94f);
		/// <summary>Backdrop color behind a modal dialog.</summary>
		public Color ModalBackdrop = new Color(0f, 0f, 0f, 0.6f);
		/// <summary>Background color for a modal dialog's own panel.</summary>
		public Color ModalPanelBackground = new Color(0.05f, 0.05f, 0.08f, 0.98f);

		/// <summary>Primary/prominent button color.</summary>
		/// <remarks>Used for CharacterLabScene's own canvas-level buttons.</remarks>
		public Color ButtonColor = new Color(0.2f, 0.4f, 0.75f);
		/// <summary>Quieter button color for buttons packed inside a panel's own rows.</summary>
		/// <remarks>Used for row/chip buttons across the Animator panels (EditHistoryPanel, AnimationsPanel, AnimationTimelinePanel) and non-primary UiButton/UiDropdown/UiComboBox/UiToggle chrome.</remarks>
		public Color RowButtonColor = new Color(0.25f, 0.3f, 0.4f);
		/// <summary>Highlight color for an active/selected state.</summary>
		/// <remarks>Used for ToolbarPanel's active-tool highlight, and UiToggle's own "on" fill.</remarks>
		public Color AccentColor = new Color(0.85f, 0.55f, 0.15f);

		/// <summary>Background color for UiTextField.</summary>
		public Color FieldBackground = new Color(0.95f, 0.95f, 0.95f);
		/// <summary>Text color inside a UiTextField.</summary>
		public Color FieldTextColor = Color.black;

		/// <summary>Default text color for labels and buttons.</summary>
		public Color LabelColor = Color.white;
		/// <summary>Font size for panel/dialog titles.</summary>
		public int TitleFontSize = 15;
		/// <summary>Font size for ordinary body text.</summary>
		public int BodyFontSize = 12;

		/// <summary>Color for error status rows/messages.</summary>
		/// <remarks>Used for ReadinessChecklistPanel's own Error rows.</remarks>
		public Color ErrorColor = new Color(1f, 0.4f, 0.4f);
		/// <summary>Color for warning status rows/messages.</summary>
		/// <remarks>Used for ReadinessChecklistPanel's own Warning rows.</remarks>
		public Color WarningColor = new Color(1f, 0.85f, 0.3f);

		/// <summary>Default UiStack padding, in pixels.</summary>
		public float Padding = 12f;
		/// <summary>Default UiStack inter-child spacing, in pixels.</summary>
		public float Spacing = 6f;
		/// <summary>Default height for button/field-sized controls, in pixels.</summary>
		public float ControlHeight = 32f;
		/// <summary>Default scroll wheel sensitivity for a scrollable UiStack.</summary>
		public float ScrollSensitivity = 26f;

		/// <summary>Idle color for a UiSplitter handle.</summary>
		public Color SplitterColor = new Color(0.15f, 0.18f, 0.25f);
		/// <summary>Hover/active color for a UiSplitter handle.</summary>
		public Color SplitterHoverColor = new Color(0.85f, 0.55f, 0.15f);
		/// <summary>Pixel thickness of a UiSplitter handle.</summary>
		public float SplitterThickness = 6f;

		/// <summary>Background color for an unselected tab in UiTabGroup.</summary>
		public Color TabBackground = new Color(0.12f, 0.14f, 0.18f);
		/// <summary>Background color for the selected tab in UiTabGroup.</summary>
		public Color TabActiveBackground = new Color(0.22f, 0.26f, 0.34f);
		/// <summary>Pixel height of a UiTabGroup tab strip.</summary>
		public float TabHeight = 28f;

		/// <summary>Highlight overlay color shown over a dock zone while a panel is being dragged onto it.</summary>
		public Color DockDropHighlight = new Color(0.85f, 0.55f, 0.15f, 0.35f);

		/// <summary>Background color for a selected UiTree row.</summary>
		public Color TreeSelection = new Color(0.9f, 0.75f, 0.2f, 0.9f);
		/// <summary>Idle background color for a UiTree row.</summary>
		public Color TreeRowBackground = new Color(1f, 1f, 1f, 0.08f);
		/// <summary>Pixel height of one UiTree row.</summary>
		public float TreeRowHeight = 24f;
		/// <summary>Pixel indent per UiTree depth level.</summary>
		public float TreeIndent = 16f;

		/// <summary>Background color for UiContextMenu.</summary>
		public Color ContextMenuBackground = new Color(0.05f, 0.05f, 0.08f, 0.98f);
		/// <summary>Color for toolbar/menu separators.</summary>
		public Color SeparatorColor = new Color(0.3f, 0.32f, 0.38f);

		/// <summary>Default pixel height of UiStatusBar.</summary>
		public float StatusBarHeight = 24f;
		/// <summary>Default pixel height of UiToolbar.</summary>
		public float ToolbarHeight = 36f;
	}
}
