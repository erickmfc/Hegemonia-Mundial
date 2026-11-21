using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.EditorTools
{
    public static class GlobalMapSceneStructureDiagnostic
    {
        private sealed class RootSummary
        {
            public string Name;
            public int Objects;
            public int Renderers;
            public int MeshFilters;
            public int Colliders;
            public int MonoBehaviours;
            public int MaximumDepth;
            public int PrefabAssetCount;
            public long PrefabDependencyBytes;
        }

        private sealed class DependencyClosureSummary
        {
            public string Path;
            public long FileBytes;
            public long ClosureBytes;
            public int ClosureFileCount;
            public List<KeyValuePair<string, long>> LargestFiles = new List<KeyValuePair<string, long>>();
        }

        public static void Export()
        {
            const string scenePath = "Assets/Scenes/GlobalMapRTS.unity";
            const string outputPath = "D:/HegemoniaValidation/GlobalMapRTS-SceneStructure-20261120.txt";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            List<RootSummary> summaries = new List<RootSummary>(roots.Length);
            int totalObjects = 0;

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                    continue;

                RootSummary summary = new RootSummary { Name = root.name };
                HashSet<string> prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Stack<KeyValuePair<Transform, int>> pending = new Stack<KeyValuePair<Transform, int>>();
                pending.Push(new KeyValuePair<Transform, int>(root.transform, 0));
                while (pending.Count > 0)
                {
                    KeyValuePair<Transform, int> entry = pending.Pop();
                    Transform current = entry.Key;
                    if (current == null)
                        continue;

                    GameObject go = current.gameObject;
                    summary.Objects++;
                    summary.Renderers += go.GetComponent<Renderer>() != null ? 1 : 0;
                    summary.MeshFilters += go.GetComponent<MeshFilter>() != null ? 1 : 0;
                    summary.Colliders += go.GetComponent<Collider>() != null ? 1 : 0;
                    summary.MonoBehaviours += go.GetComponents<MonoBehaviour>().Length;
                    summary.MaximumDepth = Math.Max(summary.MaximumDepth, entry.Value);

                    GameObject sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (sourceObject != null)
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(sourceObject);
                        if (!string.IsNullOrEmpty(prefabPath)
                            && prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        {
                            prefabPaths.Add(prefabPath);
                        }
                    }

                    for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                        pending.Push(new KeyValuePair<Transform, int>(current.GetChild(childIndex), entry.Value + 1));
                }

                summary.PrefabAssetCount = prefabPaths.Count;
                HashSet<string> rootDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string prefabPath in prefabPaths)
                {
                    string[] prefabDependencies = AssetDatabase.GetDependencies(prefabPath, true);
                    for (int i = 0; i < prefabDependencies.Length; i++)
                        rootDependencies.Add(prefabDependencies[i]);
                }

                foreach (string dependency in rootDependencies)
                {
                    if (File.Exists(dependency))
                        summary.PrefabDependencyBytes += new FileInfo(dependency).Length;
                }

                totalObjects += summary.Objects;
                summaries.Add(summary);
            }

            StringBuilder output = new StringBuilder(64 * 1024);
            output.AppendLine("GlobalMapRTS scene hierarchy diagnostic");
            output.AppendLine("Scene: " + scene.path);
            output.AppendLine("Root GameObjects: " + roots.Length);
            output.AppendLine("GameObjects counted through Transform hierarchy: " + totalObjects);
            output.AppendLine("Root summaries sorted by object count; prefab dependency totals may overlap across roots:");
            foreach (RootSummary summary in summaries.OrderByDescending(item => item.Objects).Take(80))
            {
                output.AppendLine(string.Format(
                    "objects={0} renderers={1} meshFilters={2} colliders={3} monoBehaviours={4} maxDepth={5} prefabAssets={6} prefabDependencyBytes={7} root={8}",
                    summary.Objects,
                    summary.Renderers,
                    summary.MeshFilters,
                    summary.Colliders,
                    summary.MonoBehaviours,
                    summary.MaximumDepth,
                    summary.PrefabAssetCount,
                    summary.PrefabDependencyBytes,
                    summary.Name));
            }

            string[] directDependencies = AssetDatabase.GetDependencies(scenePath, false);
            List<KeyValuePair<string, long>> directDependencySizes = new List<KeyValuePair<string, long>>(directDependencies.Length);
            long totalDirectDependencyBytes = 0;
            for (int i = 0; i < directDependencies.Length; i++)
            {
                string dependency = directDependencies[i];
                if (string.IsNullOrEmpty(dependency) || !File.Exists(dependency))
                    continue;

                long size = new FileInfo(dependency).Length;
                totalDirectDependencyBytes += size;
                directDependencySizes.Add(new KeyValuePair<string, long>(dependency, size));
            }

            output.AppendLine();
            output.AppendLine("Direct scene dependencies: " + directDependencySizes.Count);
            output.AppendLine("Direct dependency file size total: " + totalDirectDependencyBytes + " bytes");
            output.AppendLine("Largest direct dependency files:");
            foreach (KeyValuePair<string, long> item in directDependencySizes.OrderByDescending(item => item.Value).Take(100))
                output.AppendLine("  bytes=" + item.Value + " path=" + item.Key);

            List<DependencyClosureSummary> directAssetClosures = new List<DependencyClosureSummary>();
            foreach (KeyValuePair<string, long> item in directDependencySizes)
            {
                string extension = Path.GetExtension(item.Key);
                if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] closurePaths = AssetDatabase.GetDependencies(item.Key, true);
                HashSet<string> uniqueClosurePaths = new HashSet<string>(closurePaths, StringComparer.OrdinalIgnoreCase);
                DependencyClosureSummary summary = new DependencyClosureSummary
                {
                    Path = item.Key,
                    FileBytes = item.Value,
                    ClosureFileCount = uniqueClosurePaths.Count
                };

                foreach (string dependency in uniqueClosurePaths)
                {
                    if (!File.Exists(dependency))
                        continue;

                    long size = new FileInfo(dependency).Length;
                    summary.ClosureBytes += size;
                    summary.LargestFiles.Add(new KeyValuePair<string, long>(dependency, size));
                }

                directAssetClosures.Add(summary);
            }

            output.AppendLine();
            output.AppendLine("Dependency closures grouped by each direct non-script scene reference; closure totals overlap and are not additive:");
            foreach (DependencyClosureSummary summary in directAssetClosures.OrderByDescending(item => item.ClosureBytes).Take(50))
            {
                output.AppendLine(string.Format(
                    "directBytes={0} closureBytes={1} closureFiles={2} path={3}",
                    summary.FileBytes,
                    summary.ClosureBytes,
                    summary.ClosureFileCount,
                    summary.Path));
            }

            output.AppendLine("Largest files per top 10 direct-reference closures:");
            foreach (DependencyClosureSummary summary in directAssetClosures.OrderByDescending(item => item.ClosureBytes).Take(10))
            {
                output.AppendLine("  direct=" + summary.Path);
                foreach (KeyValuePair<string, long> item in summary.LargestFiles.OrderByDescending(entry => entry.Value).Take(10))
                    output.AppendLine("    bytes=" + item.Value + " path=" + item.Key);
            }

            string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);
            List<KeyValuePair<string, long>> dependencySizes = new List<KeyValuePair<string, long>>(dependencies.Length);
            Dictionary<string, int> dependencyCountsByExtension = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            long totalDependencyBytes = 0;
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependency = dependencies[i];
                if (string.IsNullOrEmpty(dependency) || !File.Exists(dependency))
                    continue;

                FileInfo info = new FileInfo(dependency);
                totalDependencyBytes += info.Length;
                dependencySizes.Add(new KeyValuePair<string, long>(dependency, info.Length));
                string extension = Path.GetExtension(dependency);
                dependencyCountsByExtension[extension] = dependencyCountsByExtension.TryGetValue(extension, out int count)
                    ? count + 1
                    : 1;
            }

            output.AppendLine();
            output.AppendLine("Direct and transitive AssetDatabase dependencies: " + dependencySizes.Count);
            output.AppendLine("Dependency file size total: " + totalDependencyBytes + " bytes");
            output.AppendLine("Dependency counts by extension:");
            foreach (KeyValuePair<string, int> item in dependencyCountsByExtension.OrderByDescending(item => item.Value))
                output.AppendLine("  " + item.Key + ": " + item.Value);
            output.AppendLine("Largest dependency files:");
            foreach (KeyValuePair<string, long> item in dependencySizes.OrderByDescending(item => item.Value).Take(80))
                output.AppendLine("  bytes=" + item.Value + " path=" + item.Key);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, output.ToString(), new UTF8Encoding(false));
            Debug.Log("[GlobalMapSceneStructureDiagnostic] Wrote " + outputPath + " objects=" + totalObjects);
            EditorApplication.Exit(0);
        }
    }
}
