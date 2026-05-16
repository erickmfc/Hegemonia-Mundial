using System;

namespace Hegemonia.AI.DEUSA
{
    public enum DeusaTipoPrioridade
    {
        ConstruirHQ,
        ConstruirFarm,
        ConstruirCasa,
        ConstruirEnergia,
        ConstruirRadar,
        ConstruirAeroporto,
        ConstruirEstaleiro,
        ConstruirPier,
        ConstruirPlataforma,
        ConstruirQuartel,
        ConstruirIndustria,
        CriarPetroleiro,
        CriarEscoltaNaval,
        CriarEsquadraoAereo,
        CriarInfantaria,
        CriarTanques,
        EspionarJogador,
        DefenderHQ,
        DefenderPetroleo,
        AtacarRadar,
        AtacarEnergia,
        AtacarPetroleo,
        PrepararDesembarque,
        MonitorarSituacao
    }

    [Serializable]
    public sealed class IA_DeusaPrioridade
    {
        public DeusaTipoPrioridade tipo;
        public int peso;
        public string detalhe;

        public IA_DeusaPrioridade()
        {
        }

        public IA_DeusaPrioridade(DeusaTipoPrioridade tipo, int peso, string detalhe)
        {
            this.tipo = tipo;
            this.peso = peso;
            this.detalhe = detalhe;
        }

        public override string ToString()
        {
            return tipo + " (" + peso + ") " + detalhe;
        }
    }
}
