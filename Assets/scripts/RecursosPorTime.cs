using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EstadoRecursosTime
{
    public int teamId;
    public int petroleoBruto;
    public float combustivel;
    public int petroleoEntregue;
    public int petroleoConvertido;
    public float ultimaAtualizacao;
}

public static class RecursosPorTime
{
    private static readonly Dictionary<int, EstadoRecursosTime> _porTime = new Dictionary<int, EstadoRecursosTime>();

    public static EstadoRecursosTime Obter(int teamId)
    {
        int id = Mathf.Max(1, teamId);
        EstadoRecursosTime estado;
        if (!_porTime.TryGetValue(id, out estado))
        {
            estado = new EstadoRecursosTime
            {
                teamId = id,
                ultimaAtualizacao = Time.time
            };
            _porTime[id] = estado;
        }

        return estado;
    }

    public static int ObterTeamId(Component origem)
    {
        if (origem == null)
        {
            return 0;
        }

        IdentidadeUnidade identidade = origem.GetComponent<IdentidadeUnidade>();
        if (identidade == null)
        {
            identidade = origem.GetComponentInParent<IdentidadeUnidade>();
        }

        if (identidade != null && identidade.teamID > 0)
        {
            return identidade.teamID;
        }

        IdentidadeIA identidadeIA = origem.GetComponent<IdentidadeIA>();
        if (identidadeIA == null)
        {
            identidadeIA = origem.GetComponentInParent<IdentidadeIA>();
        }

        if (identidadeIA != null && identidadeIA.teamID > 0)
        {
            return identidadeIA.teamID;
        }

        return 0;
    }

    public static int ObterTeamId(GameObject origem)
    {
        return origem != null ? ObterTeamId(origem.transform) : 0;
    }

    public static int ReceberPetroleoNoPier(PierMarinha pier, int quantidade)
    {
        int teamId = ObterTeamId(pier);
        return AdicionarPetroleoBruto(teamId, quantidade);
    }

    public static int AdicionarPetroleoBruto(int teamId, int quantidade)
    {
        int recebido = Mathf.Max(0, quantidade);
        if (teamId <= 0 || recebido <= 0)
        {
            return 0;
        }

        int id = teamId;
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null
            || !industrial.TentarAdicionar(id.ToString(), IndustriaIds.PetroleoBruto, recebido))
        {
            return 0;
        }

        EstadoRecursosTime estado = Obter(id);
        estado.petroleoBruto += recebido;
        estado.petroleoEntregue += recebido;
        estado.ultimaAtualizacao = Time.time;

        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica(
            "petroleo_time_" + id,
            "bruto_entregue=" + estado.petroleoEntregue);

        return recebido;
    }

    // Compatibilidade com cenas/testes antigos: a opção anterior marcava como
    // convertido um barril que continuava armazenado como petróleo bruto.
    // Conversão agora só ocorre por uma receita do refino industrial.
    public static int AdicionarPetroleoBruto(int teamId, int quantidade, bool converterEmCombustivel)
    {
        return AdicionarPetroleoBruto(teamId, quantidade);
    }

    public static bool TentarAbastecer(CombustivelUnidade unidade, float quantidade, out float abastecido)
    {
        abastecido = 0f;
        if (unidade == null || quantidade <= 0f || unidade.Capacidade <= 0f)
        {
            return false;
        }

        float espaco = unidade.Capacidade - unidade.CombustivelAtual;
        if (espaco <= 0.01f)
        {
            return false;
        }

        int teamId = ObterTeamId(unidade);
        string recursoCombustivel = ObterRecursoCombustivel(unidade.classe);
        if (string.IsNullOrEmpty(recursoCombustivel))
        {
            return false;
        }

        float desejado = Mathf.Min(quantidade, espaco);
        float disponivel;
        if (!TentarConsumirCombustivel(teamId, recursoCombustivel, desejado, out disponivel))
        {
            return false;
        }

        abastecido = unidade.Abastecer(disponivel);
        if (abastecido + 0.01f < disponivel)
        {
            DevolverCombustivel(teamId, recursoCombustivel, disponivel - abastecido);
        }

        return abastecido > 0.01f;
    }

    public static bool TentarCarregarCaminhao(CaminhaoTanqueAbastecimento caminhao, float quantidade, out float carregado)
    {
        carregado = 0f;
        if (caminhao == null || quantidade <= 0f)
        {
            return false;
        }

        float espaco = caminhao.EspacoCarga;
        if (espaco <= 0.01f)
        {
            return false;
        }

        int teamId = ObterTeamId(caminhao);
        // O caminhão-tanque existente atende apenas unidades terrestres.
        const string recursoCombustivel = IndustriaIds.Diesel;
        float desejado = Mathf.Min(quantidade, espaco);
        float disponivel;
        if (!TentarConsumirCombustivel(teamId, recursoCombustivel, desejado, out disponivel))
        {
            return false;
        }

        carregado = caminhao.CarregarSemCusto(disponivel);
        if (carregado + 0.01f < disponivel)
        {
            DevolverCombustivel(teamId, recursoCombustivel, disponivel - carregado);
        }

        return carregado > 0.01f;
    }

    private static bool TentarConsumirCombustivel(int teamId, string recursoCombustivel, float desejado, out float consumido)
    {
        consumido = 0f;
        if (string.IsNullOrWhiteSpace(recursoCombustivel) || desejado <= 0f)
        {
            return false;
        }

        if (teamId <= 0)
        {
            return false;
        }

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            return false;
        }

        string teamKey = teamId.ToString();
        double combustivelDisponivel = industrial.ObterQuantidadePais(teamId, recursoCombustivel);
        float possivel = Mathf.Min(desejado, (float)combustivelDisponivel);
        if (possivel <= 0.01f)
        {
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica(
                "combustivel_bloqueio_time_" + teamId,
                "estoque insuficiente: " + recursoCombustivel);
            return false;
        }

        if (!industrial.TentarConsumir(teamKey, recursoCombustivel, possivel))
        {
            return false;
        }

        EstadoRecursosTime estado = Obter(teamId);
        estado.ultimaAtualizacao = Time.time;
        consumido = possivel;
        return true;
    }

    private static void DevolverCombustivel(int teamId, string recursoCombustivel, float quantidade)
    {
        if (string.IsNullOrWhiteSpace(recursoCombustivel) || quantidade <= 0.01f)
        {
            return;
        }

        if (teamId <= 0)
        {
            return;
        }

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            return;
        }

        industrial.TentarAdicionar(teamId.ToString(), recursoCombustivel, quantidade);
        Obter(teamId).ultimaAtualizacao = Time.time;
    }

    private static string ObterRecursoCombustivel(ClasseCombustivelUnidade classe)
    {
        switch (classe)
        {
            case ClasseCombustivelUnidade.Terrestre: return IndustriaIds.Diesel;
            case ClasseCombustivelUnidade.Naval: return IndustriaIds.CombustivelNaval;
            case ClasseCombustivelUnidade.Aerea: return IndustriaIds.CombustivelAviacao;
            default: return string.Empty;
        }
    }
}
