using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class RunAircraftTestsInEditor
{
    private const string RanKey = "AircraftTests_Ran_Flag_v1";
    private static TestRunnerApi api;

    static RunAircraftTestsInEditor()
    {
        EditorApplication.delayCall += Trigger;
    }

    private static void Trigger()
    {
        if (SessionState.GetBool(RanKey, false)) return;
        SessionState.SetBool(RanKey, true);

        Debug.Log("[AircraftTestRunner] Iniciando execucao dos testes AircraftPurchasePlayModeTests...");

        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new TestListener());

        var filter = new Filter
        {
            testMode = TestMode.PlayMode,
            groupNames = new[] { "AircraftPurchasePlayModeTests" }
        };

        api.Execute(new ExecutionSettings(filter));
    }

    [MenuItem("Hegemonia/Testes/Executar AircraftPurchasePlayModeTests")]
    public static void ManualRun()
    {
        SessionState.SetBool(RanKey, false);
        Trigger();
    }

    private class TestListener : ICallbacks
    {
        private readonly StringBuilder report = new StringBuilder();
        private int totalRun = 0;
        private int passed = 0;
        private int failed = 0;
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        public void RunStarted(ITestAdaptor testsToRun)
        {
            stopwatch.Start();
            report.AppendLine("=== EXECUCAO REAL DO TEST RUNNER UNITY ===");
            report.AppendLine($"Inicio: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Total de nos na arvore: {testsToRun.TestCaseCount}");
            report.AppendLine("------------------------------------------");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            stopwatch.Stop();
            report.AppendLine("------------------------------------------");
            report.AppendLine($"FINALIZADO em {stopwatch.ElapsedMilliseconds} ms ({stopwatch.Elapsed.TotalSeconds:F2}s)");
            report.AppendLine($"Status Geral: {result.TestStatus}");
            report.AppendLine($"Total Executados: {totalRun}");
            report.AppendLine($"PASS: {passed}");
            report.AppendLine($"FAIL: {failed}");
            report.AppendLine("==========================================");

            string outputPath = Path.Combine(Application.dataPath, "../../test_runner_real_results.txt");
            string fullPath = Path.GetFullPath(outputPath);
            File.WriteAllText(fullPath, report.ToString());
            Debug.Log($"[AircraftTestRunner] Relatorio gravado com sucesso em: {fullPath}");
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;

            totalRun++;
            if (result.TestStatus == TestStatus.Passed) passed++;
            else failed++;

            report.AppendLine($"TESTE: {result.Test.Name}");
            report.AppendLine($"STATUS: {result.TestStatus}");
            report.AppendLine($"DURACAO: {result.Duration:F4}s");
            if (!string.IsNullOrEmpty(result.Message))
            {
                report.AppendLine($"MENSAGEM: {result.Message}");
            }
            if (!string.IsNullOrEmpty(result.StackTrace))
            {
                report.AppendLine($"STACKTRACE:\n{result.StackTrace}");
            }
            if (!string.IsNullOrEmpty(result.Output))
            {
                report.AppendLine($"LOG:\n{result.Output.Trim()}");
            }
            report.AppendLine();
        }
    }
}
