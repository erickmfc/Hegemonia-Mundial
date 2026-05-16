using System;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    [Serializable]
    public sealed class IA_DeusaIdentidadeNacional
    {
        private static readonly string[] Paises =
        {
            "Republica Boreal",
            "Dominio Solaris",
            "Federacao do Aco",
            "Uniao Oceana",
            "Confederacao Rubra",
            "Liga Meridian",
            "Pacto Aurora",
            "Diretorio Atlas"
        };

        private static readonly string[] Presidentes =
        {
            "Helena Voss",
            "Artur Nobre",
            "Caio Mercer",
            "Ayla Reznik",
            "Dario Salvat",
            "Nadia Korvin",
            "Leandro Sanz",
            "Marta Valen"
        };

        private static readonly string[] Moedas =
        {
            "Solari",
            "Atlas",
            "Boreal",
            "Valer",
            "Rubra",
            "Aurora",
            "Merid",
            "Dinar de Aco"
        };

        public string nomePais;
        public string nomePresidente;
        public string nomeMoeda;
        public int teamID;
        public DeusaPersonalidade personalidade = DeusaPersonalidade.Economica;
        public DeusaModoInicial modoInicial = DeusaModoInicial.Normal;
        public DeusaEstagio estagioAtual = DeusaEstagio.Inicializacao;

        public void GarantirDefaults(int teamId, DeusaPersonalidade personalidadeConfigurada, DeusaModoInicial modoConfigurado)
        {
            teamID = Mathf.Max(1, teamId);
            personalidade = ResolverPersonalidade(personalidadeConfigurada, teamID);
            modoInicial = modoConfigurado;

            int indice = Mathf.Abs(teamID - 1) % Paises.Length;
            if (string.IsNullOrWhiteSpace(nomePais))
            {
                nomePais = Paises[indice] + " " + teamID;
            }

            if (string.IsNullOrWhiteSpace(nomePresidente))
            {
                nomePresidente = Presidentes[indice];
            }

            if (string.IsNullOrWhiteSpace(nomeMoeda))
            {
                nomeMoeda = Moedas[indice];
            }
        }

        public string ResumoCurto()
        {
            return nomePais + " | presidente=" + nomePresidente + " | moeda=" + nomeMoeda + " | personalidade=" + personalidade;
        }

        private static DeusaPersonalidade ResolverPersonalidade(DeusaPersonalidade configurada, int teamId)
        {
            if (configurada != DeusaPersonalidade.Aleatoria)
            {
                return configurada;
            }

            DeusaPersonalidade[] opcoes =
            {
                DeusaPersonalidade.Militarista,
                DeusaPersonalidade.Economica,
                DeusaPersonalidade.Naval,
                DeusaPersonalidade.Aerea,
                DeusaPersonalidade.Defensiva,
                DeusaPersonalidade.Diplomatica,
                DeusaPersonalidade.Expansionista
            };

            return opcoes[Mathf.Abs(teamId * 17) % opcoes.Length];
        }
    }
}
