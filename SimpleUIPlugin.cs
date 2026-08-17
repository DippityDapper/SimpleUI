using BepInEx;
using BepInEx.Logging;

namespace SimpleUI
{
	/// <summary>Plugin entry point for the SimpleUI shared widget library.</summary>
	/// <remarks>
	/// A passive shared-library plugin, not a standalone feature -- same shape as LokrModAPI. Every
	/// type here is a plain static/factory API called synchronously by whichever plugin references
	/// it (LokrCharacterLab, to start); there's no Harmony patching and no runtime overlay of its own.
	/// </remarks>
	[BepInPlugin(Guid, Name, Version)]
	public class SimpleUIPlugin : BaseUnityPlugin
	{
		/// <summary>This plugin's BepInEx GUID.</summary>
		public const string Guid = "com.lokrmodding.simpleui";
		/// <summary>This plugin's display name.</summary>
		public const string Name = "LoKR Simple UI";
		/// <summary>This plugin's version string.</summary>
		public const string Version = "1.2.11";

		/// <summary>This plugin's shared BepInEx log source, set once in Awake().</summary>
		internal static ManualLogSource Log;

		/// <summary>Logs that the plugin loaded and caches the logger.</summary>
		private void Awake()
		{
			Log = base.Logger;
			Log.LogInfo(Name + " v" + Version + " loaded.");
		}
	}
}
