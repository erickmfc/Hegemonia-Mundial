using UnityEngine;
using UnityEditor;

public class AutoFixWaterShader : EditorWindow
{
    [MenuItem("Tools/Fix Water Shader (Agua to Lit)")]
    public static void FixWaterShader()
    {
        GameObject agua = GameObject.Find("Agua");
        if (agua == null)
        {
            // Try finding by tag or loosely
            Debug.LogWarning("GameObject 'Agua' not found in the active scene. Searching for any object with Ocean material...");
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r.sharedMaterial != null && (r.sharedMaterial.shader.name.Contains("Ocean") || r.sharedMaterial.name.Contains("Sea")))
                {
                    agua = r.gameObject;
                    break;
                }
            }
        }

        if (agua != null)
        {
            Renderer r = agua.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit != null)
                {
                    Undo.RecordObject(r.sharedMaterial, "Change Water Shader");
                    r.sharedMaterial.shader = lit;
                    Debug.Log($"[AutoFix] Changed shader of '{agua.name}' to Universal Render Pipeline/Lit.");
                }
                else
                {
                    Debug.LogError("[AutoFix] Could not find shader 'Universal Render Pipeline/Lit'. Is URP installed?");
                }
            }
        }
        else
        {
            Debug.LogError("[AutoFix] Could not find object 'Agua' or any object with Ocean material.");
        }
    }
}
