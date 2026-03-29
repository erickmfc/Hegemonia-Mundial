using System.Collections.Generic;
using UnityEngine;

public static class TagSafe
{
    public static bool Matches(Component component, string tagName)
    {
        if (component == null || string.IsNullOrEmpty(tagName))
        {
            return false;
        }

        try
        {
            return string.Equals(component.tag, tagName, System.StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool Matches(GameObject gameObject, string tagName)
    {
        if (gameObject == null || string.IsNullOrEmpty(tagName))
        {
            return false;
        }

        try
        {
            return string.Equals(gameObject.tag, tagName, System.StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool MatchesAny(Component component, IEnumerable<string> tags)
    {
        if (component == null || tags == null)
        {
            return false;
        }

        foreach (string tag in tags)
        {
            if (Matches(component, tag))
            {
                return true;
            }
        }

        return false;
    }
}
