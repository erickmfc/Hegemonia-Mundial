using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GerenciadorOperacoesPortaAvioesV2 : MonoBehaviour
{
    [Header("Sistema paralelo e fallback")]
    public bool usarSistemaOperacoesV2;
    public LayoutConvesPortaAvioesV2 layout;
    public float velocidadeTaxi = 12f;
    public float velocidadeAproximacao = 30f;
    public float timeoutPorEstado = 45f;
    public float velocidadeReabastecimento = 40f;
    public bool interiorHangarModelado;
    private readonly Dictionary<string, AeronaveEmbarcadaV2> aeronaves = new Dictionary<string, AeronaveEmbarcadaV2>();
    private readonly Dictionary<string, Coroutine> operacoes = new Dictionary<string, Coroutine>();
    private readonly Dictionary<string, int> tentativas = new Dictionary<string, int>();
    private readonly Dictionary<Transform, string> catapultasReservadas = new Dictionary<Transform, string>();
    private GerenciadorPortaAvioes legadoSuspenso;
    private bool legadoEstavaAtivo;
    private string autoridade => name + ".OperacoesV2";
    public event Action<AeronaveEmbarcadaV2, EstadoOperacaoPortaAvioesV2> OperacaoConcluida;
    public IReadOnlyDictionary<string, AeronaveEmbarcadaV2> Aeronaves => aeronaves;

    private void Awake() { if (layout == null) layout = GetComponentInChildren<LayoutConvesPortaAvioesV2>(); if (layout != null) { layout.interiorHangarModelado = interiorHangarModelado; layout.AtualizarListas(); } if (usarSistemaOperacoesV2) SuspenderLegadoDoNavio(); }
    private void OnDestroy() { foreach (var p in operacoes.Values) if (p != null) StopCoroutine(p); operacoes.Clear(); foreach (var a in aeronaves.Values) if (a != null) { LiberarRecursos(a); a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.FalhaControlada, "porta-aviões destruído"); a.LiberarAutoridade(autoridade); } if (legadoSuspenso != null) { legadoSuspenso.operacoesV2AssumiuControle = false; legadoSuspenso.enabled = legadoEstavaAtivo; } }
    private void SuspenderLegadoDoNavio() { legadoSuspenso = GetComponentInParent<GerenciadorPortaAvioes>(); if (legadoSuspenso != null) { legadoEstavaAtivo = legadoSuspenso.enabled; legadoSuspenso.StopAllCoroutines(); legadoSuspenso.operacoesV2AssumiuControle = true; legadoSuspenso.enabled = true; } }

    public AeronaveEmbarcadaV2 Registrar(ControleAviao controle)
    {
        if (controle == null) return null;
        var a = controle.GetComponent<AeronaveEmbarcadaV2>() ?? controle.gameObject.AddComponent<AeronaveEmbarcadaV2>();
        a.GarantirIdentidade(); aeronaves[a.Registro.id] = a; return a;
    }
    public AeronaveEmbarcadaV2 Registrar(GameObject objeto) { return objeto == null ? null : Registrar(objeto.GetComponent<ControleAviao>()); }
    public bool TryObter(string id, out AeronaveEmbarcadaV2 aeronave) { return aeronaves.TryGetValue(id, out aeronave) && aeronave != null; }

    /// <summary>
    /// Faz a ponte de compatibilidade com aeronaves que já estavam nas listas
    /// do menu antigo antes do V2 assumir o navio. Não move nem recria a
    /// aeronave: apenas sincroniza o registro lógico para que o próximo
    /// comando seja validado pelo V2.
    /// </summary>
    public AeronaveEmbarcadaV2 PrepararAeronaveParaMenu(ControleAviao controle, bool armazenadaNoHangar)
    {
        AeronaveEmbarcadaV2 aeronave = Registrar(controle);
        if (aeronave == null) return null;
        if (operacoes.ContainsKey(aeronave.Registro.id)) return aeronave;

        aeronave.Registro.portaAvioesAtual = name;
        aeronave.Registro.operacaoAtual = string.Empty;
        aeronave.Registro.motivoFalha = string.Empty;
        aeronave.Registro.vagaReservada = string.Empty;
        aeronave.Registro.catapultaReservada = string.Empty;

        bool achouVaga = false;
        List<VagaPortaAvioesV2> vagas = armazenadaNoHangar ? layout.vagasHangar : layout.vagasConves;
        if (vagas != null)
        {
            foreach (VagaPortaAvioesV2 vaga in vagas)
            {
                if (vaga == null) continue;
                if (controle.transform == vaga.transform || controle.transform.IsChildOf(vaga.transform))
                {
                    vaga.Ocupar(aeronave.Registro.id);
                    aeronave.Registro.vagaOcupada = vaga.id;
                    achouVaga = true;
                    break;
                }
            }
        }

        if (!achouVaga) aeronave.Registro.vagaOcupada = string.Empty;
        aeronave.ForcarEstadoSeguro(
            armazenadaNoHangar
                ? EstadoOperacaoPortaAvioesV2.ArmazenadoNoHangar
                : EstadoOperacaoPortaAvioesV2.ProntoNoConves,
            "sincronizado pelo menu do porta-aviões");
        return aeronave;
    }

    public bool TrySolicitarPouso(ControleAviao controle)
    {
        if (!usarSistemaOperacoesV2 || layout == null || layout.pontosPouso.Count < 5) return false;
        var a = Registrar(controle); if (a == null || operacoes.ContainsKey(a.Registro.id)) return false;
        if (a.Registro.estado != EstadoOperacaoPortaAvioesV2.EmVoo
            && a.Registro.estado != EstadoOperacaoPortaAvioesV2.EmMissao
            && a.Registro.estado != EstadoOperacaoPortaAvioesV2.SubidaInicial) return false;
        if (a.Registro.estado != EstadoOperacaoPortaAvioesV2.EmVoo)
            a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.EmVoo, "aeronave detectada em voo pelo radar");
        VagaPortaAvioesV2 vaga = ObterVagaLivre(layout.vagasConves, a);
        if (vaga == null) { a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.CircuitoDeEspera, "sem vaga externa"); return false; }
        if (!Assumir(a, EstadoOperacaoPortaAvioesV2.SolicitandoPouso)) return false;
        if (controle != null)
        {
            controle.aeroportoOrigem = legadoSuspenso;
            controle.vagaRetorno = vaga.transform;
            controle.DefinirEstado(ControleAviao.EstadoAviao.Pousando);
            controle.estaEmModoVooFisico = false;
        }
        a.Registro.vagaReservada = vaga.id; a.Registro.portaAvioesAtual = name;
        a.Registro.operacaoAtual = "Pouso"; Iniciar(a, Pousar(a, vaga)); return true;
    }

    public bool TrySolicitarReabastecimento(ControleAviao controle) { var a = Registrar(controle); if (!usarSistemaOperacoesV2 || a == null || (a.Registro.estado != EstadoOperacaoPortaAvioesV2.EstacionadoNoConves && a.Registro.estado != EstadoOperacaoPortaAvioesV2.ProntoNoConves && a.Registro.estado != EstadoOperacaoPortaAvioesV2.ArmazenadoNoHangar)) return false; if (!Assumir(a, EstadoOperacaoPortaAvioesV2.Reabastecendo)) return false; Iniciar(a, Reabastecer(a)); return true; }
    public bool TryEnviarParaHangarInterno(ControleAviao controle) { var a = Registrar(controle); if (!usarSistemaOperacoesV2 || a == null || a.Registro.estado != EstadoOperacaoPortaAvioesV2.ProntoNoConves) return false; var vaga = ObterVagaLivre(layout.vagasHangar, a); if (vaga == null) { a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.SemVaga, "sem vaga interna compatível"); return false; } if (!Assumir(a, EstadoOperacaoPortaAvioesV2.AguardandoElevador)) return false; a.Registro.vagaReservada = vaga.id; Iniciar(a, EnviarParaHangar(a, vaga)); return true; }
    public bool TryTrazerParaConves(ControleAviao controle) { var a = Registrar(controle); if (!usarSistemaOperacoesV2 || a == null || a.Registro.estado != EstadoOperacaoPortaAvioesV2.ArmazenadoNoHangar) return false; var vaga = ObterVagaLivre(layout.vagasConves, a); if (vaga == null) { a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.SemVaga, "sem vaga externa compatível"); return false; } if (!Assumir(a, EstadoOperacaoPortaAvioesV2.PreparandoSaidaDoHangar)) return false; a.Registro.vagaReservada = vaga.id; Iniciar(a, TrazerParaConves(a, vaga)); return true; }
    public bool TrySolicitarDecolagem(ControleAviao controle, Vector3 destino, bool patrulha = false) { var a = Registrar(controle); if (!usarSistemaOperacoesV2 || a == null || (a.Registro.estado != EstadoOperacaoPortaAvioesV2.ProntoNoConves && a.Registro.estado != EstadoOperacaoPortaAvioesV2.EstacionadoNoConves)) return false; Transform catapulta = ObterCatapultaLivre(a.Registro.id); if (catapulta == null) { a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.SemCatapulta, "todas as catapultas estão ocupadas"); return false; } if (!Assumir(a, EstadoOperacaoPortaAvioesV2.AguardandoCatapulta)) return false; catapultasReservadas[catapulta] = a.Registro.id; a.Registro.catapultaReservada = catapulta.name; a.Registro.missaoAtual = patrulha ? "Patrulha" : "Missão"; Iniciar(a, Decolar(a, destino, catapulta)); return true; }
    public bool TrySolicitarPatrulha(ControleAviao controle, Vector3 destino) { return TrySolicitarDecolagem(controle, destino, true); }
    public bool TryCancelarOperacao(ControleAviao controle) { var a = Registrar(controle); if (a == null || !operacoes.TryGetValue(a.Registro.id, out var rotina)) return false; StopCoroutine(rotina); operacoes.Remove(a.Registro.id); LiberarRecursos(a); a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.OperacaoCancelada, "cancelado pelo menu"); a.LiberarAutoridade(autoridade); return true; }

    private bool PodeIniciar(AeronaveEmbarcadaV2 a, EstadoOperacaoPortaAvioesV2 esperado) { return a != null && a.Registro.estado == esperado && !operacoes.ContainsKey(a.Registro.id); }
    private bool Assumir(AeronaveEmbarcadaV2 a, EstadoOperacaoPortaAvioesV2 estado) { if (!a.TentarAssumirAutoridade(autoridade, out _)) return false; return a.TentarTransicionar(estado, Time.time); }
    private void Iniciar(AeronaveEmbarcadaV2 a, IEnumerator rotina) { operacoes[a.Registro.id] = StartCoroutine(Executar(a, rotina)); }
    private IEnumerator Executar(AeronaveEmbarcadaV2 a, IEnumerator rotina) { yield return rotina; if (a != null && operacoes.ContainsKey(a.Registro.id)) { operacoes.Remove(a.Registro.id); a.LiberarAutoridade(autoridade); } }
    private void SuspenderLegado(AeronaveEmbarcadaV2 a) { var c = a.GetComponent<ControleAviao>(); if (c != null) { c.StopAllCoroutines(); c.enabled = false; } }
    private void LiberarLegado(ControleAviao c) { if (c != null) c.enabled = true; }
    private VagaPortaAvioesV2 ObterVagaLivre(List<VagaPortaAvioesV2> vagas, AeronaveEmbarcadaV2 aeronave) { if (vagas == null || aeronave == null) return null; PrepararClassificacao(aeronave); foreach (var v in vagas) if (v != null && VagaAceitaAeronave(v, aeronave) && v.Reservar(aeronave.Registro.id)) return v; return null; }
    private bool VagaAceitaAeronave(VagaPortaAvioesV2 vaga, AeronaveEmbarcadaV2 aeronave) { TipoAeronavePortaAvioesV2 tipo = aeronave.Registro.tipo; bool tipoAceito = vaga.tipoPermitido == TipoAeronavePortaAvioesV2.Qualquer || tipo == vaga.tipoPermitido; return tipoAceito && vaga.tamanhoMaximo + .01f >= TamanhoAeronave(aeronave); }
    private void PrepararClassificacao(AeronaveEmbarcadaV2 aeronave) { if (aeronave != null && aeronave.Registro.tipo == TipoAeronavePortaAvioesV2.Qualquer) aeronave.Registro.tipo = InferirTipo(aeronave); }
    private TipoAeronavePortaAvioesV2 InferirTipo(AeronaveEmbarcadaV2 aeronave) { if (aeronave.GetComponent<ControleAviaoCaca>() != null) return TipoAeronavePortaAvioesV2.Caca; if (aeronave.GetComponent<C700TransporteAereo>() != null || aeronave.GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>() != null || aeronave.GetComponent<AviaoBombardeiro>() != null) return TipoAeronavePortaAvioesV2.Transporte; return TipoAeronavePortaAvioesV2.Caca; }
    private float TamanhoAeronave(AeronaveEmbarcadaV2 aeronave) { return aeronave == null || aeronave.Registro.tipo == TipoAeronavePortaAvioesV2.Caca ? 8f : 18f; }
    private Transform ObterCatapultaLivre(string aeronaveId) { if (layout == null || layout.catapultasLista == null) return null; foreach (var cat in layout.catapultasLista) if (cat != null && (!catapultasReservadas.TryGetValue(cat, out var ocupante) || ocupante == aeronaveId)) return cat; return null; }
    private Transform ObterAcessoVaga(VagaPortaAvioesV2 vaga) { if (vaga == null || layout == null || layout.taxi == null) return null; return layout.taxi.Find(vaga.transform.localPosition.x < 0f ? "Acesso_Vagas_Esquerda" : "Acesso_Vagas_Direita"); }
    private Transform ObterCruzamentoCatapulta(VagaPortaAvioesV2 vaga) { if (vaga == null || layout == null || layout.taxi == null) return null; return layout.taxi.Find(vaga.transform.localPosition.x < 0f ? "Cruzamento_Esquerda" : "Cruzamento_Direita"); }
    private Transform EntradaDaVaga(VagaPortaAvioesV2 vaga) { return vaga == null ? null : (vaga.transform.Find("Entrada") ?? vaga.transform); }
    private VagaPortaAvioesV2 LocalizarVaga(List<VagaPortaAvioesV2> vagas, string id) { if (vagas != null) foreach (var v in vagas) if (v != null && v.id == id) return v; return null; }
    private void LiberarRecursos(AeronaveEmbarcadaV2 a) { if (layout == null || a == null) return; foreach (var v in layout.vagasConves) if (v != null) v.Liberar(a.Registro.id); foreach (var v in layout.vagasHangar) if (v != null) v.Liberar(a.Registro.id); Transform catapulta = null; foreach (var item in catapultasReservadas) if (item.Value == a.Registro.id) { catapulta = item.Key; break; } if (catapulta != null) catapultasReservadas.Remove(catapulta); a.Registro.vagaReservada = string.Empty; a.Registro.vagaOcupada = string.Empty; a.Registro.catapultaReservada = string.Empty; }
    private IEnumerator Mover(AeronaveEmbarcadaV2 a, Transform alvo, float velocidade, bool parentarAoFinal = false, float alturaLocalFinal = 0f)
    {
        if (alvo == null) yield break; float inicio = Time.time;
        while (a != null && Time.time - inicio < timeoutPorEstado) { Vector3 destino = alvo.position; Vector3 delta = destino - a.transform.position; if (delta.sqrMagnitude <= .25f) break; a.transform.position = Vector3.MoveTowards(a.transform.position, destino, Mathf.Max(.01f, velocidade) * Time.deltaTime); if (delta.sqrMagnitude > .01f) a.transform.rotation = Quaternion.RotateTowards(a.transform.rotation, Quaternion.LookRotation(delta.normalized), 180f * Time.deltaTime); yield return null; }
        if (parentarAoFinal && a != null) { a.transform.SetParent(alvo, true); a.transform.localPosition = new Vector3(0f, alturaLocalFinal, 0f); a.transform.localRotation = Quaternion.identity; }
    }
    private IEnumerator MoverParaPonto(AeronaveEmbarcadaV2 a, Vector3 destino, float velocidade)
    {
        float inicio = Time.time;
        while (a != null && Time.time - inicio < timeoutPorEstado && (a.transform.position - destino).sqrMagnitude > .25f)
        {
            Vector3 delta = destino - a.transform.position;
            a.transform.position = Vector3.MoveTowards(a.transform.position, destino, Mathf.Max(.01f, velocidade) * Time.deltaTime);
            if (delta.sqrMagnitude > .01f) a.transform.rotation = Quaternion.RotateTowards(a.transform.rotation, Quaternion.LookRotation(delta.normalized), 180f * Time.deltaTime);
            yield return null;
        }
    }
    private IEnumerator Pausa(float segundos) { yield return new WaitForSeconds(segundos); }
    private float AlturaEstacionamento(AeronaveEmbarcadaV2 aeronave)
    {
        ControleAviao controle = aeronave != null ? aeronave.GetComponent<ControleAviao>() : null;
        return controle == null ? 0f : Mathf.Min(0.25f, controle.ObterAlturaEstacionamento() * 0.1f);
    }

    private void MarcarAeronaveEstacionada(AeronaveEmbarcadaV2 aeronave)
    {
        ControleAviao controle = aeronave != null ? aeronave.GetComponent<ControleAviao>() : null;
        if (controle == null) return;
        controle.aeroportoOrigem = legadoSuspenso;
        controle.vagaRetorno = !string.IsNullOrEmpty(aeronave.Registro.vagaOcupada)
            ? LocalizarVaga(layout.vagasConves, aeronave.Registro.vagaOcupada)?.transform
            : null;
        controle.estaEmModoVooFisico = false;
        controle.DefinirEstado(ControleAviao.EstadoAviao.ProntoNoPatio);
    }

    private IEnumerator Pousar(AeronaveEmbarcadaV2 a, VagaPortaAvioesV2 vaga)
    {
        SuspenderLegado(a); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.AguardandoAutorizacao, Time.time); if (layout.pontosPouso.Count > 0) yield return Mover(a, layout.pontosPouso[0], velocidadeAproximacao); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.AproximacaoLonga, Time.time);
        if (layout.pontosPouso.Count > 1) { a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.AproximacaoIntermediaria, Time.time); yield return Mover(a, layout.pontosPouso[1], velocidadeAproximacao); }
        if (layout.pontosPouso.Count > 2) { a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.AproximacaoFinal, Time.time); yield return Mover(a, layout.pontosPouso[2], velocidadeAproximacao); }
        for (int i = 3; i < Mathf.Min(4, layout.pontosPouso.Count); i++) yield return Mover(a, layout.pontosPouso[i], velocidadeAproximacao);
        a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.ToqueNoConves, Time.time); yield return Mover(a, layout.pontosPouso[Mathf.Min(4, layout.pontosPouso.Count - 1)], velocidadeTaxi); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.FrenagemOuCaboDeRetencao, Time.time); yield return Pausa(.35f); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.TaxiandoParaSaida, Time.time); for (int i = 5; i < layout.pontosPouso.Count; i++) yield return Mover(a, layout.pontosPouso[i], velocidadeTaxi);
        Transform acesso = ObterAcessoVaga(vaga); if (acesso != null) yield return Mover(a, acesso, velocidadeTaxi); Transform entrada = EntradaDaVaga(vaga); if (entrada != null) yield return Mover(a, entrada, velocidadeTaxi); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.TaxiandoParaVaga, Time.time); yield return Mover(a, vaga.transform, velocidadeTaxi, true, AlturaEstacionamento(a)); vaga.Ocupar(a.Registro.id); a.Registro.vagaOcupada = vaga.id; a.Registro.vagaReservada = string.Empty; a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.EstacionadoNoConves, Time.time); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.ProntoNoConves, Time.time); MarcarAeronaveEstacionada(a); OperacaoConcluida?.Invoke(a, a.Registro.estado);
    }
    private IEnumerator Reabastecer(AeronaveEmbarcadaV2 a) { bool estavaNoHangar = a.Registro.estado == EstadoOperacaoPortaAvioesV2.Reabastecendo && !string.IsNullOrEmpty(a.Registro.vagaOcupada) && LocalizarVaga(layout.vagasHangar, a.Registro.vagaOcupada) != null; a.Registro.operacaoAtual = "Reabastecimento"; var c = a.GetComponent<CombustivelUnidade>(); if (c == null) c = CombustivelUnidade.Garantir(a.gameObject, false); float inicio = Time.time; while (c != null && c.CombustivelAtual < c.Capacidade && Time.time - inicio < timeoutPorEstado) { c.Abastecer(velocidadeReabastecimento * Time.deltaTime); a.Registro.combustivel = c.CombustivelAtual; yield return null; } a.Registro.operacaoAtual = string.Empty; a.TentarTransicionar(estavaNoHangar ? EstadoOperacaoPortaAvioesV2.ArmazenadoNoHangar : EstadoOperacaoPortaAvioesV2.ProntoNoConves, Time.time); OperacaoConcluida?.Invoke(a, a.Registro.estado); }
    private IEnumerator EnviarParaHangar(AeronaveEmbarcadaV2 a, VagaPortaAvioesV2 vaga)
    {
        if (layout.elevadoresLista.Count == 0) { a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.SemElevador, "elevador ausente"); yield break; } Transform elevador = layout.elevadoresLista[0]; Transform baixo = elevador.Find("Posicao_Baixa") ?? elevador; Transform cima = elevador.Find("Posicao_Conves") ?? elevador; Transform plataforma = elevador.Find("Plataforma") ?? cima;
        a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.TaxiandoParaElevador, Time.time); yield return Mover(a, elevador.Find("Fila") ?? cima, velocidadeTaxi); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.AlinhandoNoElevador, Time.time); yield return Mover(a, plataforma, velocidadeTaxi, true); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.ElevadorDescendo, Time.time); var elevadorV2 = elevador.GetComponent<ElevadorPortaAvioesV2>(); if (elevadorV2 != null && elevadorV2.Configurado) yield return elevadorV2.MoverPara(true); else yield return Mover(a, baixo, 3f); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.EntrandoNoHangar, Time.time); var vagaExterna = LocalizarVaga(layout.vagasConves, a.Registro.vagaOcupada); if (vagaExterna != null) vagaExterna.Liberar(a.Registro.id); if (!interiorHangarModelado) a.gameObject.SetActive(false); a.transform.SetParent(vaga.transform, true); vaga.Ocupar(a.Registro.id); a.Registro.vagaOcupada = vaga.id; a.Registro.vagaReservada = string.Empty; a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.ArmazenadoNoHangar, Time.time); OperacaoConcluida?.Invoke(a, a.Registro.estado);
    }
    private IEnumerator TrazerParaConves(AeronaveEmbarcadaV2 a, VagaPortaAvioesV2 vaga)
    {
        var antiga = LocalizarVaga(layout.vagasHangar, a.Registro.vagaOcupada); if (antiga != null) antiga.Liberar(a.Registro.id); if (!interiorHangarModelado) a.gameObject.SetActive(true); Transform elevador = layout.elevadoresLista[0]; Transform baixo = elevador.Find("Posicao_Baixa") ?? elevador; Transform cima = elevador.Find("Posicao_Conves") ?? elevador; var elevadorV2 = elevador.GetComponent<ElevadorPortaAvioesV2>(); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.ElevadorSubindo, Time.time); a.transform.SetParent(elevadorV2 != null && elevadorV2.plataforma != null ? elevadorV2.plataforma : baixo, true); if (elevadorV2 != null && elevadorV2.Configurado) yield return elevadorV2.MoverPara(false); else yield return Mover(a, cima, 3f); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.SaindoDoElevador, Time.time); Transform cruzamentoLadoDireito = layout.taxi != null ? layout.taxi.Find("Cruzamento_Direita") : null; if (cruzamentoLadoDireito != null) yield return Mover(a, cruzamentoLadoDireito, velocidadeTaxi); Transform cruzamento = ObterCruzamentoCatapulta(vaga); if (cruzamento != null && cruzamento != cruzamentoLadoDireito) yield return Mover(a, cruzamento, velocidadeTaxi); Transform acesso = ObterAcessoVaga(vaga); if (acesso != null) yield return Mover(a, acesso, velocidadeTaxi); Transform entrada = EntradaDaVaga(vaga); if (entrada != null) yield return Mover(a, entrada, velocidadeTaxi); yield return Mover(a, vaga.transform, velocidadeTaxi, true, AlturaEstacionamento(a)); vaga.Ocupar(a.Registro.id); a.Registro.vagaOcupada = vaga.id; a.Registro.vagaReservada = string.Empty; a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.TaxiandoParaVaga, Time.time); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.EstacionadoNoConves, Time.time); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.ProntoNoConves, Time.time); MarcarAeronaveEstacionada(a); OperacaoConcluida?.Invoke(a, a.Registro.estado);
    }
    private IEnumerator Decolar(AeronaveEmbarcadaV2 a, Vector3 destino, Transform catapulta)
    {
        var c = a.GetComponent<ControleAviao>(); if (c != null) c.StopAllCoroutines(); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.TaxiandoParaCatapulta, Time.time); Transform cat = catapulta; if (cat == null) { a.ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2.SemCatapulta, "catapulta ausente"); yield break; }
        Transform fila = cat.Find("Fila") ?? cat; Transform inicio = cat.Find("Inicio") ?? (layout.decolagem != null ? layout.decolagem.Find("Alinhamento") : cat); Transform liberacao = cat.Find("Liberacao") ?? (layout.decolagem != null ? layout.decolagem.Find("Liberacao") : cat); Transform subida = cat.Find("Subida") ?? (layout.decolagem != null ? layout.decolagem.Find("Subida_Inicial") : cat);
        VagaPortaAvioesV2 vaga = LocalizarVaga(layout.vagasConves, a.Registro.vagaOcupada); Transform entrada = EntradaDaVaga(vaga); if (entrada != null) yield return Mover(a, entrada, velocidadeTaxi); Transform acesso = ObterAcessoVaga(vaga); if (acesso != null) yield return Mover(a, acesso, velocidadeTaxi); Transform cruzamento = ObterCruzamentoCatapulta(vaga); if (cruzamento != null) yield return Mover(a, cruzamento, velocidadeTaxi); if (vaga != null) { vaga.Liberar(a.Registro.id); a.Registro.vagaOcupada = string.Empty; }
        yield return Mover(a, fila, velocidadeTaxi); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.AlinhandoNaCatapulta, Time.time); yield return Mover(a, inicio, velocidadeTaxi); yield return Pausa(.25f); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.PreparandoDecolagem, Time.time); yield return Mover(a, liberacao, velocidadeTaxi); a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.Lancamento, Time.time); a.transform.SetParent(null, true); yield return Mover(a, subida, velocidadeAproximacao);
        // Saida_Voo é o primeiro ponto fora do convés. Ele precisa ser
        // alcançado antes dos pontos de voo; fazer o inverso faria o avião
        // voltar para perto do navio depois de já ter subido.
        Transform saidaVoo = layout.decolagem != null ? layout.decolagem.Find("Saida_Voo") : null;
        if (saidaVoo != null)
        {
            yield return Mover(a, saidaVoo, velocidadeAproximacao);
        }

        // Os pontos abaixo de Voo são somente a saída inicial do convés.
        // Ponto_Missao é um destino lógico da missão e não pode ser usado
        // como waypoint físico de decolagem, pois normalmente fica sobre o
        // centro do navio.
        Transform ultimoPontoVoo = null;
        if (layout.pontosVoo != null)
        {
            foreach (var ponto in layout.pontosVoo)
            {
                if (ponto == null || ponto.name.IndexOf("missao", StringComparison.OrdinalIgnoreCase) >= 0
                    || ponto.name.IndexOf("mission", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                yield return Mover(a, ponto, velocidadeAproximacao);
                ultimoPontoVoo = ponto;
            }
        }

        Vector3 saida = ultimoPontoVoo != null
            ? ultimoPontoVoo.position + ultimoPontoVoo.forward * 80f + Vector3.up * 20f
            : saidaVoo != null
                ? saidaVoo.position + saidaVoo.forward * 80f + Vector3.up * 20f
            : a.transform.position + (cat.forward.sqrMagnitude > .01f ? cat.forward.normalized : transform.forward) * 30f + Vector3.up * 8f;
        yield return MoverParaPonto(a, saida, velocidadeAproximacao);

        a.Registro.catapultaReservada = string.Empty;
        catapultasReservadas.Remove(cat);
        a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.SubidaInicial, Time.time);
        a.TentarTransicionar(EstadoOperacaoPortaAvioesV2.EmMissao, Time.time);

        // A partir daqui somente o ControleAviao volta a mover o objeto.
        // Não iniciar IniciarMissaoCompleta: isso abriria uma segunda
        // coroutine e faria o avião retornar aos Creates do navio.
        if (c != null)
        {
            c.AssumirVooAposDecolagem(destino);
            LiberarLegado(c);
        }

        a.LiberarAutoridade(autoridade);
        OperacaoConcluida?.Invoke(a, a.Registro.estado);
    }
}
