using System;

namespace Hegemonia.AI.DEUSA
{
    [Serializable]
    public sealed class IA_DeusaPoliticaNacional
    {
        public DeusaEstagio estagioAtual = DeusaEstagio.Inicializacao;
        public float focoEconomia;
        public float focoMilitar;
        public float focoExpansao;
        public float focoDiplomacia;
        public float focoDefesa;
        public float focoAtaque;
        public bool permitirGuerra;
        public bool permitirSancoes;
        public bool permitirComercio;
        public bool permitirExpansao;
        public bool modoObservador;
        public int metaMinimaSoldados;
        public int metaMinimaTanques;
        public int metaMinimaAvioes;
        public int metaMinimaNavios;
        public string alvoPrioritario = "Nenhum";
        public string proximaConstrucao = "Nenhuma";
        public string origemDaDecisao = "Aguardando";

        public string ResumoCurto()
        {
            return estagioAtual
                   + " | eco=" + focoEconomia.ToString("0.00")
                   + " mil=" + focoMilitar.ToString("0.00")
                   + " def=" + focoDefesa.ToString("0.00")
                   + " atk=" + focoAtaque.ToString("0.00")
                   + " | alvo=" + alvoPrioritario
                   + " | build=" + proximaConstrucao
                   + (modoObservador ? " | observador" : string.Empty);
        }
    }
}
