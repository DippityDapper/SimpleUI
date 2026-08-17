using UnityEngine;

namespace SimpleUI
{
	/// <summary>One pickable row in a <see cref="UiCatalogue"/>: thumbnail, display name, and id.</summary>
	public sealed class UiCatalogueItem
	{
		/// <summary>Stable id used for selection and filter matching.</summary>
		public string Id { get; set; }

		/// <summary>Human-readable label shown above the id.</summary>
		public string Name { get; set; }

		/// <summary>Optional card thumbnail. Null draws a placeholder swatch.</summary>
		public Sprite Image { get; set; }
	}
}
