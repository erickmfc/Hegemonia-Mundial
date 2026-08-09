using UnityEngine;

namespace Hegemonia.RTS
{
    /// <summary>Cria os servicos centrais uma unica vez por execucao.</summary>
    public static class RTSRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntime()
        {
            GameObject root = GameObject.Find("RTS_CoreRuntime");
            if (root == null)
            {
                root = new GameObject("RTS_CoreRuntime");
                Object.DontDestroyOnLoad(root);
            }

            if (root.GetComponent<RTSGameSession>() == null) root.AddComponent<RTSGameSession>();
            if (root.GetComponent<RTSSimulationClock>() == null) root.AddComponent<RTSSimulationClock>();
            if (root.GetComponent<RTSResourceLedgerService>() == null) root.AddComponent<RTSResourceLedgerService>();
            if (root.GetComponent<RTSVisibilityService>() == null) root.AddComponent<RTSVisibilityService>();
            if (root.GetComponent<RTSObjectiveService>() == null) root.AddComponent<RTSObjectiveService>();
        }
    }
}
