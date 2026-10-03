using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    [DisallowMultipleComponent]
    public sealed class CreatyEstrategico : MonoBehaviour
    {
        [Header("Identificação")]
        [SerializeField] private string id = string.Empty;
        [SerializeField, Min(1)] private int paisProprietarioTeamId = 1;
        [SerializeField, Min(0)] private int paisAlvoTeamId;

        [Header("Missão")]
        [SerializeField] private IA03TipoCreaty tipo = IA03TipoCreaty.PontoGenerico;
        [SerializeField] private IA03NivelConflito nivelDeConflito = IA03NivelConflito.Tensao;
        [SerializeField] private IA03DominioEstrategico dominio = IA03DominioEstrategico.Terrestre;
        [SerializeField, Range(0, 100)] private int prioridade = 50;
        [SerializeField, Min(0f)] private float pesoDeEscolha = 1f;
        [SerializeField, Min(1)] private int maximoDeUnidades = 12;
        [SerializeField, Min(0f)] private float tempoDeReutilizacaoSegundos = 60f;
        [SerializeField] private bool ativo = true;

        [Header("Rotas e diplomacia")]
        [SerializeField] private CreatyEstrategico proximoPonto;
        [SerializeField] private string grupo = string.Empty;
        [SerializeField] private List<int> paisesPermitidos = new List<int>();
        [SerializeField] private List<int> paisesProibidos = new List<int>();
        [SerializeField] private IA03AlvoPreferencial alvoPreferencial = IA03AlvoPreferencial.Nenhum;

        [Header("Gizmos")]
        [SerializeField, Min(0.25f)] private float tamanhoGizmo = 1.25f;

        private int unidadesReservadas;
        private float disponivelNovamenteEm;

        public string Id => id;
        public int PaisProprietarioTeamId => paisProprietarioTeamId;
        public int PaisAlvoTeamId => paisAlvoTeamId;
        public IA03TipoCreaty Tipo => tipo;
        public IA03NivelConflito NivelDeConflito => nivelDeConflito;
        public IA03DominioEstrategico Dominio => dominio;
        public int Prioridade => prioridade;
        public float PesoDeEscolha => pesoDeEscolha;
        public int MaximoDeUnidades => maximoDeUnidades;
        public float TempoDeReutilizacaoSegundos => tempoDeReutilizacaoSegundos;
        public bool Ativo => ativo && isActiveAndEnabled;
        public CreatyEstrategico ProximoPonto => proximoPonto;
        public string Grupo => grupo;
        public IA03AlvoPreferencial AlvoPreferencial => alvoPreferencial;
        public int UnidadesReservadas => unidadesReservadas;
        public bool EstaEmRecarga => Time.unscaledTime < disponivelNovamenteEm;

        private void OnEnable()
        {
            RegistroCreatysEstrategicos.Registrar(this);
        }

        private void OnDisable()
        {
            RegistroCreatysEstrategicos.Remover(this);
            unidadesReservadas = 0;
        }

        public bool PermiteAlvo(int teamId)
        {
            if (paisAlvoTeamId > 0 && paisAlvoTeamId != teamId)
            {
                return false;
            }

            if (paisesPermitidos != null && paisesPermitidos.Count > 0 && !paisesPermitidos.Contains(teamId))
            {
                return false;
            }

            return paisesProibidos == null || !paisesProibidos.Contains(teamId);
        }

        public bool PodeReservar(int solicitanteTeamId, int alvoTeamId, int quantidade)
        {
            return Ativo
                   && !EstaEmRecarga
                   && paisProprietarioTeamId == solicitanteTeamId
                   && PermiteAlvo(alvoTeamId)
                   && quantidade > 0
                   && unidadesReservadas + quantidade <= maximoDeUnidades;
        }

        public bool TentarReservar(int solicitanteTeamId, int alvoTeamId, int quantidade)
        {
            if (!PodeReservar(solicitanteTeamId, alvoTeamId, quantidade))
            {
                return false;
            }

            unidadesReservadas += quantidade;
            return true;
        }

        public void LiberarReserva(int quantidade)
        {
            unidadesReservadas = Mathf.Max(0, unidadesReservadas - Mathf.Max(0, quantidade));
            if (unidadesReservadas == 0)
            {
                disponivelNovamenteEm = Time.unscaledTime + tempoDeReutilizacaoSegundos;
            }
        }

        private void OnDrawGizmos()
        {
            Color color = ResolverCor();
            Gizmos.color = color;
            Vector3 centro = transform.position;
            Gizmos.DrawWireSphere(centro, tamanhoGizmo);
            Gizmos.DrawLine(centro - Vector3.up * tamanhoGizmo, centro + Vector3.up * tamanhoGizmo);
            if (proximoPonto != null)
            {
                Gizmos.DrawLine(centro, proximoPonto.transform.position);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            string alvo = paisAlvoTeamId > 0 ? " -> " + paisAlvoTeamId : string.Empty;
            string label = tipo + " N" + (int)nivelDeConflito + "\n" + paisProprietarioTeamId + alvo + "\n" + id;
            UnityEditor.Handles.Label(centro + Vector3.up * (tamanhoGizmo + 0.25f), label);
#endif
        }

        private Color ResolverCor()
        {
            switch (nivelDeConflito)
            {
                case IA03NivelConflito.GuerraTotal: return new Color(1f, 0.18f, 0.15f, 0.9f);
                case IA03NivelConflito.ConflitoLimitado: return new Color(1f, 0.48f, 0.12f, 0.9f);
                case IA03NivelConflito.AvancoMilitar: return new Color(1f, 0.83f, 0.16f, 0.9f);
                case IA03NivelConflito.Tensao: return new Color(0.2f, 0.75f, 1f, 0.9f);
                default: return new Color(0.3f, 1f, 0.55f, 0.9f);
            }
        }
    }
}
