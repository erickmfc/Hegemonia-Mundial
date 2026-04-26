using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AuditoriaTrilhaControleUnidades
{
    private const string MarcadorFallbackTransicional = "CONTROL_PATH_TRANSITIONAL_FALLBACK";

    private static readonly HashSet<string> ArquivosAutorizadosSetDestination = new HashSet<string>
    {
        "Assets/scripts/ControleUnidade.cs",
        "Assets/scripts/ControleNavioRealista.cs",
        "Assets/scripts/ControleSubmarino.cs",
        "Assets/scripts/C700TransporteAereo.cs",
        "Assets/scripts/Helicoptero.cs",
        "Assets/scripts/MovimentoFallbackTransicional.cs",
        "Assets/scripts/NavegacaoInteligenteNaval.cs"
    };

    [MenuItem("Tools/Diagnostics/Control Path/Scan All")]
    private static void ScanAll()
    {
        int problemas = ScanAllPrefabsInternal();
        problemas += ScanAllScriptsInternal();

        if (problemas > 0)
        {
            Debug.LogWarning("[AuditoriaTrilhaControleUnidades] Problemas encontrados na trilha de controle: " + problemas);
        }
        else
        {
            Debug.Log("[AuditoriaTrilhaControleUnidades] Nenhum problema encontrado na auditoria completa.");
        }
    }

    // Entry-point para rodar via linha de comando:
    // Unity.exe -batchmode -quit -projectPath <path> -executeMethod AuditoriaTrilhaControleUnidades.RunScanAllCli -logFile <file>
    public static void RunScanAllCli()
    {
        int problemas = ScanAllPrefabsInternal();
        problemas += ScanAllScriptsInternal();

        if (problemas > 0)
        {
            Debug.LogWarning("[AuditoriaTrilhaControleUnidades] Problemas encontrados na trilha de controle: " + problemas);
        }
        else
        {
            Debug.Log("[AuditoriaTrilhaControleUnidades] Nenhum problema encontrado na auditoria completa.");
        }

        EditorApplication.Exit(problemas > 0 ? 1 : 0);
    }

    [MenuItem("Tools/Diagnostics/Control Path/Scan Prefabs")]
    private static void ScanPrefabs()
    {
        int problemas = ScanAllPrefabsInternal();
        if (problemas > 0)
        {
            Debug.LogWarning("[AuditoriaTrilhaControleUnidades] Problemas encontrados em prefabs: " + problemas);
        }
        else
        {
            Debug.Log("[AuditoriaTrilhaControleUnidades] Nenhum problema encontrado nos prefabs.");
        }
    }

    [MenuItem("Tools/Diagnostics/Control Path/Scan Direct SetDestination Callers")]
    private static void ScanScripts()
    {
        int problemas = ScanAllScriptsInternal();
        if (problemas > 0)
        {
            Debug.LogWarning("[AuditoriaTrilhaControleUnidades] Chamadas diretas proibidas de SetDestination encontradas: " + problemas);
        }
        else
        {
            Debug.Log("[AuditoriaTrilhaControleUnidades] Nenhuma chamada direta proibida de SetDestination encontrada.");
        }
    }

    internal static int ScanImportedAssets(string[] importedAssets)
    {
        int problemas = 0;
        if (importedAssets == null)
        {
            return 0;
        }

        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i];
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
            {
                continue;
            }

            if (path.EndsWith(".prefab"))
            {
                problemas += ScanPrefabAtPath(path);
            }
            else if (path.EndsWith(".cs"))
            {
                problemas += ScanScriptAtPath(path);
            }
        }

        return problemas;
    }

    private static int ScanAllPrefabsInternal()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int problemas = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            problemas += ScanPrefabAtPath(path);
        }

        return problemas;
    }

    private static int ScanAllScriptsInternal()
    {
        string[] scriptGuids = AssetDatabase.FindAssets("t:Script");
        int problemas = 0;

        for (int i = 0; i < scriptGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs"))
            {
                continue;
            }

            problemas += ScanScriptAtPath(path);
        }

        return problemas;
    }

    private static int ScanPrefabAtPath(string assetPath)
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefabRoot == null)
        {
            return 0;
        }

        int problemas = ScanSerializedLegacyComponents(assetPath);
        Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            problemas += ScanGameObject(assetPath, transforms[i].gameObject);
        }

        return problemas;
    }

    private static int ScanGameObject(string assetPath, GameObject go)
    {
        if (go == null)
        {
            return 0;
        }

        int problemas = 0;
        bool temControleUnidade = go.GetComponent<ControleUnidade>() != null;
        bool temNavioRealista = go.GetComponent<ControleNavioRealista>() != null;
        bool temNavioInteligente = go.GetComponent<NavegacaoInteligenteNaval>() != null;
        bool temSubmarino = go.GetComponent<ControleSubmarino>() != null;
        bool temControleAviao = go.GetComponent<ControleAviao>() != null;
        bool temControleAviaoCaca = go.GetComponent<ControleAviaoCaca>() != null;
        bool temC700 = go.GetComponent<C700TransporteAereo>() != null;
        bool temHelicoptero = go.GetComponent<Helicoptero>() != null;
        bool temPatrulhaUniversal = go.GetComponent<ComportamentoPatrulhaUniversal>() != null;
        bool temSeguirUniversal = go.GetComponent<ComportamentoSeguirUniversal>() != null;

        if (temNavioRealista && temNavioInteligente)
        {
            problemas += RegistrarProblema(assetPath, go, "Prefab mistura ControleNavioRealista e NavegacaoInteligenteNaval no mesmo objeto.");
        }

        if (temSubmarino && (temNavioRealista || temNavioInteligente))
        {
            problemas += RegistrarProblema(assetPath, go, "Prefab mistura ControleSubmarino com executor naval de superficie.");
        }

        if (temHelicoptero && (temControleAviao || temC700))
        {
            problemas += RegistrarProblema(assetPath, go, "Prefab mistura Helicoptero com controlador aereo de aviao/transporte.");
        }

        if (temControleAviaoCaca && !temControleAviao)
        {
            problemas += RegistrarProblema(assetPath, go, "ControleAviaoCaca encontrado sem ControleAviao no mesmo objeto.");
        }

        if (temC700 && !temControleAviao)
        {
            problemas += RegistrarProblema(assetPath, go, "C700TransporteAereo encontrado sem ControleAviao no mesmo objeto.");
        }

        bool temExecutorPrincipal = temNavioRealista || temNavioInteligente || temSubmarino || temControleAviao || temC700 || temHelicoptero;
        if (temExecutorPrincipal && !temControleUnidade)
        {
            problemas += RegistrarProblema(assetPath, go, "Executor de dominio encontrado sem ControleUnidade como fachada oficial.");
        }

        if (temNavioInteligente)
        {
            problemas += RegistrarProblema(assetPath, go, "NavegacaoInteligenteNaval ainda esta serializada no prefab. Marque esta unidade para migracao naval.");
        }

        if (temPatrulhaUniversal || temSeguirUniversal)
        {
            problemas += RegistrarProblema(assetPath, go, "Comportamento universal de ordem foi salvo no prefab e deveria existir apenas como bridge temporaria em runtime.");
        }

        return problemas;
    }

    private static int ScanSerializedLegacyComponents(string assetPath)
    {
        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(absolutePath))
        {
            return 0;
        }

        string contents = File.ReadAllText(absolutePath);
        int problemas = 0;

        if (contents.Contains("Assembly-CSharp::ComportamentoPatrulha")
            || contents.Contains("Assembly-CSharp::ComportamentoPatrulhaCaminho")
            || contents.Contains("Assembly-CSharp::ComportamentoSeguir"))
        {
            Debug.LogWarning("[AuditoriaTrilhaControleUnidades] Prefab=" + assetPath + " | Comportamento legacy de patrulha/seguir ainda esta serializado no asset.");
            problemas++;
        }

        return problemas;
    }

    private static int ScanScriptAtPath(string assetPath)
    {
        if (assetPath.Contains("/Editor/") || assetPath.Contains("\\Editor\\"))
        {
            return 0;
        }

        if (ArquivosAutorizadosSetDestination.Contains(assetPath))
        {
            return 0;
        }

        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(absolutePath))
        {
            return 0;
        }

        string[] lines = File.ReadAllLines(absolutePath);
        int problemas = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//"))
            {
                continue;
            }

            if (!line.Contains("SetDestination("))
            {
                continue;
            }

            if (line.Contains(MarcadorFallbackTransicional))
            {
                continue;
            }

            Debug.LogWarning(
                "[AuditoriaTrilhaControleUnidades] Script=" + assetPath
                + " | Linha=" + (i + 1)
                + " | Chamada direta de SetDestination fora da trilha oficial. Migre para ControleUnidade.",
                AssetDatabase.LoadAssetAtPath<Object>(assetPath));
            problemas++;
        }

        return problemas;
    }

    private static int RegistrarProblema(string assetPath, GameObject go, string mensagem)
    {
        Debug.LogWarning(
            "[AuditoriaTrilhaControleUnidades] Prefab=" + assetPath
            + " | Objeto=" + GetHierarchyPath(go.transform)
            + " | " + mensagem,
            go);
        return 1;
    }

    private static string GetHierarchyPath(Transform current)
    {
        if (current == null)
        {
            return "(null)";
        }

        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }
}

public sealed class AuditoriaTrilhaControleUnidadesPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        int problemas = AuditoriaTrilhaControleUnidades.ScanImportedAssets(importedAssets);
        if (problemas > 0)
        {
            Debug.LogWarning("[AuditoriaTrilhaControleUnidades] Problemas detectados automaticamente em assets importados: " + problemas);
        }
    }
}
