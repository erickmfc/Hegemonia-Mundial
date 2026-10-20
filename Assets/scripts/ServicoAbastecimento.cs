using UnityEngine;

public static class ServicoAbastecimento
{
    public const float ConversaoPetroleoParaCombustivel = 1f;

    public static float AbastecerUnidadeProduzida(GameObject unidade)
    {
        if (unidade == null)
        {
            return 0f;
        }

        // Aeronaves comerciais mantêm a isenção operacional explícita do
        // projeto e não devem retirar petróleo do estoque militar nacional.
        if (unidade.GetComponent<ControleAviaoComercial>() != null)
        {
            return 0f;
        }

        CombustivelUnidade combustivel = CombustivelUnidade.Garantir(unidade, false);
        if (combustivel == null || !combustivel.PrepararTanqueParaCargaInicialPaga())
        {
            return 0f;
        }

        float totalAbastecido = 0f;
        if (combustivel.usaCombustivel && combustivel.Capacidade > 0f
            && RecursosPorTime.TentarAbastecer(combustivel, combustivel.Capacidade, out float abastecido))
        {
            totalAbastecido += abastecido;
        }

        CaminhaoTanqueAbastecimento caminhao = unidade.GetComponent<CaminhaoTanqueAbastecimento>();
        if (caminhao != null)
        {
            caminhao.cargaAtual = 0f;
            if (TentarCarregarCaminhao(caminhao, caminhao.CapacidadeCarga, out float cargaInicial))
            {
                totalAbastecido += cargaInicial;
            }
        }

        return totalAbastecido;
    }

    public static bool TentarAbastecer(CombustivelUnidade unidade, float quantidade, out float abastecido)
    {
        abastecido = 0f;
        if (unidade == null || quantidade <= 0f || unidade.Capacidade <= 0f)
        {
            return false;
        }

        return RecursosPorTime.TentarAbastecer(unidade, quantidade, out abastecido);
    }

    public static bool TentarCarregarCaminhao(CaminhaoTanqueAbastecimento caminhao, float quantidade, out float carregado)
    {
        carregado = 0f;
        if (caminhao == null || quantidade <= 0f)
        {
            return false;
        }

        return RecursosPorTime.TentarCarregarCaminhao(caminhao, quantidade, out carregado);
    }
}
