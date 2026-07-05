using UnityEngine;

namespace Hegemonia.AI.Sovereign
{
    [DefaultExecutionOrder(-920)]
    public sealed class AISovereignBootstrapper : MonoBehaviour
    {
        private float _nextSyncTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapper()
        {
            GameObject existing = GameObject.Find("AISovereignBootstrapper");
            if (existing != null)
            {
                return;
            }

            GameObject root = new GameObject("AISovereignBootstrapper");
            root.hideFlags = HideFlags.DontSave;
            root.AddComponent<AISovereignBootstrapper>();
            DontDestroyOnLoad(root);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextSyncTime)
            {
                return;
            }

            _nextSyncTime = Time.unscaledTime + 5f;
            SistemaGovernoMundial.GarantirInstancia();
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null)
            {
                return;
            }

            for (int i = 0; i < gov.Paises.Count; i++)
            {
                DadosPaisGoverno pais = gov.Paises[i];
                if (pais == null || pais.teamId <= gov.teamJogador || AISovereignRuntime.Instance.HasControllerForTeam(pais.teamId))
                {
                    continue;
                }

                GameObject go = new GameObject("AISovereign_Team_" + pais.teamId);
                go.SetActive(false);
                DontDestroyOnLoad(go);
                AISovereignController controller = go.AddComponent<AISovereignController>();
                controller.ConfigureRuntimeTeam(pais.teamId);
                go.SetActive(true);
            }
        }
    }
}
