using System;
using System.Collections;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

public enum EstadoMissaoSinalPerdido
{
    Disponivel,
    ExpedicaoEmAndamento,
    AeronaveDesaparecida,
    BuscasIniciadas,
    Concluida
}

/// <summary>Evento único de desaparecimento que ativa a facção e libera buscas.</summary>
[DisallowMultipleComponent]
public sealed class MissaoOperacaoSinalPerdido : MonoBehaviour
{
    public static event Action OnOrganizacaoAtivada;
    public static MissaoOperacaoSinalPerdido Instancia { get; private set; }

    [Header("Missão")]
    [SerializeField, Min(0)] private long custoExpedicao = 250000;
    [SerializeField, Min(10f)] private float tempoAteDesaparecimento = 180f;
    [SerializeField, Min(1)] private int cientistasTransportados = 8;
    [SerializeField] private EstadoMissaoSinalPerdido estado = EstadoMissaoSinalPerdido.Disponivel;
    [Header("Aeronave e rota")]
    [SerializeField] private ControleAviao aeronaveDaExpedicao;
    [SerializeField] private GameObject prefabAeronaveCientifica;
    [SerializeField] private GerenciadorAeroporto aeroportoCivil;
    [SerializeField] private Transform pontoCriacaoAviao;
    [SerializeField] private Transform destinoDaExpedicao;
    [SerializeField] private RebeldeControlador controladorRebelde;

    private Coroutine temporizadorDesaparecimento;
    private bool eventoExecutado;
    public EstadoMissaoSinalPerdido Estado => estado;
    public long CustoExpedicao => custoExpedicao;
    public int CientistasTransportados => cientistasTransportados;
    public float TempoRestante => temporizadorDesaparecimento != null ? Mathf.Max(0f, tempoAteDesaparecimento - tempoDecorrido) : 0f;
    private float tempoDecorrido;
    public float TempoDecorrido => tempoDecorrido;
    public bool EventoExecutado => eventoExecutado;

    public SaveMissaoSinalPerdidoData CapturarEstadoSalvavel()
    {
        return new SaveMissaoSinalPerdidoData
        {
            estado = (int)estado,
            eventoExecutado = eventoExecutado,
            tempoDecorrido = tempoDecorrido
        };
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { enabled = false; return; }
        Instancia = this;
    }

    private void OnDestroy() { if (Instancia == this) Instancia = null; }

