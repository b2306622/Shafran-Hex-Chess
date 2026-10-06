using UnityEngine;

/// <summary>
/// Shared diagnostics for Shafran AI systems.
/// Kept quiet by default; tournament manager writes its own visible progress logs.
/// </summary>
public static class ShafranDebug
{
    public static bool Enabled = false;

    public static void Log(object message)
    {
        if (!Enabled) return;
        Debug.Log(message);
    }

    public static void Log(object message, Object context)
    {
        if (!Enabled) return;
        Debug.Log(message, context);
    }
}
