using System;
using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

public enum EstadoOrganizacaoRebelde
{
    INATIVA, PATRULHANDO, ALERTA, DEFENDENDO, MOBILIZANDO, REFORCANDO, CONTRA_ATACANDO, RECUANDO, DERROTADA
}

/// <summary>Controlador local da Organização Rebelde; não participa da estratégia mundial.</summary>
[DisallowMultipleComponent]
public sealed class RebeldeControlador : MonoBehaviour
{
    [Header("Facção")]
    [SerializeField] private string nomeOrganizacao = "Organização Rebelde";
    [SerializeField, Min(1)] private int identificacaoFacao;
    [SerializeField] private EstadoOrganizacaoRebelde estado = EstadoOrganizacaoRebelde.INATIVA;
    [SerializeField, Range(0, 5)] private int nivelAtividade;
    [Header("Áreas operacionais")]
    [SerializeField] private List<AreaOperacionalRebelde> areasOperacionais = new List<AreaOperacionalRebelde>();
    [SerializeField] private List<ControleAviao> aeronavesRebeldes = new List<ControleAviao>();
    [SerializeField] private List<Helicoptero> helicopterosRebeldes = new List<Helicoptero>();
    [SerializeField] private List<ControleNavioRealista> naviosRebeldes = new List<ControleNavioRealista>();
    [Header("Unidades iniciais — não recriar após perdas")]
    [SerializeField] private GameObject prefabCacaF5;
    [SerializeField] private GameObject prefabHelicopteroRebelde;
    [SerializeField] private GameObject prefabNavioArmado;
    [SerializeField, Range(0, 2)] private int quantidadeInicialNavios = 2;
    [Header("Reservas limitadas")]
    [SerializeField] private List<GameObject> pontosCriacaoSoldado = new List<GameObject>();
    [SerializeField] private List<GameObject> prefabsSoldado = new List<GameObject>();
    [SerializeField, Min(0)] private int reservasDeSoldados = 12;
    [SerializeField, Min(1)] private int tamanhoGrupoReforco = 3;
    [SerializeField, Min(0)] private int quantidadeMaximaSoldadosAtivos = 24;
    [SerializeField, Min(1)] private int quantidadeMaximaReforcos = 2;
    [SerializeField, Min(1f)] private float intervaloEntreReforcos = 90f;
    [SerializeField, Min(1f)] private float intervaloDecisaoLocal = 3f;
    [SerializeField, Min(1f)] private float tempoCombateParaReforco = 300f;
    [SerializeField, Min(0)] private int quantidadeMaximaAereaAtiva = 2;
    [SerializeField, Min(0)] private int quantidadeMaximaNavalAtiva = 2;
    [Header("Instalações")]
    [SerializeField] private List<GameObject> instalacoesRebeldes = new List<GameObject>();
    [SerializeField] private GameObject basePrincipal;
    [SerializeField] private GameObject baseAliada;

    [NonSerialized] private float proximaDecisao;
    [NonSerialized] private float combateAtivoAcumulado;
    [NonSerialized] private float proximoReforco;
    [NonSerialized] private int reforcosSolicitados;
    [NonSerialized] private readonly Dictionary<int, IdentidadeUnidade> contatosAtivos = new Dictionary<int, IdentidadeUnidade>();
    [NonSerialized] private Coroutine rotinaAtivacao;
    [NonSerialized] private readonly List<bool> aeronavesIndisponiveis = new List<bool>();
    [NonSerialized] private readonly List<bool> helicopterosIndisponiveis = new List<bool>();
    [NonSerialized] private readonly List<bool> naviosIndisponiveis = new List<bool>();

    public string NomeOrganizacao => nomeOrganizacao;
    public int IdentificacaoFacao => identificacaoFacao;
    public EstadoOrganizacaoRebelde Estado => estado;
    public int NivelAtividade => nivelAtividade;
    public int ReservasDeSoldados => reservasDeSoldados;
    public bool Ativo => estado != EstadoOrganizacaoRebelde.INATIVA && estado != EstadoOrganizacaoRebelde.DERROTADA;
    public float CombateAtivoAcumulado => combateAtivoAcumulado;
    public int ReforcosSolicitados => reforcosSolicitados;

