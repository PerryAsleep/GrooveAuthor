using StepManiaEditor;

namespace StepManiaEditorTests;

/// <summary>
/// Tests for the autosave preferences.
/// </summary>
[TestClass]
public class TestPreferencesAutosave
{
	private static Preferences RoundTrip(string json)
	{
		var fileName = Path.Combine(Path.GetTempPath(), $"grooveauthor-prefs-{Guid.NewGuid()}.json");
		try
		{
			File.WriteAllText(fileName, json);
			// Load runs PostLoad, which is where the interval is clamped.
			return Preferences.Load(null, fileName);
		}
		finally
		{
			File.Delete(fileName);
		}
	}

	/// <summary>
	/// A preferences file written before autosave existed has no autosave fields. Autosave must
	/// stay off and the interval must fall back to the documented default rather than to zero,
	/// which would otherwise save the chart on every single frame.
	/// </summary>
	[TestMethod]
	public void TestDefaultsWhenFieldsAreMissing()
	{
		var preferences = RoundTrip("{}");
		Assert.IsFalse(preferences.AutosaveEnabled, "Autosave should be opt-in.");
		Assert.AreEqual(120, preferences.AutosaveIntervalSeconds);
	}

	/// <summary>
	/// Autosave settings must survive a save/load cycle.
	/// </summary>
	[TestMethod]
	public void TestAutosaveFieldsRoundTrip()
	{
		var preferences = RoundTrip(
			"{\"AutosaveEnabled\": true, \"AutosaveIntervalSeconds\": 300}");
		Assert.IsTrue(preferences.AutosaveEnabled);
		Assert.AreEqual(300, preferences.AutosaveIntervalSeconds);
	}

	/// <summary>
	/// A hand-edited or corrupted preferences file must not be able to set an interval that
	/// would run a save every frame, or a negative one that never fires.
	/// </summary>
	[TestMethod]
	public void TestIntervalIsClampedToMinimum()
	{
		Assert.AreEqual(Preferences.MinAutosaveIntervalSeconds,
			RoundTrip("{\"AutosaveIntervalSeconds\": 0}").AutosaveIntervalSeconds);
		Assert.AreEqual(Preferences.MinAutosaveIntervalSeconds,
			RoundTrip("{\"AutosaveIntervalSeconds\": -600}").AutosaveIntervalSeconds);
		Assert.AreEqual(Preferences.MinAutosaveIntervalSeconds,
			RoundTrip("{\"AutosaveIntervalSeconds\": 1}").AutosaveIntervalSeconds);
	}

	/// <summary>
	/// A valid interval above the minimum must not be clamped.
	/// </summary>
	[TestMethod]
	public void TestValidIntervalIsNotClamped()
	{
		Assert.AreEqual(Preferences.MinAutosaveIntervalSeconds + 1,
			RoundTrip($"{{\"AutosaveIntervalSeconds\": {Preferences.MinAutosaveIntervalSeconds + 1}}}")
				.AutosaveIntervalSeconds);
	}
}
