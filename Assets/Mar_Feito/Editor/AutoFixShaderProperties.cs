using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AutoFixShaderProperties
{
    static void RunFix()
    {
        string[] matGuids = AssetDatabase.FindAssets("t:Material");
        List<string> fixedMats = new List<string>();

        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Only process materials using our shaders, or ANY material that has the problematic properties
            // But checking all materials is safer.
            // However, modifying non-problematic materials is risky if they coincidentally use these names (highly unlikely).
            if (mat.shader.name != "Nature/Ocean" && mat.shader.name != "Nature/OceanAdvanced") continue;

            SerializedObject so = new SerializedObject(mat);
            SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            
            bool changed = false;
            
            if (RemoveTextureProperty(texEnvs, "_OceanRefraction")) changed = true;
            if (RemoveTextureProperty(texEnvs, "_OceanAdvRefraction")) changed = true;
            if (RemoveTextureProperty(texEnvs, "_RefractionTex")) changed = true; // Also remove old name if present
            
            // Also clean up the new names if they accidentally get serialized (they should be global only)
            if (RemoveTextureProperty(texEnvs, "_GrabRefraction")) changed = true;
            if (RemoveTextureProperty(texEnvs, "_GrabAdvRefraction")) changed = true;
            
            if (changed)
            {
                so.ApplyModifiedProperties();
                fixedMats.Add(path);
                EditorUtility.SetDirty(mat);
            }
        }
        
        if (fixedMats.Count > 0)
        {
            Debug.Log($"[AutoFixShaderProperties] Fixed {fixedMats.Count} materials: \n" + string.Join("\n", fixedMats));
            AssetDatabase.SaveAssets();
        }
    }

    [MenuItem("Tools/Fix Shader Properties")]
    public static void ManualFix()
    {
        RunFix();
    }

    static bool RemoveTextureProperty(SerializedProperty texEnvs, string propName)
    {
        if (texEnvs == null || !texEnvs.isArray) return false;
        
        bool found = false;
        for (int i = texEnvs.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
            SerializedProperty keyProp = entry.FindPropertyRelative("first");
            
            if (keyProp != null && keyProp.stringValue == propName)
            {
                texEnvs.DeleteArrayElementAtIndex(i);
                found = true;
            }
        }
        return found;
    }
}
