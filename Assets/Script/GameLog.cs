using System.Diagnostics;

/// <summary>
/// Drop-in replacement for UnityEngine.Debug.Log that compiles out of
/// non-editor builds entirely, so routine state-tracing logs don't ship
/// in the final game. Use UnityEngine.Debug.LogWarning/LogError directly
/// for anything that should still surface in production.
/// </summary>
public static class GameLog
{
    [Conditional("UNITY_EDITOR")]
    public static void Log(object message)
    {
        UnityEngine.Debug.Log(message);
    }
}
