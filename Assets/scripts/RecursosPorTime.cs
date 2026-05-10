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
            return 1;
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

        return 1;
    }

    public static int ObterTeamId(GameObject origem)
    {
        return origem != null ? ObterTeamId(origem.transform) : 1;
    }

    public static int ReceberPetroleoNoPier(PierMarinha pier, int quantidade)
    {
        int teamId = ObterTeamId(pier);
        return AdicionarPetroleoBruto(teamId, quantidade, true);
    }

    public static int AdicionarPetroleoBruto(int teamId, int quantidade, bool converterEmCombustivel)
    {
        int recebido = Mathf.Max(0, quantidade);
        if (recebido <= 0)
        {
            return 0;
        }

        int id = Mathf.Max(1, teamId);
        EstadoRecursosTime estado = Obter(id);
        estado.petroleoBruto += recebido;
        estado.petroleoEntregue += recebido;
        estado.ultimaAtualizacao = Time.time;

        if (converterEmCombustivel)
        {
            estado.combustivel += recebido * ServicoAbastecimento.ConversaoPetroleoParaCombustivel;
            estado.petroleoConvertido += recebido;
        }

        if (id == 1)
        {
            GerenciadorRecursos bancoJogador = GerenciadorRecursos.Instancia;
            if (bancoJogador != null)
            {
                bancoJogador.AdicionarRecursos(addPetroleo: recebido);
            }
        }
        else
        {
            DadosPaisGoverno pais = ConectorGoverno.ObterPais(id);
            if (pais != null)
            {
                pais.petroleo = Mathf.Max(pais.petroleo, Mathf.RoundToInt(estado.combustivel));
            }
        }

        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica(
            "petroleo_time_" + id,
            "bruto=" + estado.petroleoBruto + " combustivel=" + estado.combustivel.ToString("0"));

        return recebido;
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
        float desejado = Mathf.Min(quantidade, espaco);
        float disponivel;
        if (!TentarConsumirCombustivel(teamId, desejado, out disponivel))
        {
            return false;
        }

        abastecido = unidade.Abastecer(disponivel);
        if (abastecido + 0.01f < disponivel)
        {
            DevolverCombustivel(teamId, disponivel - abastecido);
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
        float desejado = Mathf.Min(quantidade, espaco);
        float disponivel;
        if (!TentarConsumirCombustivel(teamId, desejado, out disponivel))
        {
            return false;
        }

        carregado = caminhao.CarregarSemCusto(disponivel);
        if (carregado + 0.01f < disponivel)
        {
            DevolverCombustivel(teamId, disponivel - carregado);
        }

        return carregado > 0.01f;
    }

    private static bool TentarConsumirCombustivel(int teamId, float desejado, out float consumido)
    {
        consumido = 0f;
        if (desejado <= 0f)
        {
            return false;
        }

        int id = Mathf.Max(1, teamId);
        if (id == 1)
        {
            GerenciadorRecursos bancoJogador = GerenciadorRecursos.Instancia;
            if (bancoJogador == null || bancoJogador.petroleo <= 0)
            {
                return false;
            }

            float possivel = Mathf.Min(desejado, bancoJogador.petroleo * ServicoAbastecimento.ConversaoPetroleoParaCombustivel);
            int custoPetroleo = Mathf.Clamp(
                Mathf.CeilToInt(possivel / ServicoAbastecimento.ConversaoPetroleoParaCombustivel),
                1,
                bancoJogador.petroleo);

            bancoJogador.RemoverRecurso("Petroleo", custoPetroleo);
            consumido = custoPetroleo * ServicoAbastecimento.ConversaoPetroleoParaCombustivel;
            return consumido > 0.01f;
        }

        EstadoRecursosTime estado = Obter(id);
        if (estado.combustivel < desejado)
        {
            ConverterReservaGovernamental(id, estado, desejado - estado.combustivel);
        }

        float possivelIA = Mathf.Min(desejado, estado.combustivel);
        if (possivelIA <= 0.01f)
        {
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("combustivel_bloqueio_time_" + id, "estoque insuficiente");
            return false;
        }

        estado.combustivel -= possivelIA;
        estado.ultimaAtualizacao = Time.time;
        consumido = possivelIA;
        return true;
    }

    private static void ConverterReservaGovernamental(int teamId, EstadoRecursosTime estado, float necessario)
    {
        DadosPaisGoverno pais = ConectorGoverno.ObterPais(teamId);
        if (pais == null || pais.petroleo <= 0 || necessario <= 0f)
        {
            return;
        }

        int bruto = Mathf.Clamp(Mathf.CeilToInt(necessario / ServicoAbastecimento.ConversaoPetroleoParaCombustivel), 0, pais.petroleo);
        if (bruto <= 0)
        {
            return;
        }

        pais.petroleo -= bruto;
        estado.petroleoBruto += bruto;
        estado.petroleoConvertido += bruto;
        estado.combustivel += bruto * ServicoAbastecimento.ConversaoPetroleoParaCombustivel;
        estado.ultimaAtualizacao = Time.time;
    }

    private static void DevolverCombustivel(int teamId, float quantidade)
    {
        if (quantidade <= 0.01f)
        {
            return;
        }

        int id = Mathf.Max(1, teamId);
        if (id == 1)
        {
            GerenciadorRecursos bancoJogador = GerenciadorRecursos.Instancia;
            if (bancoJogador != null)
            {
                bancoJogador.AdicionarRecursos(addPetroleo: Mathf.CeilToInt(quantidade / ServicoAbastecimento.ConversaoPetroleoParaCombustivel));
            }
            return;
        }

        EstadoRecursosTime estado = Obter(id);
        estado.combustivel += quantidade;
        estado.ultimaAtualizacao = Time.time;
    }
}
