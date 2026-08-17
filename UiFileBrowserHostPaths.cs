using System;
using System.IO;

namespace SimpleUI
{
	/// <summary>Finds the host Linux filesystem when the game is running under Steam Proton / Wine.</summary>
	/// <remarks>
	/// Unity sees the Wine prefix: Home is C:\users\steamuser. Proton maps the real machine to
	/// Z:\ (Linux /). HOME / USER are often still the host values, so we can build Z:\home\&lt;user&gt;.
	/// Native Windows leaves these null.
	/// </remarks>
	internal static class UiFileBrowserHostPaths
	{
		internal static readonly string Root;
		internal static readonly string Home;
		internal static readonly string Pictures;
		internal static readonly bool IsProton;

		static UiFileBrowserHostPaths()
		{
			string wineHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
			string hostHomeEnv = Environment.GetEnvironmentVariable("HOME");
			string hostUser = Environment.GetEnvironmentVariable("USER");

			IsProton = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINEPREFIX"))
				|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH"))
				|| wineHome.IndexOf("steamuser", StringComparison.OrdinalIgnoreCase) >= 0
				|| LooksLikeUnixHome(hostHomeEnv);

			if (!IsProton)
			{
				return;
			}

			Root = FirstExisting("/", @"Z:\");
			Home = FirstExisting(
				hostHomeEnv,
				ToWineZ(hostHomeEnv),
				UnixHome(hostUser),
				ToWineZ(UnixHome(hostUser)));

			if (string.Equals(Normalize(Home), Normalize(wineHome), StringComparison.OrdinalIgnoreCase))
			{
				Home = null;
			}

			if (!string.IsNullOrEmpty(Home))
			{
				Pictures = FirstExisting(
					Path.Combine(Home, "Pictures"),
					Path.Combine(Home, "pictures"));
			}
		}

		internal static string PreferredStartDirectory()
		{
			return FirstExisting(Pictures, Home);
		}

		private static bool LooksLikeUnixHome(string path)
		{
			return !string.IsNullOrEmpty(path)
				&& (path.StartsWith("/home/", StringComparison.Ordinal) || path.StartsWith("/Users/", StringComparison.Ordinal));
		}

		private static string UnixHome(string user)
		{
			if (string.IsNullOrEmpty(user) || string.Equals(user, "steamuser", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			return "/home/" + user;
		}

		private static string ToWineZ(string unixPath)
		{
			if (string.IsNullOrEmpty(unixPath) || unixPath[0] != '/')
			{
				return null;
			}

			return @"Z:\" + unixPath.TrimStart('/').Replace('/', '\\');
		}

		private static string Normalize(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return string.Empty;
			}

			try
			{
				return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			}
			catch (Exception)
			{
				return path;
			}
		}

		private static string FirstExisting(params string[] paths)
		{
			for (int i = 0; i < paths.Length; i++)
			{
				string path = paths[i];
				if (string.IsNullOrEmpty(path))
				{
					continue;
				}

				try
				{
					if (Directory.Exists(path))
					{
						return path;
					}
				}
				catch (Exception)
				{
				}
			}

			return null;
		}
	}
}
