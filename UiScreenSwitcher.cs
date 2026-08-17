using System.Collections.Generic;
using UnityEngine;

namespace SimpleUI
{
	/// <summary>Switches between named, full-stretch sibling screens by toggling GameObject.SetActive.</summary>
	/// <remarks>
	/// Formalizes the "one full-stretch sibling RectTransform per screen, switching is a SetActive
	/// call" pattern CharacterLabScene already hand-rolls (CreateFullStretchScreenRoot + its own
	/// LabScreen enum/currentScreen field). Register each named screen once, then Show(name) hides
	/// every other registered screen.
	/// </remarks>
	public sealed class UiScreenSwitcher
	{
		private readonly Dictionary<string, GameObject> screens = new Dictionary<string, GameObject>();

		/// <summary>The name most recently passed to Show, or null if Show hasn't been called yet.</summary>
		public string CurrentScreen { get; private set; }

		/// <summary>Registers a new full-stretch screen under canvas, initially hidden, and returns its root Transform to build content under.</summary>
		public Transform Register(string name, Transform canvas)
		{
			GameObject screenObject = new GameObject(name, typeof(RectTransform));
			screenObject.transform.SetParent(canvas, false);
			RectTransform rect = screenObject.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			screenObject.SetActive(false);

			screens[name] = screenObject;
			return screenObject.transform;
		}

		/// <summary>Gets a registered screen's root Transform by name.</summary>
		public Transform GetRoot(string name)
		{
			return screens[name].transform;
		}

		/// <summary>Looks up a registered screen root without throwing.</summary>
		public bool TryGetRoot(string name, out Transform root)
		{
			if (!string.IsNullOrEmpty(name) && screens.TryGetValue(name, out GameObject screenObject))
			{
				root = screenObject.transform;
				return true;
			}

			root = null;
			return false;
		}

		/// <summary>Shows the named screen and hides every other registered screen.</summary>
		public void Show(string name)
		{
			foreach (KeyValuePair<string, GameObject> entry in screens)
			{
				entry.Value.SetActive(entry.Key == name);
			}
			CurrentScreen = name;
		}

		/// <summary>Clears registered screens after the lab scene they lived in was unloaded.</summary>
		public void Clear()
		{
			screens.Clear();
			CurrentScreen = null;
		}
	}
}