    public SaveRebeldeData CapturarEstadoSalvavel()
    {
        return new SaveRebeldeData
        {
            identificacaoFacao = identificacaoFacao,
            estado = (int)estado,
            nivelAtividade = nivelAtividade,
            reservasDeSoldados = reservasDeSoldados,
            reforcosSolicitados = reforcosSolicitados,
            combateAtivoAcumulado = combateAtivoAcumulado,
            aeronavesIndisponiveis = new List<bool>(aeronavesIndisponiveis),
            helicopterosIndisponiveis = new List<bool>(helicopterosIndisponiveis),
            naviosIndisponiveis = new List<bool>(naviosIndisponiveis)
        };
    }

    private void Awake()
    {
        GarantirIdentificacaoExclusiva();
        VincularIdentidadesConfiguradas();
    }

    private void Start()
    {
        if (identificacaoFacao <= 0) GarantirIdentificacaoExclusiva();
        if (identificacaoFacao > 0)
        {
            ReconstruirUnidadesDaFacao();
            VincularIdentidadesConfiguradas();
        }
    }

    private void OnEnable() => MissaoOperacaoSinalPerdido.OnOrganizacaoAtivada += Ativar;
    private void OnDisable() => MissaoOperacaoSinalPerdido.OnOrganizacaoAtivada -= Ativar;

    private void Update()
    {
        if (!Ativo || Time.time < proximaDecisao) return;
        proximaDecisao = Time.time + Mathf.Clamp(intervaloDecisaoLocal, 1f, 15f);
        ProcessarDecisaoLocal();
    }

