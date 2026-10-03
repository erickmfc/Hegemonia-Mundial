using System;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    /// <summary>Marca uma unidade de jogo real que transporta o chefe de Estado.</summary>
    [DisallowMultipleComponent]
    public sealed class IA03MarcaPresidencial : MonoBehaviour
    {
        public static event Action<int, int, string> PresidenteAbatido;

        [Header("Identificação presidencial")]
        [SerializeField, Min(1)] private int paisTeamId = 1;
        [SerializeField] private string nomePresidente = string.Empty;

        public int PaisTeamId => paisTeamId;
        public string NomePresidente => nomePresidente;

        private void OnEnable()
        {
            SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
            SistemaDeDanos.OnMorteGlobal += AoMorrerUnidade;
        }

        private void OnDisable()
        {
            SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
        }

        private void AoMorrerUnidade(SistemaDeDanos vitima, GameObject agressor)
        {
            if (vitima == null || (vitima.transform != transform && !vitima.transform.IsChildOf(transform)))
            {
                return;
            }

            IdentidadeUnidade identidadeAgressor = agressor != null ? agressor.GetComponentInParent<IdentidadeUnidade>() : null;
            int agressorTeamId = identidadeAgressor != null ? identidadeAgressor.teamID : 0;
            PresidenteAbatido?.Invoke(paisTeamId, agressorTeamId, nomePresidente);
        }
    }
}
