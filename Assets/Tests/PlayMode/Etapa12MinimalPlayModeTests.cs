using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[PrebuildSetup(typeof(Etapa12MinimalPlayModeSetup))]
public sealed class Etapa12MinimalPlayModeTests
{
    [UnityTest]
    public IEnumerator UnityTestStartsAndYieldsOneFrame()
    {
        Debug.Log("[Etapa12MinimalPlayModeTests] UnityTest iniciou.");
        yield return null;

        Assert.That(Application.isPlaying, Is.True);
        Assert.That(SceneManager.GetActiveScene().IsValid(), Is.True);
    }

    [Test]
    public void NUnitTestRunsInPlayModeAssembly()
    {
        Assert.That(Application.isPlaying, Is.True);
    }

    [UnityTest]
    public IEnumerator UnityTestCreatesAndDestroysSimpleGameObject()
    {
        GameObject probe = new GameObject("Etapa12_PlayModeProbe");
        Assert.That(probe, Is.Not.Null);

        UnityEngine.Object.Destroy(probe);
        yield return null;

        Assert.That(probe == null, Is.True);
    }
}

public sealed class Etapa12MinimalPlayModeSetup : IPrebuildSetup
{
    private const string IsolatedScenePath = "Assets/Tests/PlayMode/Etapa12MinimalPlayModeScene.unity";

    public void Setup()
    {
#if UNITY_EDITOR
        if (!File.Exists(IsolatedScenePath))
        {
            throw new FileNotFoundException("Cena isolada de PlayMode nao encontrada.", IsolatedScenePath);
        }

        Scene bootstrapScene = SceneManager.GetActiveScene();
        GameObject testController = null;
        if (bootstrapScene.IsValid())
        {
            GameObject[] roots = bootstrapScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].GetComponent("PlaymodeTestsController") != null)
                {
                    testController = roots[i];
                    break;
                }
            }
        }

        Scene isolatedScene = EditorSceneManager.OpenScene(IsolatedScenePath, OpenSceneMode.Additive);
        if (!isolatedScene.IsValid())
        {
            throw new InvalidOperationException("Nao foi possivel abrir a cena isolada de PlayMode.");
        }

        if (testController == null)
        {
            throw new InvalidOperationException("PlaymodeTestsController nao encontrado na cena bootstrap.");
        }

        SceneManager.MoveGameObjectToScene(testController, isolatedScene);
        SceneManager.SetActiveScene(isolatedScene);
        if (bootstrapScene.IsValid() && bootstrapScene != isolatedScene)
        {
            EditorSceneManager.CloseScene(bootstrapScene, true);
        }

        Debug.Log("[Etapa12MinimalPlayModeSetup] Cena isolada aberta: " + IsolatedScenePath);
#endif
    }
}
