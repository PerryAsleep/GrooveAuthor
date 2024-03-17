using System;
using System.Diagnostics;

namespace StepManiaEditor;

/// <summary>
/// Class for managing opening external documentation.
/// </summary>
internal sealed class Documentation
{
	public const string GitHubUrl = "https://github.com/PerryAsleep/GrooveAuthor";

	public enum Page
	{
		TableOfContents,
		PatternGeneration,
		PatternConfigs,
		PerformedChartConfigs,
		ExpressedChartConfigs,
		SongSync,
	}

	private static readonly string[] DocumentationFiles;

	static Documentation()
	{
		var count = Enum.GetNames(typeof(Page)).Length;
		DocumentationFiles = new string[count];
		DocumentationFiles[(int)Page.TableOfContents] = "TableOfContents.md";
		DocumentationFiles[(int)Page.PatternConfigs] = "PatternConfigs.md";
		DocumentationFiles[(int)Page.PatternGeneration] = "PatternGeneration.md";
		DocumentationFiles[(int)Page.PerformedChartConfigs] = "PerformedChartConfigs.md";
		DocumentationFiles[(int)Page.ExpressedChartConfigs] = "ExpressedChartConfigs.md";
		DocumentationFiles[(int)Page.SongSync] = "SongSync.md";
	}

	private static string GetDocumentationBaseUrl()
	{
#if RELEASE
		var version = Utils.GetAppVersion();
		var tree = $"v{version.Major}.{version.Minor}.{version.Build}";
#else
		const string tree = "main";
#endif
		return $"{GitHubUrl}/blob/{tree}/StepManiaEditor/docs/";
	}

	/// <summary>
	/// Open a documentation page with an external application.
	/// </summary>
	/// <param name="page">Type of page to open.</param>
	public static void OpenDocumentation(Page page = Page.TableOfContents)
	{
		OpenUrl($"{GetDocumentationBaseUrl()}{DocumentationFiles[(int)page]}");
	}

	/// <summary>
	/// Open a link to the application GitHub page.
	/// </summary>
	public static void OpenGitHub()
	{
		OpenUrl(GitHubUrl);
	}

	private static void OpenUrl(string url)
	{
		try
		{
			Process.Start("explorer.exe", url);
		}
		catch (Exception e)
		{
			Fumen.Logger.Error($"Failed to open {url}. {e}");
		}
	}
}
