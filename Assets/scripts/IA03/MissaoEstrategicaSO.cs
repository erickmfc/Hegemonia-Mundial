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
        [SerializeField, Min(1f)] private float tempoMaximoSegundos = 600f;
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
        [SerializeField, Min(0f), Tooltip("Tempo contínuo que o grupo deve permanecer no Creaty para cumprir PermanecerNoDestino.")]
        private float tempoMinimoDePermanenciaSegundos = 30f;

        public string IdMissao => idMissao;
        public string NomeMissao => nomeMissao;
        public string Objetivo => objetivo;
        public IA03TipoMissao TipoMissao => tipoMissao;
        public IA03NivelConflito NivelMenosGrave => nivelMenosGrave;
        public IA03NivelConflito NivelMaisGrave => nivelMaisGrave;
        public IA03DominioEstrategico Dominio => dominio;
        public int QuantidadeMinimaDeUnidades => quantidadeMinimaDeUnidades;
        public int Prioridade => prioridade;
        public float TempoMaximoSegundos => Mathf.Max(1f, tempoMaximoSegundos);
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
        public float TempoMinimoDePermanenciaSegundos => tempoMinimoDePermanenciaSegundos;

        public bool Aceita(IA03NivelConflito nivel, IA03TipoCreaty tipoCreaty)
        {
            if (nivel > nivelMenosGrave || nivel < nivelMaisGrave)
            {
                return false;
            }

            if (!OperacaoPermitidaNoNivel(nivel)
                || !CreatyPermitidoNoNivel(nivel, tipoCreaty)
                || exigePortaAvioes
                || exigeTransporteNaval
                || exigePresidente
                || tipoCreaty == IA03TipoCreaty.ZonaPortaAvioes
                || tipoCreaty == IA03TipoCreaty.EmbarqueTransporte
                || tipoCreaty == IA03TipoCreaty.DesembarqueAnfibio
                || tipoCreaty == IA03TipoCreaty.HelipontoDiplomatico
                || tipoCreaty == IA03TipoCreaty.RecepcaoPresidencial)
            {
                return false;
            }

            if (tipoOrdem == IA03TipoOrdem.Atacar
                && (nivel > IA03NivelConflito.ConflitoLimitado
                    || (tipoMissao != IA03TipoMissao.AtaqueLimitado && tipoMissao != IA03TipoMissao.GuerraTotal)))
            {
                return false;
            }

            return creatysPermitidos == null || creatysPermitidos.Count == 0 || creatysPermitidos.Contains(tipoCreaty);
        }

        private bool OperacaoPermitidaNoNivel(IA03NivelConflito nivel)
        {
            if (tipoMissao == IA03TipoMissao.GrupoPortaAvioes
                || tipoMissao == IA03TipoMissao.InvasaoAnfibia
                || tipoMissao == IA03TipoMissao.VisitaPresidencial)
            {
                return false;
            }

            switch (nivel)
            {
                case IA03NivelConflito.Tensao:
                    return tipoMissao == IA03TipoMissao.Patrulha
                           || tipoMissao == IA03TipoMissao.PatrulhaSubmarino
                           || tipoMissao == IA03TipoMissao.DefesaDeObjetivo;
                case IA03NivelConflito.AvancoMilitar:
                    return tipoMissao == IA03TipoMissao.Patrulha
                           || tipoMissao == IA03TipoMissao.PatrulhaSubmarino
                           || tipoMissao == IA03TipoMissao.Avanco
                           || tipoMissao == IA03TipoMissao.DefesaDeObjetivo;
                case IA03NivelConflito.ConflitoLimitado:
                    return tipoMissao == IA03TipoMissao.Patrulha
                           || tipoMissao == IA03TipoMissao.PatrulhaSubmarino
                           || tipoMissao == IA03TipoMissao.Avanco
                           || tipoMissao == IA03TipoMissao.AtaqueLimitado
                           || tipoMissao == IA03TipoMissao.DefesaDeObjetivo;
                case IA03NivelConflito.GuerraTotal:
                    return tipoMissao == IA03TipoMissao.Patrulha
                           || tipoMissao == IA03TipoMissao.PatrulhaSubmarino
                           || tipoMissao == IA03TipoMissao.Avanco
                           || tipoMissao == IA03TipoMissao.AtaqueLimitado
                           || tipoMissao == IA03TipoMissao.GuerraTotal
                           || tipoMissao == IA03TipoMissao.DefesaDeObjetivo;
                default:
                    return false;
            }
        }

        private static bool CreatyPermitidoNoNivel(IA03NivelConflito nivel, IA03TipoCreaty tipo)
        {
            bool creatyDePatrulha = tipo == IA03TipoCreaty.PatrulhaTerrestre
                                    || tipo == IA03TipoCreaty.PatrulhaNaval
                                    || tipo == IA03TipoCreaty.PatrulhaAerea
                                    || tipo == IA03TipoCreaty.PatrulhaSubmarino
                                    || tipo == IA03TipoCreaty.PontoGenerico;
            switch (nivel)
            {
                case IA03NivelConflito.Tensao:
                    return creatyDePatrulha || tipo == IA03TipoCreaty.TensaoN4;
                case IA03NivelConflito.AvancoMilitar:
                    return creatyDePatrulha || tipo == IA03TipoCreaty.TensaoN4 || tipo == IA03TipoCreaty.AvancoN3;
                case IA03NivelConflito.ConflitoLimitado:
                    return creatyDePatrulha || tipo == IA03TipoCreaty.TensaoN4
                           || tipo == IA03TipoCreaty.AvancoN3 || tipo == IA03TipoCreaty.ConflitoN2;
                case IA03NivelConflito.GuerraTotal:
                    return creatyDePatrulha || tipo == IA03TipoCreaty.TensaoN4
                           || tipo == IA03TipoCreaty.AvancoN3 || tipo == IA03TipoCreaty.ConflitoN2
                           || tipo == IA03TipoCreaty.GuerraN1;
                default:
                    return false;
            }
        }

        private void OnValidate()
        {
            tempoMaximoSegundos = Mathf.Max(1f, tempoMaximoSegundos);
        }
    }
}
