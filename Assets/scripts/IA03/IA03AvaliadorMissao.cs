namespace Hegemonia.AI.IA03
{
    public enum IA03ResultadoMissao
    {
        Pendente,
        Sucesso,
        Fracasso,
        Cancelada,
        Expirada,
        EmAndamento
    }

    /// <summary>
    /// Regras determinísticas para avaliar as condições configuradas no
    /// ScriptableObject. Falhas observadas encerram a missão como Fracasso;
    /// condições não concluídas até o limite encerram como Expirada.
    /// </summary>
    public static class IA03AvaliadorMissao
    {
        public static IA03ResultadoMissao Avaliar(
            IA03CondicaoMissao sucesso,
            IA03CondicaoMissao fracasso,
            bool chegouAoDestino,
            bool permaneceuNoDestino,
            bool saiuDoDestino,
            bool alvoDestruido,
            bool territorioCapturado,
            bool territorioPerdido,
            bool grupoSobreviveuAteOPrazo,
            bool grupoPerdeuUnidade,
            bool prazoEncerrado,
            bool confirmacaoDeSucesso,
            bool confirmacaoDeFracasso)
        {
            if (CondicaoDeSucessoAtendida(
                    sucesso,
                    chegouAoDestino,
                    permaneceuNoDestino,
                    alvoDestruido,
                    territorioCapturado,
                    grupoSobreviveuAteOPrazo,
                    prazoEncerrado,
                    confirmacaoDeSucesso))
            {
                return IA03ResultadoMissao.Sucesso;
            }

            if (CondicaoDeFracassoAtendida(
                    fracasso,
                    saiuDoDestino,
                    territorioPerdido,
                    grupoPerdeuUnidade,
                    confirmacaoDeFracasso))
            {
                return IA03ResultadoMissao.Fracasso;
            }

            // O prazo máximo encerra qualquer missão sem sucesso, inclusive
            // configurações que escolhem uma condição de fracasso externa.
            if (prazoEncerrado)
            {
                return IA03ResultadoMissao.Expirada;
            }

            return IA03ResultadoMissao.EmAndamento;
        }

        private static bool CondicaoDeSucessoAtendida(
            IA03CondicaoMissao condicao,
            bool chegouAoDestino,
            bool permaneceuNoDestino,
            bool alvoDestruido,
            bool territorioCapturado,
            bool grupoSobreviveuAteOPrazo,
            bool prazoEncerrado,
            bool confirmacaoDeSucesso)
        {
            switch (condicao)
            {
                case IA03CondicaoMissao.ChegarAoDestino:
                    return chegouAoDestino;
                case IA03CondicaoMissao.PermanecerNoDestino:
                    return permaneceuNoDestino;
                case IA03CondicaoMissao.DestruirAlvo:
                    return alvoDestruido;
                case IA03CondicaoMissao.SobreviverAteOPrazo:
                    return prazoEncerrado && grupoSobreviveuAteOPrazo;
                case IA03CondicaoMissao.ConfirmacaoExterna:
                    return confirmacaoDeSucesso;
                case IA03CondicaoMissao.CapturarTerritorio:
                    return territorioCapturado;
                default:
                    return false;
            }
        }

        private static bool CondicaoDeFracassoAtendida(
            IA03CondicaoMissao condicao,
            bool saiuDoDestino,
            bool territorioPerdido,
            bool grupoPerdeuUnidade,
            bool confirmacaoDeFracasso)
        {
            switch (condicao)
            {
                case IA03CondicaoMissao.PermanecerNoDestino:
                    return saiuDoDestino;
                case IA03CondicaoMissao.SobreviverAteOPrazo:
                    return grupoPerdeuUnidade;
                case IA03CondicaoMissao.ConfirmacaoExterna:
                    return confirmacaoDeFracasso;
                case IA03CondicaoMissao.CapturarTerritorio:
                    return territorioPerdido;
                default:
                    return false;
            }
        }
    }
}
