using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CampaignLoadTimingPlayModeTests
{
    private const string MenuScenePath = "Assets/Scenes/Menu cena.unity";
    private const string CampaignScenePath = "Assets/Scenes/GlobalMapRTS.unity";
    private string previousScenePath;
    private Type loadingServiceType;
    private Type loadingKindType;

    [UnitySetUp]
    public IEnumerator OpenMenuForCampaignLoad()
    {
        previousScenePath = SceneManager.GetActiveScene().path;
        if (!EditorApplication.isPlaying)
            yield return new EnterPlayMode();
        loadingServiceType = FindProjectType("LoadingScreenService");
        loadingKindType = FindProjectType("LoadingRequestKind");

        Scene current = SceneManager.GetActiveScene();
        if (!current.IsValid() || current.path != MenuScenePath)
        {
            EditorSceneManager.LoadSceneInPlayMode(
                MenuScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
        }

        float deadline = Time.realtimeSinceStartup + 90f;
        while (Time.realtimeSinceStartup < deadline)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == MenuScenePath
                && GetLoadingServiceInstance() != null)
                break;
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(MenuScenePath),
            "A fixture precisa iniciar pelo menu real antes de medir a campanha.");
        Assert.That(GetLoadingServiceInstance(), Is.Not.Null,
            "O menu precisa inicializar o LoadingScreenService real.");
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator RestorePreviousScene()
    {
        if (!EditorApplication.isPlaying || string.IsNullOrEmpty(previousScenePath)
            || SceneManager.GetActiveScene().path == previousScenePath)
            yield break;

        Scene previous = SceneManager.GetSceneByPath(previousScenePath);
        if (!previous.IsValid() || !previous.isLoaded)
        {
            EditorSceneManager.LoadSceneInPlayMode(
                previousScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
        }
        else
        {
            SceneManager.SetActiveScene(previous);
        }
        yield return null;
    }

    [UnityTest]
    [Timeout(450000)]
    public IEnumerator CampaignLoadingScreenReachesRealReadiness()
    {
        object service = GetLoadingServiceInstance();
        Assert.That(service, Is.Not.Null);
        FieldInfo waitingField = loadingServiceType.GetField(
            "aguardandoProntidaoInicial", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(waitingField, Is.Not.Null);

        Stopwatch timer = Stopwatch.StartNew();
        List<string> milestones = new List<string>();
        Application.LogCallback callback = (message, stackTrace, type) =>
        {
            if (message != null && message.IndexOf("[LoadingScreen][Tempo]", StringComparison.Ordinal) >= 0)
                milestones.Add("t=" + timer.Elapsed.TotalSeconds.ToString("F2") + "s " + message);
        };

        Application.logMessageReceived += callback;
        try
        {
            MethodInfo loadScene = loadingServiceType.GetMethod("CarregarCena", BindingFlags.Static | BindingFlags.Public);
            loadScene.Invoke(null, new[] { (object)"GlobalMapRTS", Enum.Parse(loadingKindType, "Campanha") });
            float deadline = Time.realtimeSinceStartup + 360f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Scene active = SceneManager.GetActiveScene();
                bool readyWait = !(bool)waitingField.GetValue(service);
                if (active.IsValid() && active.name == "GlobalMapRTS" && readyWait)
                    break;
                yield return null;
            }
        }
        finally
        {
            Application.logMessageReceived -= callback;
        }

        timer.Stop();
        foreach (string milestone in milestones)
            TestContext.WriteLine(milestone);
        TestContext.WriteLine("Campaign load total=" + timer.Elapsed.TotalSeconds.ToString("F2") + "s");

        Scene result = SceneManager.GetActiveScene();
        Assert.That(result.IsValid() && result.name == "GlobalMapRTS", Is.True,
            "O fluxo real deve terminar com GlobalMapRTS ativa. " + string.Join(" | ", milestones));
        Assert.That((bool)waitingField.GetValue(service), Is.False,
            "A tela não deve declarar prontidão antes da superfície, navegação e bootstrap. "
            + string.Join(" | ", milestones));
        Assert.That(milestones.Exists(m => m.Contains("etapa=DadosDaCenaProntos")), Is.True,
            "A medição deve registrar a preparação assíncrona da cena.");
        Assert.That(milestones.Exists(m => m.Contains("etapa=SceneLoadedCallback")), Is.True,
            "A medição deve registrar a ativação efetiva da cena.");
        Assert.That(milestones.Exists(m => m.Contains("etapa=SuperficieInicialPronta")), Is.True,
            "A medição deve registrar a prontidão real antes de esconder a tela.");
        Assert.That(timer.Elapsed.TotalSeconds, Is.LessThan(360d));
    }

    private object GetLoadingServiceInstance()
    {
        if (loadingServiceType == null)
            return null;
        PropertyInfo instance = loadingServiceType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return instance != null ? instance.GetValue(null) : null;
    }

    private static Type FindProjectType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null)
                return type;
        }
        return null;
    }
}
