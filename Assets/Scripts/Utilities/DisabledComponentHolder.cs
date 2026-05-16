using System.Collections.Generic;
using UnityEngine;

public class DisabledComponentHolder : MonoBehaviour
{
    public List<MonoBehaviour> disabled = new List<MonoBehaviour>();

    public void Restore()
    {
        foreach (var mb in disabled)
        {
            if (mb != null)
            {
                mb.enabled = true;
            }
        }
        disabled.Clear();
        Destroy(this);
    }
}

public static class ProjectSilentDebug
{
    public static void Log(object message)
    {
    }

    public static void Log(object message, Object context)
    {
    }

    public static void LogWarning(object message)
    {
    }

    public static void LogWarning(object message, Object context)
    {
    }

    public static void LogError(object message)
    {
    }

    public static void LogError(object message, Object context)
    {
    }
}
