using System;
using System.Diagnostics;
using System.IO;

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
	}

	/// <summary>
	/// Open a documentation page with an external application.
	/// </summary>
	/// <param name="page">Type of page to open.</param>
	public static void OpenDocumentation(Page page = Page.TableOfContents)
	{
		var file = DocumentationFiles[(int)page];
		try
		{
			// By default use the file in the docs folder from the packaged build.
			var documentationFile = Path.Combine(new[] { AppContext.BaseDirectory, "docs", file });

			// Fallback for local builds.
			if (!File.Exists(documentationFile))
			{
				documentationFile = Path.Combine(new[] { AppContext.BaseDirectory, "..\\..\\..", "docs", file });
			}

			// Couldn't find the file.
			if (!File.Exists(documentationFile))
			{
				Fumen.Logger.Warn($"Failed to open documentation. Couldn't find {file}.");
				return;
			}

			Process.Start("explorer.exe", documentationFile);
		}
		catch (Exception e)
		{
			Fumen.Logger.Error($"Failed to open documentation. {e}");
		}
	}

	/// <summary>
	/// Open a link to the application GitHub page.
	/// </summary>
	public static void OpenGitHub()
	{
		try
		{
			Process.Start("explorer.exe", GitHubUrl);
		}
		catch (Exception e)
		{
			Fumen.Logger.Error($"Failed to open {GitHubUrl}. {e}");
		}
	}
}
