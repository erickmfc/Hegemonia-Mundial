using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaPoliticaEstagio
    {
        public DeusaEstagio Estagio = DeusaEstagio.Inicializacao;
        public string Motivo = "aguardando";
        public bool PriorizarComida;
        public bool PriorizarMoradia;
        public bool PriorizarEnergia;
        public bool PriorizarIndustria;
        public bool PriorizarExpansao;
        public bool PriorizarDefesa;
        public bool PriorizarNaval;
        public bool PriorizarAereo;
        public bool PriorizarEspionagem;
        public bool PriorizarGuerraTotal;
        public int MinimoEsquadraoAereo = 4;
        public int MinimoAtaqueAereoPesado = 6;

        public string Resumo()
        {
            return Estagio + " | comida=" + PriorizarComida
                   + " | moradia=" + PriorizarMoradia
                   + " | energia=" + PriorizarEnergia
                   + " | industria=" + PriorizarIndustria
                   + " | naval=" + PriorizarNaval
                   + " | aereo=" + PriorizarAereo
                   + " | guerra=" + PriorizarGuerraTotal;
        }
    }

    public sealed class IA_DeusaEstagios
    {
        public IA_DeusaPoliticaEstagio Avaliar(
            IA_DeusaConfig config,
            IA_DeusaIdentidadeNacional identidade,
            DadosPaisGoverno pais,
            DadosEconomiaPais economia,
            IA_ForceSnapshot snapshot,
            IA_DeusaMapaMemoria mapa)
        {
            IA_DeusaPoliticaEstagio politica = new IA_DeusaPoliticaEstagio();
            DeusaEstagio resolvido = ResolverEstagio(config, identidade, pais, economia, snapshot);
            identidade.estagioAtual = resolvido;

            politica.Estagio = resolvido;
            politica.PriorizarComida = resolvido <= DeusaEstagio.OrganizacaoEconomica || (economia != null && economia.deficitComida > 0.5f);
            politica.PriorizarMoradia = resolvido <= DeusaEstagio.OrganizacaoEconomica || (economia != null && economia.pressaoPopulacional > 0.80f);
            politica.PriorizarEnergia = resolvido <= DeusaEstagio.Industrializacao || (economia != null && economia.deficitEnergia > 0.5f);
            politica.PriorizarIndustria = resolvido >= DeusaEstagio.Industrializacao;
            politica.PriorizarExpansao = resolvido >= DeusaEstagio.ExpansaoTerritorial;
            politica.PriorizarDefesa = resolvido >= DeusaEstagio.MilitarizacaoDefensiva;
            politica.PriorizarNaval = resolvido >= DeusaEstagio.ProjecaoRegional && mapa != null && mapa.TemAreaNavalValida;
            politica.PriorizarAereo = resolvido >= DeusaEstagio.ProjecaoRegional && mapa != null && mapa.TemAreaAereaValida;
            politica.PriorizarEspionagem = resolvido >= DeusaEstagio.MilitarizacaoDefensiva;
            politica.PriorizarGuerraTotal = resolvido >= DeusaEstagio.GuerraTotal && config.permitirGuerraTotal;
            politica.MinimoEsquadraoAereo = resolvido >= DeusaEstagio.ProjecaoRegional ? 4 : 0;
            politica.MinimoAtaqueAereoPesado = resolvido >= DeusaEstagio.GuerraTotal ? 8 : 6;
            politica.Motivo = ConstruirMotivo(config, pais, economia, snapshot, resolvido);
            return politica;
        }

        private static DeusaEstagio ResolverEstagio(
            IA_DeusaConfig config,
            IA_DeusaIdentidadeNacional identidade,
            DadosPaisGoverno pais,
            DadosEconomiaPais economia,
            IA_ForceSnapshot snapshot)
        {
            if (config == null)
            {
                return identidade != null ? identidade.estagioAtual : DeusaEstagio.Inicializacao;
            }

            if (config.modoInicial == DeusaModoInicial.Manual)
            {
                return config.estagioInicialManual;
            }

            if (config.travarEstagio && identidade != null)
            {
                return identidade.estagioAtual;
            }

            DeusaEstagio estagioBase = config.modoInicial == DeusaModoInicial.Paz
                ? DeusaEstagio.OrganizacaoEconomica
                : config.modoInicial == DeusaModoInicial.Guerra
                    ? DeusaEstagio.MilitarizacaoDefensiva
                    : DeusaEstagio.FundacaoNacional;

            if (snapshot == null)
            {
                return estagioBase;
            }

            DeusaEstagio desejado = estagioBase;
            if (snapshot.TotalOwnStructures <= 0)
            {
                desejado = DeusaEstagio.FundacaoNacional;
            }
            else if (economia != null && (economia.deficitComida > 0.5f || economia.pressaoPopulacional > 0.8f || economia.deficitEnergia > 0.5f))
            {
                desejado = Max(desejado, DeusaEstagio.OrganizacaoEconomica);
            }
            else if (snapshot.TotalOwnStructures >= 5)
            {
                desejado = Max(desejado, DeusaEstagio.ExpansaoTerritorial);
            }

            if (economia != null && economia.industriaProduzida >= 4f)
            {
                desejado = Max(desejado, DeusaEstagio.Industrializacao);
            }

            if (snapshot.BarracksCount > 0 || config.modoInicial == DeusaModoInicial.Guerra)
            {
                desejado = Max(desejado, DeusaEstagio.MilitarizacaoDefensiva);
            }

            if (snapshot.HasAirport || snapshot.HasNavalBase || snapshot.TotalCombatUnits >= 12)
            {
                desejado = Max(desejado, DeusaEstagio.ProjecaoRegional);
            }

            if ((pais != null && (pais.emGuerra || pais.rivalTeamId > 0 || pais.sancionado))
                || snapshot.VisibleEnemies > 0)
            {
                desejado = Max(desejado, DeusaEstagio.TensaoGeopolitica);
            }

            if (config.permitirGuerraTotal && pais != null && pais.emGuerra)
            {
                desejado = Max(desejado, DeusaEstagio.GuerraTotal);
            }

            if (config.modoInicial == DeusaModoInicial.Paz && desejado > DeusaEstagio.TensaoGeopolitica && (pais == null || !pais.emGuerra))
            {
                desejado = DeusaEstagio.TensaoGeopolitica;
            }

            if (identidade == null)
            {
                return desejado;
            }

            return Max(identidade.estagioAtual, desejado);
        }

        private static string ConstruirMotivo(IA_DeusaConfig config, DadosPaisGoverno pais, DadosEconomiaPais economia, IA_ForceSnapshot snapshot, DeusaEstagio estagio)
        {
            if (config != null && config.modoInicial == DeusaModoInicial.Manual)
            {
                return "estagio manual";
            }

            if (pais != null && pais.emGuerra && config != null && config.permitirGuerraTotal)
            {
                return "guerra declarada";
            }

            if (economia != null && economia.deficitEnergia > 0.5f)
            {
                return "deficit de energia";
            }

            if (economia != null && economia.deficitComida > 0.5f)
            {
                return "deficit de comida";
            }

            if (economia != null && economia.pressaoPopulacional > 0.8f)
            {
                return "pressao populacional";
            }

            if (snapshot != null && snapshot.HasAirport)
            {
                return "projecao aerea ativa";
            }

            return "progressao por infraestrutura: " + estagio;
        }

        private static DeusaEstagio Max(DeusaEstagio a, DeusaEstagio b)
        {
            return (DeusaEstagio)Mathf.Max((int)a, (int)b);
        }
    }
}
