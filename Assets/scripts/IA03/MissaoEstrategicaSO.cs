using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    [CreateAssetMenu(fileName = "NovaMissaoEstrategicaIA03", menuName = "Hegemonia/IA03/Missão Estratégica")]
    public sealed class MissaoEstrategicaSO : ScriptableObject
    {
        [Header("Identificação")]
        [SerializeField] private string idMissao = string.Empty;
        [SerializeField] private string nomeMissao = "Nova missão";
        [SerializeField] private string objetivo = string.Empty;
        [SerializeField] private IA03TipoMissao tipoMissao = IA03TipoMissao.Patrulha;

        [Header("Requisitos")]
        [SerializeField, Tooltip("Limite menos grave da faixa. Nível 4 é menos grave que nível 1.")]
        private IA03NivelConflito nivelMenosGrave = IA03NivelConflito.Tensao;
        [SerializeField, Tooltip("Limite mais grave da faixa. Nível 1 é o estágio mais grave.")]
        private IA03NivelConflito nivelMaisGrave = IA03NivelConflito.GuerraTotal;
        [SerializeField] private IA03DominioEstrategico dominio = IA03DominioEstrategico.Terrestre;
        [SerializeField, Min(1)] private int quantidadeMinimaDeUnidades = 1;
        [SerializeField, Min(0)] private int prioridade = 50;
        [SerializeField, Min(0f)] private float tempoMaximoSegundos = 600f;
        [SerializeField] private List<IA03TipoCreaty> creatysPermitidos = new List<IA03TipoCreaty>();
        [SerializeField] private IA03AlvoPreferencial alvo = IA03AlvoPreferencial.Nenhum;
        [SerializeField] private IA03TipoOrdem tipoOrdem = IA03TipoOrdem.Mover;
        [SerializeField] private bool exigePortaAvioes;
        [SerializeField] private bool exigeSubmarino;
        [SerializeField] private bool exigeTransporteNaval;
        [SerializeField] private bool exigePresidente;
        [SerializeField, Min(0)] private int minimoNaviosEscolta = 2;
        [SerializeField, Min(0)] private int minimoAeronaves = 0;

        [Header("Condição")]
        [SerializeField] private IA03CondicaoMissao condicaoDeSucesso = IA03CondicaoMissao.ChegarAoDestino;
        [SerializeField] private IA03CondicaoMissao condicaoDeFracasso = IA03CondicaoMissao.SobreviverAteOPrazo;

        public string IdMissao => idMissao;
        public string NomeMissao => nomeMissao;
        public string Objetivo => objetivo;
        public IA03TipoMissao TipoMissao => tipoMissao;
        public IA03NivelConflito NivelMenosGrave => nivelMenosGrave;
        public IA03NivelConflito NivelMaisGrave => nivelMaisGrave;
        public IA03DominioEstrategico Dominio => dominio;
        public int QuantidadeMinimaDeUnidades => quantidadeMinimaDeUnidades;
        public int Prioridade => prioridade;
        public float TempoMaximoSegundos => tempoMaximoSegundos;
        public IReadOnlyList<IA03TipoCreaty> CreatysPermitidos => creatysPermitidos;
        public IA03AlvoPreferencial Alvo => alvo;
        public IA03TipoOrdem TipoOrdem => tipoOrdem;
        public bool ExigePortaAvioes => exigePortaAvioes;
        public bool ExigeSubmarino => exigeSubmarino;
        public bool ExigeTransporteNaval => exigeTransporteNaval;
        public bool ExigePresidente => exigePresidente;
        public int MinimoNaviosEscolta => minimoNaviosEscolta;
        public int MinimoAeronaves => minimoAeronaves;
        public IA03CondicaoMissao CondicaoDeSucesso => condicaoDeSucesso;
        public IA03CondicaoMissao CondicaoDeFracasso => condicaoDeFracasso;

        public bool Aceita(IA03NivelConflito nivel, IA03TipoCreaty tipoCreaty)
        {
            if (nivel > nivelMenosGrave || nivel < nivelMaisGrave)
            {
                return false;
            }

            return creatysPermitidos == null || creatysPermitidos.Count == 0 || creatysPermitidos.Contains(tipoCreaty);
        }
    }
}
