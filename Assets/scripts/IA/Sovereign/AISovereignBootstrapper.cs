using System;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.AI.Sovereign
{
    [DefaultExecutionOrder(-920)]
    public sealed class AISovereignBootstrapper : MonoBehaviour
    {
        private const string IA03ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
        private float _nextSyncTime;

        internal static bool ShouldSkipBootstrapForScene(bool isEditor, string activeScenePath)
        {
            return isEditor && string.Equals(
                activeScenePath,
                IA03ValidationScenePath,
                StringComparison.OrdinalIgnoreCase);
        }

        internal static bool BrainMasterCanOwnTeam(
            int teamId,
            int brainTeamId,
            bool brainIsActive,
            IA_BrainMaster.IA_IntegrationMode integrationMode)
        {
            return teamId > 0
                && teamId == brainTeamId
                && brainIsActive
                && integrationMode != IA_BrainMaster.IA_IntegrationMode.ShadowReadOnly;
        }

        internal static bool ShouldBootstrapForTeam(
            int teamId,
            int playerTeamId,
            bool hasCommandCapableBrainMaster)
        {
            return teamId > 0
                && teamId != playerTeamId
                && !hasCommandCapableBrainMaster;
        }

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
            // A IA soberana e inicializada para a partida, nao para o menu.
            // Sem este bloqueio ela criava controladores persistentes antes de
            // o jogador iniciar uma campanha na build.
            if (ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
            {
                return;
            }

            if (!IA_SharedRuntimeSupport.IsBrainMasterMode)
            {
                return;
            }

            if (Time.unscaledTime < _nextSyncTime)
            {
                return;
            }

            _nextSyncTime = Time.unscaledTime + 5f;
            // A validacao IA03 isola somente os dois BrainMasters configurados
            // na cena; nao inicialize soberanos dos paises-padrao do governo.
            if (ShouldSkipBootstrapForScene(
                    Application.isEditor,
                    SceneManager.GetActiveScene().path))
            {
                return;
            }

            SistemaGovernoMundial.GarantirInstancia();
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null)
            {
                return;
            }

            IA_BrainMaster[] brainMasters = IA_UnitySearch.FindAll<IA_BrainMaster>();
            for (int i = 0; i < gov.Paises.Count; i++)
            {
                DadosPaisGoverno pais = gov.Paises[i];
                if (pais == null || AISovereignRuntime.Instance.HasControllerForTeam(pais.teamId))
                {
                    continue;
                }

                bool hasCommandCapableBrainMaster = false;
                for (int j = 0; j < brainMasters.Length; j++)
                {
                    IA_BrainMaster brain = brainMasters[j];
                    if (brain != null && BrainMasterCanOwnTeam(
                            pais.teamId,
                            brain.TeamId,
                            brain.isActiveAndEnabled,
                            brain.IntegrationMode))
                    {
                        hasCommandCapableBrainMaster = true;
                        break;
                    }
                }

                if (!ShouldBootstrapForTeam(pais.teamId, gov.teamJogador, hasCommandCapableBrainMaster))
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
