using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PainelIndustriaUI : MonoBehaviour
{
    [Header("Contexto")]
    [SerializeField] private int teamIdSelecionado = 0;
    [SerializeField] private bool usarTeamDoJogadorQuandoDisponivel = true;

    [Header("Abas")]
    [SerializeField] private GameObject[] abas;

    [Header("Resumo")]
    [SerializeField] private TextMeshProUGUI textoResumo;
    [SerializeField] private TextMeshProUGUI textoEficiencia;
    [SerializeField] private TextMeshProUGUI textoEnergia;
    [SerializeField] private TextMeshProUGUI textoLinhas;
    [SerializeField] private TextMeshProUGUI textoProducaoDiaria;
    [SerializeField] private TextMeshProUGUI textoValorEstoque;
    [SerializeField] private TextMeshProUGUI textoDependenciaImportacoes;
    [SerializeField] private TextMeshProUGUI textoOrdensAtivas;

    [Header("Listas")]
    [SerializeField] private TextMeshProUGUI[] linhasExtracao;
    [SerializeField] private TextMeshProUGUI[] linhasRefino;
    [SerializeField] private TextMeshProUGUI textoHistorico;

    private void OnEnable()
    {
        ConectarEventos();
        AtualizarPainel();
    }

    private void Start()
    {
        ConectarEventos();
        AtualizarPainel();
    }

    private void OnDisable()
    {
        DesconectarEventos();
    }

    public void SelecionarPais(int novoTeamId)
    {
        teamIdSelecionado = Mathf.Max(1, novoTeamId);
        AtualizarPainel();
    }

    public void MostrarAba(int indice)
    {
        if (abas == null)
        {
            return;
        }

        for (int i = 0; i < abas.Length; i++)
        {
            if (abas[i] != null)
            {
                abas[i].SetActive(i == indice);
            }
        }
    }

    private void ConectarEventos()
    {
        if (SistemaIndustrialNacional.Instancia != null)
        {
            SistemaIndustrialNacional.Instancia.OnSistemaAtualizado -= AtualizarPainel;
            SistemaIndustrialNacional.Instancia.OnSistemaAtualizado += AtualizarPainel;
            SistemaIndustrialNacional.Instancia.OnPaisAtualizado -= AoPaisAtualizado;
            SistemaIndustrialNacional.Instancia.OnPaisAtualizado += AoPaisAtualizado;
        }

        if (SistemaGovernoMundial.Instancia != null)
        {
            SistemaGovernoMundial.Instancia.OnGovernoAtualizado -= AtualizarPainel;
            SistemaGovernoMundial.Instancia.OnGovernoAtualizado += AtualizarPainel;
        }
    }

    private void DesconectarEventos()
    {
        if (SistemaIndustrialNacional.Instancia != null)
        {
            SistemaIndustrialNacional.Instancia.OnSistemaAtualizado -= AtualizarPainel;
            SistemaIndustrialNacional.Instancia.OnPaisAtualizado -= AoPaisAtualizado;
        }

        if (SistemaGovernoMundial.Instancia != null)
        {
            SistemaGovernoMundial.Instancia.OnGovernoAtualizado -= AtualizarPainel;
        }
    }

    private void AoPaisAtualizado(int teamId)
    {
        if (teamId == teamIdSelecionado)
        {
            AtualizarPainel();
        }
    }

    private void AtualizarPainel()
    {
        if (usarTeamDoJogadorQuandoDisponivel && teamIdSelecionado <= 0 && SistemaGovernoMundial.Instancia != null)
        {
            teamIdSelecionado = Mathf.Max(1, SistemaGovernoMundial.Instancia.teamJogador);
        }

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            LimparCampos();
            return;
        }

        EstadoIndustrialPais estado = industrial.ObterEstadoPais(teamIdSelecionado);
        AtualizarResumo(estado);
        AtualizarListas(industrial);
        AtualizarHistorico(industrial);
    }

    private void AtualizarResumo(EstadoIndustrialPais estado)
    {
        if (estado == null)
        {
            LimparCampos();
            return;
        }

        SetTexto(textoResumo, estado.resumo);
        SetTexto(textoEficiencia, $"Eficiência: {estado.eficienciaIndustrial:P0}");
        SetTexto(textoEnergia, $"Energia: {estado.energiaDisponivel:P0}");
        SetTexto(textoLinhas, $"Linhas: {estado.linhasOcupadas}/{estado.linhasDisponiveis}");
        SetTexto(textoProducaoDiaria, $"Produção diária: {estado.producaoDiariaTotal:N0} t");
        SetTexto(textoValorEstoque, $"Valor do estoque: ${estado.valorEstoqueTotal:N0}");
        SetTexto(textoDependenciaImportacoes, $"Dependência: {estado.dependenciaImportacoes:P0}");
        SetTexto(textoOrdensAtivas, $"Ordens ativas: {estado.ordensAtivas}");
    }

    private void AtualizarListas(SistemaIndustrialNacional industrial)
    {
        List<OrdemExtracaoIndustrial> ordensExtracao = industrial.OrdensExtracao.Where(o => o != null && o.teamId == teamIdSelecionado).ToList();
        List<OrdemRefinoIndustrial> ordensRefino = industrial.OrdensRefino.Where(o => o != null && o.teamId == teamIdSelecionado).ToList();

        PreencherLista(linhasExtracao, ordensExtracao.Select(FormatarOrdemExtracao).ToList());
        PreencherLista(linhasRefino, ordensRefino.Select(FormatarOrdemRefino).ToList());
    }

    private void AtualizarHistorico(SistemaIndustrialNacional industrial)
    {
        if (textoHistorico == null)
        {
            return;
        }

        List<SaveHistoricoIndustrial> eventos = industrial.Historico
            .Where(h => h != null && h.teamId == teamIdSelecionado)
            .OrderByDescending(h => h.dia)
            .Take(10)
            .ToList();

        if (eventos.Count == 0)
        {
            textoHistorico.text = "Sem histórico industrial.";
            return;
        }

        textoHistorico.text = string.Join("\n", eventos.Select(FormatarHistorico));
    }

    private static void PreencherLista(TextMeshProUGUI[] campos, List<string> linhas)
    {
        if (campos == null)
        {
            return;
        }

        for (int i = 0; i < campos.Length; i++)
        {
            if (campos[i] == null)
            {
                continue;
            }

            campos[i].text = i < linhas.Count ? linhas[i] : string.Empty;
        }
    }

    private static string FormatarOrdemExtracao(OrdemExtracaoIndustrial ordem)
    {
        if (ordem == null)
        {
            return string.Empty;
        }

        return $"{ordem.nomeRecurso} | {ordem.estado} | {ordem.producaoUltimoDia:N0} t | {ordem.diasRestantes} dia(s)";
    }

    private static string FormatarOrdemRefino(OrdemRefinoIndustrial ordem)
    {
        if (ordem == null)
        {
            return string.Empty;
        }

        return $"{ordem.receitaId} | {ordem.estado} | {ordem.progresso:P0} | {ordem.diasRestantes} dia(s)";
    }

    private static string FormatarHistorico(SaveHistoricoIndustrial item)
    {
        return $"D{item.dia}: {item.mensagem}";
    }

    private void LimparCampos()
    {
        SetTexto(textoResumo, string.Empty);
        SetTexto(textoEficiencia, string.Empty);
        SetTexto(textoEnergia, string.Empty);
        SetTexto(textoLinhas, string.Empty);
        SetTexto(textoProducaoDiaria, string.Empty);
        SetTexto(textoValorEstoque, string.Empty);
        SetTexto(textoDependenciaImportacoes, string.Empty);
        SetTexto(textoOrdensAtivas, string.Empty);
        SetTexto(textoHistorico, string.Empty);

        if (linhasExtracao != null)
        {
            foreach (TextMeshProUGUI campo in linhasExtracao)
            {
                if (campo != null)
                {
                    campo.text = string.Empty;
                }
            }
        }

        if (linhasRefino != null)
        {
            foreach (TextMeshProUGUI campo in linhasRefino)
            {
                if (campo != null)
                {
                    campo.text = string.Empty;
                }
            }
        }
    }

    private static void SetTexto(TextMeshProUGUI campo, string texto)
    {
        if (campo != null)
        {
            campo.text = texto ?? string.Empty;
        }
    }
}