    private void GarantirIdentificacaoExclusiva()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return;
        bool idDeOutraFacao = false;
        IReadOnlyList<FacaoNaoEstatalGoverno> registros = governo.FacoesNaoEstatais;
        for (int i = 0; i < registros.Count; i++)
            if (registros[i] != null && registros[i].teamId == identificacaoFacao
                && !string.Equals(registros[i].nome, nomeOrganizacao, StringComparison.Ordinal)) idDeOutraFacao = true;
        if (identificacaoFacao <= 0 || governo.ObterPais(identificacaoFacao) != null || idDeOutraFacao)
            identificacaoFacao = AlocarIdentificacao(governo);
        governo.RegistrarFacaoNaoEstatal(identificacaoFacao, nomeOrganizacao);
    }

    private int AlocarIdentificacao(SistemaGovernoMundial governo)
    {
        int maior = 0;
        IReadOnlyList<DadosPaisGoverno> paises = governo.Paises;
        for (int i = 0; i < paises.Count; i++) if (paises[i] != null) maior = Mathf.Max(maior, paises[i].teamId);
        IdentidadeUnidade[] identidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < identidades.Length; i++) if (identidades[i] != null) maior = Mathf.Max(maior, identidades[i].teamID);
        return Mathf.Max(2, maior + 1);
    }

    private void VincularIdentidadesConfiguradas()
    {
        if (identificacaoFacao <= 0) return;
        for (int i = 0; i < aeronavesRebeldes.Count; i++) Vincular(aeronavesRebeldes[i] != null ? aeronavesRebeldes[i].gameObject : null, TipoUnidade.Aereo);
        for (int i = 0; i < helicopterosRebeldes.Count; i++) Vincular(helicopterosRebeldes[i] != null ? helicopterosRebeldes[i].gameObject : null, TipoUnidade.Aereo);
        for (int i = 0; i < naviosRebeldes.Count; i++) Vincular(naviosRebeldes[i] != null ? naviosRebeldes[i].gameObject : null, TipoUnidade.Naval);
        for (int i = 0; i < instalacoesRebeldes.Count; i++) Vincular(instalacoesRebeldes[i], TipoUnidade.Estrutura);
        Vincular(basePrincipal, TipoUnidade.Estrutura);
        Vincular(baseAliada, TipoUnidade.Estrutura);
        for (int i = 0; i < areasOperacionais.Count; i++)
            if (areasOperacionais[i] != null) Vincular(areasOperacionais[i].BaseResponsavel, TipoUnidade.Estrutura);
        RegistrarUnidadesComIndice(aeronavesRebeldes, TipoUnidadeRebelde.Aeronave, aeronavesIndisponiveis);
        RegistrarUnidadesComIndice(helicopterosRebeldes, TipoUnidadeRebelde.Helicoptero, helicopterosIndisponiveis);
        RegistrarUnidadesComIndice(naviosRebeldes, TipoUnidadeRebelde.Navio, naviosIndisponiveis);
    }

    private void RegistrarUnidadesComIndice<T>(List<T> unidades, TipoUnidadeRebelde categoria, List<bool> perdas) where T : Component
    {
        while (perdas.Count < unidades.Count) perdas.Add(false);
        for (int i = 0; i < unidades.Count; i++)
        {
            T unidade = unidades[i];
            if (unidade == null) continue;
            if (perdas[i]) { Destroy(unidade.gameObject); continue; }
            UnidadeControladaRebelde marcador = unidade.GetComponent<UnidadeControladaRebelde>();
            if (marcador == null) marcador = unidade.gameObject.AddComponent<UnidadeControladaRebelde>();
            marcador.Configurar(this, categoria, i);
        }
    }

    public void RegistrarPerda(TipoUnidadeRebelde categoria, int indice)
    {
        if (indice < 0) return;
        List<bool> lista = categoria == TipoUnidadeRebelde.Aeronave ? aeronavesIndisponiveis
            : categoria == TipoUnidadeRebelde.Helicoptero ? helicopterosIndisponiveis : naviosIndisponiveis;
        while (lista.Count <= indice) lista.Add(false);
        lista[indice] = true;
    }

    private void Vincular(GameObject objeto, TipoUnidade tipo)
    {
        if (objeto == null) return;
        IdentidadeUnidade identidade = objeto.GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = objeto.AddComponent<IdentidadeUnidade>();
        identidade.teamID = identificacaoFacao;
        identidade.nomeDoPais = nomeOrganizacao;
        identidade.tipoUnidade = tipo;
        IA_WorldState.NotifyEntityChanged(identidade);
    }

    public void Ativar()
    {
        if (estado == EstadoOrganizacaoRebelde.DERROTADA || Ativo) return;
        GarantirIdentificacaoExclusiva();
        CriarUnidadesIniciaisDisponiveis();
        ReconstruirUnidadesDaFacao();
        VincularIdentidadesConfiguradas();
        if (rotinaAtivacao != null) StopCoroutine(rotinaAtivacao);
        rotinaAtivacao = StartCoroutine(AtivarPatrulhasAposIntervalo());
        SistemaGovernoMundial.Instancia?.RegistrarNoticia(nomeOrganizacao + " iniciou patrulhas na ilha neutra.");
    }

    private void CriarUnidadesIniciaisDisponiveis()
    {
        if (aeronavesRebeldes.Count == 0 && prefabCacaF5 != null)
            CriarAeronaveInicial(prefabCacaF5, TipoAreaOperacionalRebelde.Aerea, true);
        if (helicopterosRebeldes.Count == 0 && prefabHelicopteroRebelde != null)
            CriarHelicopteroInicial(prefabHelicopteroRebelde);
        if (naviosRebeldes.Count == 0 && prefabNavioArmado != null && quantidadeInicialNavios > 0)
            CriarNaviosIniciais(prefabNavioArmado, Mathf.Clamp(quantidadeInicialNavios, 0, 2));
    }

    private void CriarAeronaveInicial(GameObject prefab, TipoAreaOperacionalRebelde tipoArea, bool registrarNoAeroporto)
    {
        for (int i = 0; i < areasOperacionais.Count; i++)
        {
            AreaOperacionalRebelde area = areasOperacionais[i];
            if (area == null || area.Tipo != tipoArea) continue;
            Transform ponto = area.ObterPontoCriacaoValido();
            if (ponto == null) continue;
            GerenciadorAeroporto aeroporto = area.BaseResponsavel != null ? area.BaseResponsavel.GetComponent<GerenciadorAeroporto>() : null;
            if (registrarNoAeroporto && aeroporto == null) continue;
            GameObject objeto = Instantiate(prefab, ponto.position, ponto.rotation, area.BaseResponsavel != null ? area.BaseResponsavel.transform : null);
            ControleAviao aviao = objeto.GetComponent<ControleAviao>();
            if (aviao == null) { Destroy(objeto); continue; }
            Vincular(objeto, TipoUnidade.Aereo);
            if (registrarNoAeroporto && !aeroporto.RegistrarAeronaveRecebida(aviao)) { Destroy(objeto); continue; }
            if (!area.RegistrarUnidade(objeto)) { Destroy(objeto); continue; }
            aeronavesRebeldes.Add(aviao);
            return;
        }
    }

    private void CriarHelicopteroInicial(GameObject prefab)
    {
        for (int i = 0; i < areasOperacionais.Count; i++)
        {
            AreaOperacionalRebelde area = areasOperacionais[i];
            if (area == null || area.Tipo != TipoAreaOperacionalRebelde.Aerea) continue;
            Transform ponto = area.ObterPontoCriacaoValido();
            GerenciadorAeroporto aeroporto = area.BaseResponsavel != null ? area.BaseResponsavel.GetComponent<GerenciadorAeroporto>() : null;
            if (ponto == null || aeroporto == null) continue;
            GameObject objeto = Instantiate(prefab, ponto.position, ponto.rotation, area.BaseResponsavel.transform);
            Helicoptero heli = objeto.GetComponent<Helicoptero>();
            if (heli == null) { Destroy(objeto); continue; }
            Vincular(objeto, TipoUnidade.Aereo);
            if (!aeroporto.RegistrarHelicopteroRecebido(heli) || !area.RegistrarUnidade(objeto)) { Destroy(objeto); continue; }
            helicopterosRebeldes.Add(heli);
            return;
        }
    }

    private void CriarNaviosIniciais(GameObject prefab, int quantidade)
    {
        int criados = 0;
        for (int i = 0; i < areasOperacionais.Count && criados < quantidade; i++)
        {
            AreaOperacionalRebelde area = areasOperacionais[i];
            if (area == null || area.Tipo != TipoAreaOperacionalRebelde.Naval) continue;
            for (int p = 0; p < area.PontosCriacaoAutorizados.Count && criados < quantidade; p++)
            {
                Transform ponto = area.PontosCriacaoAutorizados[p];
                if (ponto == null || !area.Contem(ponto.position)) continue;
                bool ocupado = false;
                for (int n = 0; n < naviosRebeldes.Count; n++)
                    if (naviosRebeldes[n] != null && Vector3.SqrMagnitude(naviosRebeldes[n].transform.position - ponto.position) < 100f * 100f) { ocupado = true; break; }
                if (ocupado) continue;
                GameObject objeto = Instantiate(prefab, ponto.position, ponto.rotation);
                ControleNavioRealista navio = objeto.GetComponent<ControleNavioRealista>();
                if (navio == null) { Destroy(objeto); continue; }
                Vincular(objeto, TipoUnidade.Naval);
                if (!area.RegistrarUnidade(objeto)) { Destroy(objeto); continue; }
                naviosRebeldes.Add(navio);
                criados++;
            }
        }
    }

    private void ReconstruirUnidadesDaFacao()
    {
        if (identificacaoFacao <= 0) return;
        IdentidadeUnidade[] unidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < unidades.Length; i++)
        {
            IdentidadeUnidade identidade = unidades[i];
            if (identidade == null || identidade.teamID != identificacaoFacao) continue;
            ControleAviao aviao = identidade.GetComponent<ControleAviao>();
            Helicoptero heli = identidade.GetComponent<Helicoptero>();
            ControleNavioRealista navio = identidade.GetComponent<ControleNavioRealista>();
            if (aviao != null && !aeronavesRebeldes.Contains(aviao)) aeronavesRebeldes.Add(aviao);
            if (heli != null && !helicopterosRebeldes.Contains(heli)) helicopterosRebeldes.Add(heli);
            if (navio != null && !naviosRebeldes.Contains(navio)) naviosRebeldes.Add(navio);
        }
    }

    private IEnumerator AtivarPatrulhasAposIntervalo()
    {
        estado = EstadoOrganizacaoRebelde.PATRULHANDO;
        nivelAtividade = 1;
        int limiteAereo = Mathf.Max(0, quantidadeMaximaAereaAtiva);
        for (int i = 0; i < aeronavesRebeldes.Count && i < limiteAereo; i++)
        {
            ControleAviao aviao = aeronavesRebeldes[i];
            AreaOperacionalRebelde area = ObterAreaParaUnidade(TipoAreaOperacionalRebelde.Aerea, aviao);
            if (aviao != null && area != null && area.RotaValida() && RotaDentroDaArea(area, area.RotaPatrulha))
                aviao.ReceberOrdemPatrulha(ConverterRota(area));
            yield return new WaitForSeconds(0.25f);
        }
        for (int i = 0; i < helicopterosRebeldes.Count && limiteAereo > aeronavesRebeldes.Count + i; i++)
        {
            Helicoptero heli = helicopterosRebeldes[i];
            AreaOperacionalRebelde area = ObterAreaParaUnidade(TipoAreaOperacionalRebelde.Aerea, heli);
            if (heli != null && area != null && area.RotaValida() && RotaDentroDaArea(area, area.RotaPatrulha))
                heli.ReceberOrdemPatrulhaAeroporto(ConverterRota(area));
            yield return new WaitForSeconds(0.25f);
        }
        int limiteNaval = Mathf.Min(Mathf.Max(0, quantidadeMaximaNavalAtiva), 2);
        for (int i = 0; i < naviosRebeldes.Count && i < limiteNaval; i++)
        {
            ControleNavioRealista navio = naviosRebeldes[i];
            AreaOperacionalRebelde area = ObterAreaParaUnidade(TipoAreaOperacionalRebelde.Naval, navio);
            if (navio != null && area != null && area.RotaValida() && RotaDentroDaArea(area, area.RotaPatrulha))
                StartCoroutine(PatrulharNavio(navio, area));
            yield return new WaitForSeconds(0.25f);
        }
        rotinaAtivacao = null;
    }

    private void ProcessarDecisaoLocal()
    {
        RemoverContatosInvalidos();
        VerificarLimitesDasUnidades();
        bool baseOperacional = (basePrincipal != null && basePrincipal.activeInHierarchy) || (baseAliada != null && baseAliada.activeInHierarchy);
        if (!baseOperacional && !TemUnidadesVivas())
        {
            estado = EstadoOrganizacaoRebelde.DERROTADA;
            nivelAtividade = 0;
            return;
        }
        bool contatoHostil = PossuiContatoHostil();
        if (contatoHostil)
        {
            estado = EstadoOrganizacaoRebelde.ALERTA;
            nivelAtividade = Mathf.Max(nivelAtividade, 2);
            if (HaGuerraAtiva())
            {
                estado = EstadoOrganizacaoRebelde.DEFENDENDO;
                nivelAtividade = Mathf.Max(nivelAtividade, 3);
                combateAtivoAcumulado += intervaloDecisaoLocal;
            }
        }
        else if (estado != EstadoOrganizacaoRebelde.MOBILIZANDO && estado != EstadoOrganizacaoRebelde.REFORCANDO)
        {
            estado = EstadoOrganizacaoRebelde.PATRULHANDO;
            nivelAtividade = 1;
        }

        if (combateAtivoAcumulado >= tempoCombateParaReforco && reforcosSolicitados < quantidadeMaximaReforcos)
            TentarEnviarReforcos();
    }

    public void RegistrarContato(AreaOperacionalRebelde area, IdentidadeUnidade contato)
    {
        if (!Ativo || area == null || contato == null || contato.teamID <= 0 || contato.teamID == identificacaoFacao) return;
        contatosAtivos[contato.GetInstanceID()] = contato;
        AvaliarContato(contato);
    }

    public void RemoverContato(IdentidadeUnidade contato)
    {
        if (contato != null) contatosAtivos.Remove(contato.GetInstanceID());
    }

    private bool HaGuerraAtiva()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return false;
        IReadOnlyList<RelacaoPaisGoverno> relacoes = governo.Relacoes;
        for (int i = 0; i < relacoes.Count; i++) if (relacoes[i] != null && relacoes[i].guerraDeclarada && relacoes[i].Envolve(identificacaoFacao, relacoes[i].Outro(identificacaoFacao))) return true;
        return false;
    }

    private void AvaliarContato(IdentidadeUnidade contato)
    {
        if (contato == null) return;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        RelacaoPaisGoverno relacao = governo != null ? governo.ObterRelacao(identificacaoFacao, contato.teamID) : null;
        if (relacao != null && (relacao.guerraDeclarada || relacao.PosturaDe(identificacaoFacao) == PosturaRelacaoPais.Inimigo))
            nivelAtividade = Mathf.Max(nivelAtividade, 2);
    }

    private bool PossuiContatoHostil()
    {
        foreach (KeyValuePair<int, IdentidadeUnidade> contato in contatosAtivos)
        {
            if (contato.Value == null) continue;
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            RelacaoPaisGoverno relacao = governo != null ? governo.ObterRelacao(identificacaoFacao, contato.Value.teamID) : null;
            if (relacao != null && (relacao.guerraDeclarada || relacao.PosturaDe(identificacaoFacao) == PosturaRelacaoPais.Inimigo)) return true;
        }
        return false;
    }

    private IEnumerator PatrulharNavio(ControleNavioRealista navio, AreaOperacionalRebelde area)
    {
        while (Ativo && navio != null && area != null)
        {
            for (int i = 0; i < area.RotaPatrulha.Count; i++)
            {
                Transform ponto = area.RotaPatrulha[i];
                if (ponto == null || !area.Contem(ponto.position)) continue;
                navio.DefinirDestino(ponto.position);
                float limiteEspera = Time.time + 600f;
                while (navio != null && area != null && area.Contem(navio.transform.position)
                    && Vector3.SqrMagnitude(navio.transform.position - ponto.position) > 250f * 250f
                    && Time.time < limiteEspera)
                    yield return new WaitForSeconds(2f);
            }
            if (navio == null || area == null) yield break;
            if (area.PontoRetornoBase != null)
            {
                navio.DefinirDestino(area.PontoRetornoBase.position);
                float limiteEspera = Time.time + 600f;
                while (navio != null && Vector3.SqrMagnitude(navio.transform.position - area.PontoRetornoBase.position) > 250f * 250f
                    && Time.time < limiteEspera)
                    yield return new WaitForSeconds(2f);
            }
            yield return new WaitForSeconds(Mathf.Max(1f, area.TempoEntrePatrulhas));
        }
    }

    private void VerificarLimitesDasUnidades()
    {
        for (int i = 0; i < aeronavesRebeldes.Count; i++)
        {
            ControleAviao aviao = aeronavesRebeldes[i];
            if (aviao == null || aviao.estadoAtual != ControleAviao.EstadoAviao.EmMissao) continue;
            AreaOperacionalRebelde area = ObterArea(TipoAreaOperacionalRebelde.Aerea, aviao.transform.position);
            if (area == null && aviao.aeroportoOrigem != null) aviao.ComandoRetornarBase();
        }
        for (int i = 0; i < helicopterosRebeldes.Count; i++)
        {
            Helicoptero heli = helicopterosRebeldes[i];
            if (heli == null) continue;
            if (ObterArea(TipoAreaOperacionalRebelde.Aerea, heli.transform.position) == null) heli.RetornarParaVagaAeroporto();
        }
        for (int i = 0; i < naviosRebeldes.Count; i++)
        {
            ControleNavioRealista navio = naviosRebeldes[i];
            if (navio == null) continue;
            AreaOperacionalRebelde area = ObterArea(TipoAreaOperacionalRebelde.Naval, navio.transform.position);
            if (area == null && naviosRebeldes[i] != null)
            {
                AreaOperacionalRebelde areaNaval = ObterAreaDeUnidade(TipoAreaOperacionalRebelde.Naval, navio);
                if (areaNaval != null && areaNaval.PontoRetornoBase != null) navio.DefinirDestino(areaNaval.PontoRetornoBase.position);
            }
        }
    }

    private AreaOperacionalRebelde ObterAreaDeUnidade(TipoAreaOperacionalRebelde tipo, Component unidade)
    {
        for (int i = 0; i < areasOperacionais.Count; i++)
        {
            AreaOperacionalRebelde area = areasOperacionais[i];
            if (area == null || area.Tipo != tipo) continue;
            for (int u = 0; u < area.UnidadesVinculadas.Count; u++)
                if (area.UnidadesVinculadas[u] == unidade.gameObject) return area;
        }
        return null;
    }

    private void TentarEnviarReforcos()
    {
        bool baseDisponivel = (basePrincipal != null && basePrincipal.activeInHierarchy)
            || (baseAliada != null && baseAliada.activeInHierarchy);
        if (!baseDisponivel || reservasDeSoldados <= 0 || Time.time < proximoReforco || prefabsSoldado.Count == 0) return;
        int ativos = 0;
        for (int i = 0; i < instalacoesRebeldes.Count; i++)
            if (instalacoesRebeldes[i] != null) ativos += instalacoesRebeldes[i].GetComponentsInChildren<IdentidadeUnidade>(true).Length;
        int quantidade = Mathf.Min(tamanhoGrupoReforco, reservasDeSoldados, Mathf.Max(0, quantidadeMaximaSoldadosAtivos - ativos));
        if (quantidade <= 0) return;
        Transform ponto = null;
        for (int i = 0; i < pontosCriacaoSoldado.Count; i++)
            if (pontosCriacaoSoldado[i] != null && ObterArea(TipoAreaOperacionalRebelde.Terrestre, pontosCriacaoSoldado[i].transform.position) != null)
            { ponto = pontosCriacaoSoldado[i].transform; break; }
        if (ponto == null) return;
        GameObject prefab = prefabsSoldado[0];
        if (prefab == null) return;
        estado = EstadoOrganizacaoRebelde.REFORCANDO;
        nivelAtividade = 4;
        for (int i = 0; i < quantidade; i++)
        {
            GameObject soldado = Instantiate(prefab, ponto.position, ponto.rotation);
            Vincular(soldado, TipoUnidade.Infantaria);
            reservasDeSoldados--;
        }
        reforcosSolicitados++;
        proximoReforco = Time.time + Mathf.Max(1f, intervaloEntreReforcos);
    }

    private bool TemUnidadesVivas() => aeronavesRebeldes.Exists(x => x != null) || helicopterosRebeldes.Exists(x => x != null) || naviosRebeldes.Exists(x => x != null);
    private void RemoverContatosInvalidos()
    {
        if (contatosAtivos.Count == 0) return;
        List<int> remover = null;
        foreach (KeyValuePair<int, IdentidadeUnidade> contato in contatosAtivos)
        {
            if (contato.Value != null) { AvaliarContato(contato.Value); continue; }
            if (remover == null) remover = new List<int>();
            remover.Add(contato.Key);
        }
        if (remover != null) for (int i = 0; i < remover.Count; i++) contatosAtivos.Remove(remover[i]);
    }
    private AreaOperacionalRebelde ObterArea(TipoAreaOperacionalRebelde tipo, Vector3 pos)
    {
        for (int i = 0; i < areasOperacionais.Count; i++)
            if (areasOperacionais[i] != null && areasOperacionais[i].Tipo == tipo && areasOperacionais[i].Contem(pos)) return areasOperacionais[i];
        return null;
    }

    private AreaOperacionalRebelde ObterAreaParaUnidade(TipoAreaOperacionalRebelde tipo, Component unidade)
    {
        if (unidade == null) return null;
        AreaOperacionalRebelde vinculada = ObterAreaDeUnidade(tipo, unidade);
        return vinculada != null ? vinculada : ObterArea(tipo, unidade.transform.position);
    }
    private static bool RotaDentroDaArea(AreaOperacionalRebelde area, IReadOnlyList<Transform> rota)
    {
        if (area == null || rota == null) return false;
        for (int i = 0; i < rota.Count; i++) if (rota[i] == null || !area.Contem(rota[i].position)) return false;
        return true;
    }
    private static List<Vector3> ConverterRota(AreaOperacionalRebelde area)
    {
        List<Vector3> pontos = new List<Vector3>();
        for (int i = 0; i < area.RotaPatrulha.Count; i++) if (area.RotaPatrulha[i] != null) pontos.Add(area.RotaPatrulha[i].position);
        return pontos;
    }

    public void RestaurarEstadoSalvo(int idFacao, int estadoSalvo, int nivel, int reservas, int reforcos, float combate)
    {
        identificacaoFacao = Mathf.Max(0, idFacao);
        estado = (EstadoOrganizacaoRebelde)Mathf.Clamp(estadoSalvo, 0, Enum.GetValues(typeof(EstadoOrganizacaoRebelde)).Length - 1);
        nivelAtividade = Mathf.Clamp(nivel, 0, 5);
        reservasDeSoldados = Mathf.Max(0, reservas);
        reforcosSolicitados = Mathf.Max(0, reforcos);
        combateAtivoAcumulado = Mathf.Max(0f, combate);
        if (identificacaoFacao > 0) SistemaGovernoMundial.Instancia?.RegistrarFacaoNaoEstatal(identificacaoFacao, nomeOrganizacao);
        VincularIdentidadesConfiguradas();
    }

    public void RestaurarEstadoSalvo(SaveRebeldeData salvo)
    {
        if (salvo == null) return;
        identificacaoFacao = Mathf.Max(0, salvo.identificacaoFacao);
        estado = (EstadoOrganizacaoRebelde)Mathf.Clamp(salvo.estado, 0, Enum.GetValues(typeof(EstadoOrganizacaoRebelde)).Length - 1);
        nivelAtividade = Mathf.Clamp(salvo.nivelAtividade, 0, 5);
        reservasDeSoldados = Mathf.Max(0, salvo.reservasDeSoldados);
        reforcosSolicitados = Mathf.Max(0, salvo.reforcosSolicitados);
        combateAtivoAcumulado = Mathf.Max(0f, salvo.combateAtivoAcumulado);
        CopiarPerdas(salvo.aeronavesIndisponiveis, aeronavesIndisponiveis);
        CopiarPerdas(salvo.helicopterosIndisponiveis, helicopterosIndisponiveis);
        CopiarPerdas(salvo.naviosIndisponiveis, naviosIndisponiveis);
        if (identificacaoFacao > 0) SistemaGovernoMundial.Instancia?.RegistrarFacaoNaoEstatal(identificacaoFacao, nomeOrganizacao);
        VincularIdentidadesConfiguradas();
    }

    private static void CopiarPerdas(List<bool> origem, List<bool> destino)
    {
        destino.Clear();
        if (origem != null) destino.AddRange(origem);
    }
}