    public bool FinanciarExpedicao(out string mensagem)
    {
        mensagem = string.Empty;
        if (estado != EstadoMissaoSinalPerdido.Disponivel || eventoExecutado)
        { mensagem = "A Operação Sinal Perdido já foi iniciada."; return false; }
        if (destinoDaExpedicao == null)
        { mensagem = "Configure o destino da expedição no Inspector."; return false; }
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null)
        { mensagem = "Sistema de governo indisponível."; return false; }
        bool aeronaveCriadaAgora = false;
        if (aeronaveDaExpedicao == null)
        {
            if (prefabAeronaveCientifica == null || aeroportoCivil == null || pontoCriacaoAviao == null)
            { mensagem = "Configure o prefab científico, o aeroporto civil e PontoCriacaoAviao no Inspector."; return false; }
            if (pontoCriacaoAviao != aeroportoCivil.transform && !pontoCriacaoAviao.IsChildOf(aeroportoCivil.transform))
            { mensagem = "PontoCriacaoAviao precisa pertencer ao aeroporto civil configurado."; return false; }
            GameObject objeto = Instantiate(prefabAeronaveCientifica, pontoCriacaoAviao.position, pontoCriacaoAviao.rotation, aeroportoCivil.transform);
            aeronaveDaExpedicao = objeto.GetComponent<ControleAviao>();
            if (aeronaveDaExpedicao == null)
            {
                Destroy(objeto);
                aeronaveDaExpedicao = null;
                mensagem = "O prefab científico não possui ControleAviao.";
                return false;
            }
            aeronaveCriadaAgora = true;
            IdentidadeUnidade identidade = objeto.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = objeto.AddComponent<IdentidadeUnidade>();
            identidade.teamID = governo.teamJogador;
            identidade.nomeDoPais = governo.NomePais(governo.teamJogador);
            identidade.tipoUnidade = TipoUnidade.Aereo;
            if (!aeroportoCivil.RegistrarAeronaveRecebida(aeronaveDaExpedicao))
            {
                Destroy(objeto);
                aeronaveDaExpedicao = null;
                mensagem = "O aeroporto civil não conseguiu estacionar a aeronave científica.";
                return false;
            }
        }
        if (aeronaveDaExpedicao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio
            && aeronaveDaExpedicao.estadoAtual != ControleAviao.EstadoAviao.ReservaHangar)
        {
            if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
            mensagem = "A aeronave científica não está disponível no aeroporto.";
            return false;
        }
        IdentidadeUnidade[] identidadesAeronave = aeronaveDaExpedicao.GetComponentsInChildren<IdentidadeUnidade>(true);
        if (identidadesAeronave.Length == 0)
            identidadesAeronave = new[] { aeronaveDaExpedicao.gameObject.AddComponent<IdentidadeUnidade>() };
        for (int i = 0; i < identidadesAeronave.Length; i++)
        {
            if (identidadesAeronave[i] == null) continue;
            identidadesAeronave[i].teamID = governo.teamJogador;
            identidadesAeronave[i].nomeDoPais = governo.NomePais(governo.teamJogador);
            identidadesAeronave[i].tipoUnidade = TipoUnidade.Aereo;
            IA_WorldState.NotifyEntityChanged(identidadesAeronave[i]);
        }
        RegistroEntidadesJogo.NotificarAlteracao();
        if (!governo.TentarPagar(governo.teamJogador, custoExpedicao))
        {
            if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
            mensagem = "Saldo insuficiente para financiar a expedição.";
            return false;
        }
        estado = EstadoMissaoSinalPerdido.ExpedicaoEmAndamento;
        aeronaveDaExpedicao.ReceberOrdemManual(destinoDaExpedicao.position);
        temporizadorDesaparecimento = StartCoroutine(ExecutarDesaparecimentoUnico());
        governo.RegistrarNoticia("Operação Sinal Perdido: a expedição científica de oito pesquisadores decolou.");
        mensagem = "Expedição financiada. A aeronave está a caminho.";
        return true;
    }

    private void DescartarAeronaveDaExpedicao()
    {
        if (aeronaveDaExpedicao == null) return;
        GerenciadorAeroporto aeroporto = aeronaveDaExpedicao.aeroportoOrigem != null
            ? aeronaveDaExpedicao.aeroportoOrigem : aeroportoCivil;
        if (aeroporto != null)
        {
            aeroporto.avioesNoPatio.Remove(aeronaveDaExpedicao);
            aeroporto.avioesNoHangar.Remove(aeronaveDaExpedicao);
        }
        Destroy(aeronaveDaExpedicao.gameObject);
        aeronaveDaExpedicao = null;
    }

    private IEnumerator ExecutarDesaparecimentoUnico()
    {
        while (tempoDecorrido < tempoAteDesaparecimento)
        {
            tempoDecorrido += Time.deltaTime;
            yield return null;
        }
        DescartarAeronaveDaExpedicao();
        temporizadorDesaparecimento = null;
        MarcarDesaparecimento();
    }

    private void MarcarDesaparecimento()
    {
        if (estado != EstadoMissaoSinalPerdido.ExpedicaoEmAndamento) return;
        eventoExecutado = true;
        estado = EstadoMissaoSinalPerdido.AeronaveDesaparecida;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Sinal Perdido: a aeronave científica desapareceu; seu rastreamento foi interrompido.");
        if (controladorRebelde != null) controladorRebelde.Ativar();
        OnOrganizacaoAtivada?.Invoke();
    }

    public bool IniciarBuscas(out string mensagem)
    {
        if (estado != EstadoMissaoSinalPerdido.AeronaveDesaparecida)
        { mensagem = "As buscas só podem começar após o desaparecimento da aeronave."; return false; }
        estado = EstadoMissaoSinalPerdido.BuscasIniciadas;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Sinal Perdido: buscas iniciadas. Unidades aéreas e navais podem ser enviadas à última área conhecida.");
        mensagem = "Buscas iniciadas. Use unidades adequadas para investigar a última área conhecida.";
        return true;
    }

    public void RestaurarEstadoSalvo(int estadoSalvo, bool eventoJaExecutado, float tempo)
    {
        estado = (EstadoMissaoSinalPerdido)Mathf.Clamp(estadoSalvo, 0, Enum.GetValues(typeof(EstadoMissaoSinalPerdido)).Length - 1);
        eventoExecutado = eventoJaExecutado;
        tempoDecorrido = Mathf.Clamp(tempo, 0f, tempoAteDesaparecimento);
        if (eventoExecutado || estado >= EstadoMissaoSinalPerdido.AeronaveDesaparecida)
        {
            DescartarAeronaveDaExpedicao();
            eventoExecutado = true;
        }
        if (estado == EstadoMissaoSinalPerdido.ExpedicaoEmAndamento && !eventoExecutado)
            temporizadorDesaparecimento = StartCoroutine(ExecutarDesaparecimentoUnico());
        if (estado >= EstadoMissaoSinalPerdido.AeronaveDesaparecida) OnOrganizacaoAtivada?.Invoke();
    }
}
