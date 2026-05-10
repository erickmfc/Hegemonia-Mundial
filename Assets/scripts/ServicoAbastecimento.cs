using UnityEngine;

public static class ServicoAbastecimento
{
    public const float ConversaoPetroleoParaCombustivel = 1f;

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
