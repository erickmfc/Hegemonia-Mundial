using System;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public enum DeusaPersonalidade
    {
        Militarista,
        Economica,
        Naval,
        Aerea,
        Defensiva,
        Diplomatica,
        Expansionista,
        Aleatoria
    }

    public enum DeusaModoInicial
    {
        Paz,
        Normal,
        Guerra,
        Manual
    }

    public enum DeusaEstagio
    {
        Inicializacao = 0,
        FundacaoNacional = 1,
        OrganizacaoEconomica = 2,
        ExpansaoTerritorial = 3,
        Industrializacao = 4,
        MilitarizacaoDefensiva = 5,
        ProjecaoRegional = 6,
        TensaoGeopolitica = 7,
        GuerraTotal = 8
    }

    public enum DeusaNivelEspionagem
    {
        Desligada = 0,
        Justa = 1,
        Avancada = 2
    }

    public enum DeusaDificuldade
    {
        Facil,
        Normal,
        Dificil,
        Extrema
    }

    [Serializable]
    public sealed class IA_DeusaConfig
    {
        public bool modoObservadorDebug = true;
        public bool bloquearFilaBrainMasterEmObservador = false;
        public DeusaModoInicial modoInicial = DeusaModoInicial.Normal;
        public DeusaEstagio estagioInicialManual = DeusaEstagio.FundacaoNacional;
        public bool travarEstagio;
        public DeusaPersonalidade personalidade = DeusaPersonalidade.Aleatoria;
        public DeusaDificuldade dificuldade = DeusaDificuldade.Normal;
        [Range(0, 100)] public int vantagemInicial;
        public bool usarEspionagemJusta = true;
        public bool permitirComercioComJogador = true;
        public bool permitirComercioComOutrasIAs = true;
        public bool permitirSancoes = true;
        public bool permitirGuerraTotal = true;

        public float MultiplicadorMilitar()
        {
            switch (dificuldade)
            {
                case DeusaDificuldade.Facil:
                    return 0.90f;
                case DeusaDificuldade.Dificil:
                    return 1.12f;
                case DeusaDificuldade.Extrema:
                    return 1.28f;
                default:
                    return 1f;
            }
        }

        public int BonusIntel()
        {
            switch (dificuldade)
            {
                case DeusaDificuldade.Dificil:
                    return 1;
                case DeusaDificuldade.Extrema:
                    return 2;
                default:
                    return 0;
            }
        }
    }
}
