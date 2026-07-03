/// <summary>
/// Canonical scene names, matching the .unity files in Assets/Scenes exactly.
/// Use these instead of typing scene name string literals to avoid typo-driven
/// LoadLevel/LoadScene failures (e.g. "MainMenu" vs "Main Menu").
/// </summary>
public static class SceneNames
{
    public const string MainMenu = "Main Menu";
    public const string IntroCutscene = "IntroCutscene";
    public const string Level1 = "Level 1";
    public const string Level2 = "Level 2";
    public const string Level3 = "Level 3";
    public const string Level1VictoryCutscene = "Level1VictoryCutscene";
    public const string Level2VictoryCutscene = "Level2VictoryCutscene";
}
