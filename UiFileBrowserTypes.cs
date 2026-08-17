using System;

namespace SimpleUI
{
	/// <summary>Whether the browser confirms a file or a folder.</summary>
	public enum UiFileBrowserMode
	{
		/// <summary>Confirm a file; folders are only for navigation.</summary>
		OpenFile,
		/// <summary>Confirm a folder (the current directory, or a selected subdirectory).</summary>
		OpenFolder
	}

	/// <summary>Which details column the file list is sorted by.</summary>
	public enum UiFileBrowserSort
	{
		/// <summary>Case-insensitive name, folders first.</summary>
		Name,
		/// <summary>File size; folders sort as zero.</summary>
		Size,
		/// <summary>Extension / "Folder".</summary>
		Type,
		/// <summary>Last write time.</summary>
		Modified
	}

	/// <summary>One sidebar place (Home, a drive, a bookmark, or a caller-supplied shortcut).</summary>
	public sealed class UiFileBrowserPlace
	{
		/// <summary>Label shown in the Places list.</summary>
		public string Label { get; }

		/// <summary>Directory this place navigates to.</summary>
		public string Path { get; }

		/// <summary>Creates a place with a label and a directory path.</summary>
		public UiFileBrowserPlace(string label, string path)
		{
			if (string.IsNullOrEmpty(label))
			{
				throw new ArgumentException("Place label must be non-empty.", nameof(label));
			}
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentException("Place path must be non-empty.", nameof(path));
			}

			Label = label;
			Path = path;
		}
	}
}
