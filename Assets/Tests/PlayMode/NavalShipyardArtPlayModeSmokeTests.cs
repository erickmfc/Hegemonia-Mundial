#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class NavalShipyardArtPlayModeSmokeTests
{
    [UnityTest]
    public IEnumerator TexturedVisualRootSurvivesAPlayModeFrame()
    {
        const string prefabPath = "Assets/Prefabs/Estaleiro Marinho/Estaleiros navais.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab);
        Transform visual = prefab.transform.Find("MilitaryNavalShipyard");
        Assert.IsNotNull(visual);

        GameObject instance = Object.Instantiate(visual.gameObject);
        instance.transform.localScale = Vector3.one;
        yield return null;

        MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
        Assert.GreaterOrEqual(renderers.Length, 1700);
        var materialNames = new HashSet<string>();
        foreach (MeshRenderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.IsNotNull(material, "Material missing on " + renderer.name);
                Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
                Assert.IsNotNull(baseMap, "Albedo missing on " + material.name);
                materialNames.Add(material.name);
            }
        }
        foreach (string name in new[] { "MAT_Concrete_Naval", "MAT_Steel_Grey", "MAT_RoofMetal",
                 "MAT_SafetyYellow", "MAT_Asphalt", "MAT_Water", "MAT_Foliage_Olive" })
            Assert.IsTrue(materialNames.Contains(name), "Material not used: " + name);
        Assert.GreaterOrEqual(instance.GetComponentsInChildren<LODGroup>(true).Length, 5);

        Object.Destroy(instance);
        yield return null;
    }
}
#endif
