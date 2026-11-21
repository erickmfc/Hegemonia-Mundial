using System;
using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;
using UnityEngine.AI;

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
    [SerializeField, Min(0)] private int reservasAereasSegundaBase = 1;
    [SerializeField] private GameObject prefabReforcoAereoSegundaBase;
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
    [SerializeField] private GerenciadorAeroporto aeroportoClandestino;

    [NonSerialized] private float proximaDecisao;
    [NonSerialized] private float combateAtivoAcumulado;
    [NonSerialized] private float proximoReforco;
    [NonSerialized] private int reforcosSolicitados;
    [NonSerialized] private readonly Dictionary<int, IdentidadeUnidade> contatosAtivos = new Dictionary<int, IdentidadeUnidade>();
    [NonSerialized] private Coroutine rotinaAtivacao;
    [NonSerialized] private readonly List<bool> aeronavesIndisponiveis = new List<bool>();
    [NonSerialized] private readonly List<bool> helicopterosIndisponiveis = new List<bool>();
    [NonSerialized] private readonly List<bool> naviosIndisponiveis = new List<bool>();
    [NonSerialized] private readonly List<GameObject> soldadosReforcados = new List<GameObject>();
    [NonSerialized] private readonly HashSet<int> soldadosContados = new HashSet<int>();

    public string NomeOrganizacao => nomeOrganizacao;
    public int IdentificacaoFacao => identificacaoFacao;
    public EstadoOrganizacaoRebelde Estado => estado;
    public int NivelAtividade => nivelAtividade;
    public int ReservasDeSoldados => reservasDeSoldados;
    public int ReservasAereasSegundaBase => reservasAereasSegundaBase;
    public bool Ativo => estado != EstadoOrganizacaoRebelde.INATIVA && estado != EstadoOrganizacaoRebelde.DERROTADA;
    public float CombateAtivoAcumulado => combateAtivoAcumulado;
    public int ReforcosSolicitados => reforcosSolicitados;

    public bool TentarObterDestinoOperacao(out Vector3 destino)
    {
        for (int i = 0; i < areasOperacionais.Count; i++)
        {
            AreaOperacionalRebelde area = areasOperacionais[i];
            if (area == null) continue;
            if (area.PontoRetornoBase != null)
            {
                destino = area.PontoRetornoBase.position;
                return true;
            }
            if (area.BaseResponsavel != null)
            {
                destino = area.BaseResponsavel.transform.position;
                return true;
            }
        }

        if (basePrincipal != null)
        {
            destino = basePrincipal.transform.position;
            return true;
        }
        destino = Vector3.zero;
        return false;
    }

    public SaveRebeldeData CapturarEstadoSalvavel()
    {
        return new SaveRebeldeData
        {
            identificacaoFacao = identificacaoFacao,
            estado = (int)estado,
            nivelAtividade = nivelAtividade,
            reservasDeSoldados = reservasDeSoldados,
            reservasAereasSegundaBase = reservasAereasSegundaBase,
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
        IdentidadeUnidade[] identidades = objeto.GetComponentsInChildren<IdentidadeUnidade>(true);
        if (identidades.Length == 0)
        {
            identidades = new[] { objeto.AddComponent<IdentidadeUnidade>() };
        }
        for (int i = 0; i < identidades.Length; i++)
        {
            IdentidadeUnidade identidade = identidades[i];
            if (identidade == null) continue;
            identidade.teamID = identificacaoFacao;
            identidade.nomeDoPais = nomeOrganizacao;
            identidade.tipoUnidade = tipo;
            IA_WorldState.NotifyEntityChanged(identidade);
        }
    }

    public void Ativar()
    {
        if (estado == EstadoOrganizacaoRebelde.DERROTADA || Ativo) return;
        GarantirIdentificacaoExclusiva();
        CriarUnidadesIniciaisDisponiveis();
        ReconstruirUnidadesDaFacao();
        VincularIdentidadesConfiguradas();
        CriarInfantariaInicialDisponivel();
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

    private void CriarInfantariaInicialDisponivel()
    {
        // A facção precisa ter presença terrestre assim que a operação a ativa.
        // O caminho existente de reforços valida base, área e ponto de criação.
        for (int i = 0; i < soldadosReforcados.Count; i++)
            if (soldadosReforcados[i] != null) return;

        EnviarGrupoTerrestreDeBaseDisponivel();
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
                Vector3 posicaoNaval = ponto.position;
                if (RegistroSuperficieMapa.TryGetAltura(posicaoNaval, TipoSuperficieMapa.Agua, out float alturaAgua))
                    posicaoNaval.y = alturaAgua;
                else
                    posicaoNaval.y = 0f;
                bool ocupado = false;
                for (int n = 0; n < naviosRebeldes.Count; n++)
                    if (naviosRebeldes[n] != null && Vector3.SqrMagnitude(naviosRebeldes[n].transform.position - posicaoNaval) < 100f * 100f) { ocupado = true; break; }
                if (ocupado) continue;
                GameObject objeto = Instantiate(prefab, posicaoNaval, ponto.rotation);
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
            if (identidade.tipoUnidade == TipoUnidade.Infantaria && !soldadosReforcados.Contains(identidade.gameObject))
                soldadosReforcados.Add(identidade.gameObject);
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
        for (int i = 0; i < soldadosReforcados.Count; i++)
        {
            GameObject soldado = soldadosReforcados[i];
            if (soldado == null) continue;
            AreaOperacionalRebelde area = ObterAreaParaUnidade(TipoAreaOperacionalRebelde.Terrestre, soldado.transform);
            if (area != null && area.RotaValida() && RotaDentroDaArea(area, area.RotaPatrulha))
                StartCoroutine(PatrulharTerrestre(soldado, area));
            yield return new WaitForSeconds(0.15f);
        }
        rotinaAtivacao = null;
    }

    private void ProcessarDecisaoLocal()
    {
        soldadosReforcados.RemoveAll(x => x == null);
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
        AvaliarContato(area, contato);
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

    private void AvaliarContato(AreaOperacionalRebelde area, IdentidadeUnidade contato)
    {
        if (!RelacaoHostil(contato)) return;

        nivelAtividade = Mathf.Max(nivelAtividade, 2);
        AcionarDefesasDaArea(area, contato);
    }

    private bool RelacaoHostil(IdentidadeUnidade contato)
    {
        if (contato == null || contato.teamID <= 0 || contato.teamID == identificacaoFacao) return false;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        RelacaoPaisGoverno relacao = governo != null ? governo.ObterRelacao(identificacaoFacao, contato.teamID) : null;
        return relacao != null && (relacao.guerraDeclarada
            || relacao.PosturaDe(identificacaoFacao) == PosturaRelacaoPais.Inimigo);
    }

    private void AcionarDefesasDaArea(AreaOperacionalRebelde area, IdentidadeUnidade contato)
    {
        if (area == null || contato == null) return;

        if (area.Tipo == TipoAreaOperacionalRebelde.Aerea)
        {
            for (int i = 0; i < aeronavesRebeldes.Count; i++)
                AcionarUnidadeArmadaNaArea(aeronavesRebeldes[i] != null ? aeronavesRebeldes[i].gameObject : null, area, contato);
            for (int i = 0; i < helicopterosRebeldes.Count; i++)
                AcionarUnidadeArmadaNaArea(helicopterosRebeldes[i] != null ? helicopterosRebeldes[i].gameObject : null, area, contato);
        }
        else if (area.Tipo == TipoAreaOperacionalRebelde.Naval)
        {
            for (int i = 0; i < naviosRebeldes.Count; i++)
                AcionarUnidadeArmadaNaArea(naviosRebeldes[i] != null ? naviosRebeldes[i].gameObject : null, area, contato);
        }
        else if (area.Tipo == TipoAreaOperacionalRebelde.Terrestre)
        {
            for (int i = 0; i < soldadosReforcados.Count; i++)
                AcionarUnidadeArmadaNaArea(soldadosReforcados[i], area, contato);
        }
    }

    private static void AcionarUnidadeArmadaNaArea(GameObject unidade, AreaOperacionalRebelde area, IdentidadeUnidade contato)
    {
        if (unidade == null || area == null || contato == null || !area.Contem(unidade.transform.position)) return;
        ControleUnidade controle = unidade.GetComponent<ControleUnidade>()
            ?? unidade.GetComponentInChildren<ControleUnidade>(true);
        if (controle == null) return;

        controle.DefinirModoCombate(true);
        controle.DefinirAlvoPrioritario(contato.transform);
        if (area.Tipo == TipoAreaOperacionalRebelde.Terrestre)
        {
            NavMeshAgent agente = unidade.GetComponent<NavMeshAgent>() ?? unidade.GetComponentInChildren<NavMeshAgent>(true);
            if (agente != null && agente.isActiveAndEnabled && agente.isOnNavMesh
                && (unidade.transform.position - contato.transform.position).sqrMagnitude > 70f * 70f)
            {
                controle.MoverParaPonto(contato.transform.position, false);
            }
        }
    }

    private bool PossuiContatoHostil()
    {
        foreach (KeyValuePair<int, IdentidadeUnidade> contato in contatosAtivos)
        {
            if (contato.Value == null) continue;
            if (RelacaoHostil(contato.Value)) return true;
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
        for (int i = 0; i < soldadosReforcados.Count; i++)
        {
            GameObject soldado = soldadosReforcados[i];
            if (soldado == null) continue;
            if (ObterArea(TipoAreaOperacionalRebelde.Terrestre, soldado.transform.position) != null) continue;
            AreaOperacionalRebelde areaTerrestre = ObterAreaDeUnidade(TipoAreaOperacionalRebelde.Terrestre, soldado.transform);
            if (areaTerrestre == null || areaTerrestre.PontoRetornoBase == null) continue;
            NavMeshAgent agente = soldado.GetComponent<NavMeshAgent>();
            ControleUnidade controle = soldado.GetComponent<ControleUnidade>();
            if (agente != null && agente.isActiveAndEnabled && agente.isOnNavMesh && controle != null)
                controle.MoverParaPonto(areaTerrestre.PontoRetornoBase.position, false);
        }
    }

    private IEnumerator PatrulharTerrestre(GameObject soldado, AreaOperacionalRebelde area)
    {
        if (soldado == null || area == null) yield break;
        ControleUnidade controle = soldado.GetComponent<ControleUnidade>();
        NavMeshAgent agente = soldado.GetComponent<NavMeshAgent>();
        SistemaDeTiro tiro = soldado.GetComponent<SistemaDeTiro>();
        if (controle == null || agente == null) yield break;

        while (Ativo && soldado != null && area != null)
        {
            if (!agente.isActiveAndEnabled || !agente.isOnNavMesh)
            {
                yield return new WaitForSeconds(2f);
                continue;
            }
            if (tiro != null && (tiro.alvoAtual != null || tiro.alvoPrioritario != null))
            {
                yield return new WaitForSeconds(1.5f);
                continue;
            }
            for (int i = 0; i < area.RotaPatrulha.Count; i++)
            {
                if (!Ativo || soldado == null || area == null) yield break;
                if (!agente.isActiveAndEnabled || !agente.isOnNavMesh) break;
                if (tiro != null && (tiro.alvoAtual != null || tiro.alvoPrioritario != null)) break;
                Transform ponto = area.RotaPatrulha[i];
                if (ponto == null || !area.Contem(ponto.position)) continue;
                controle.MoverParaPonto(ponto.position, false);
                float limiteEspera = Time.time + 90f;
                while (Ativo && soldado != null && agente.isActiveAndEnabled && agente.isOnNavMesh
                    && (tiro == null || (tiro.alvoAtual == null && tiro.alvoPrioritario == null))
                    && Vector3.SqrMagnitude(soldado.transform.position - ponto.position) > 4.5f * 4.5f
                    && Time.time < limiteEspera)
                    yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(Mathf.Max(2f, area.TempoEntrePatrulhas));
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
        if (Time.time < proximoReforco || reforcosSolicitados >= quantidadeMaximaReforcos) return;
        bool enviado = EnviarGrupoTerrestreDeBaseDisponivel();
        bool apoioAereoEnviado = EnviarApoioAereoDaSegundaBase();
        if (!enviado && !apoioAereoEnviado) return;

        estado = EstadoOrganizacaoRebelde.REFORCANDO;
        nivelAtividade = 4;
        reforcosSolicitados++;
        proximoReforco = Time.time + Mathf.Max(1f, intervaloEntreReforcos);
        SistemaGovernoMundial.Instancia?.RegistrarNoticia(nomeOrganizacao
            + " enviou reforços limitados; " + reservasDeSoldados + " soldados permanecem na reserva.");
    }

    private bool EnviarGrupoTerrestreDeBaseDisponivel()
    {
        if (reservasDeSoldados <= 0 || prefabsSoldado == null || prefabsSoldado.Count == 0 || prefabsSoldado[0] == null)
            return false;
        AreaOperacionalRebelde areaEscolhida = null;
        Transform ponto = null;
        GameObject baseEscolhida = null;
        for (int b = 0; b < 2 && ponto == null; b++)
        {
            GameObject candidataBase = b == 0 ? basePrincipal : baseAliada;
            if (candidataBase == null || !candidataBase.activeInHierarchy) continue;
            for (int i = 0; i < areasOperacionais.Count && ponto == null; i++)
            {
                AreaOperacionalRebelde area = areasOperacionais[i];
                if (area == null || area.Tipo != TipoAreaOperacionalRebelde.Terrestre
                    || area.BaseResponsavel != candidataBase) continue;
                for (int p = 0; p < pontosCriacaoSoldado.Count; p++)
                {
                    GameObject candidato = pontosCriacaoSoldado[p];
                    if (candidato == null || !area.Contem(candidato.transform.position)) continue;
                    bool autorizado = false;
                    for (int a = 0; a < area.PontosCriacaoAutorizados.Count; a++)
                        if (area.PontosCriacaoAutorizados[a] == candidato.transform) { autorizado = true; break; }
                    if (!autorizado) continue;
                    baseEscolhida = candidataBase;
                    areaEscolhida = area;
                    ponto = candidato.transform;
                    break;
                }
            }
        }
        if (ponto == null || areaEscolhida == null || baseEscolhida == null) return false;

        soldadosContados.Clear();
        for (int i = 0; i < soldadosReforcados.Count; i++)
            if (soldadosReforcados[i] != null) soldadosContados.Add(soldadosReforcados[i].GetInstanceID());
        for (int i = 0; i < instalacoesRebeldes.Count; i++)
        {
            GameObject instalacao = instalacoesRebeldes[i];
            if (instalacao == null) continue;
            IdentidadeUnidade[] identidades = instalacao.GetComponentsInChildren<IdentidadeUnidade>(true);
            for (int u = 0; u < identidades.Length; u++)
                if (identidades[u] != null && identidades[u].tipoUnidade == TipoUnidade.Infantaria)
                    soldadosContados.Add(identidades[u].gameObject.GetInstanceID());
        }

        int quantidade = Mathf.Min(tamanhoGrupoReforco, reservasDeSoldados,
            Mathf.Max(0, quantidadeMaximaSoldadosAtivos - soldadosContados.Count));
        if (quantidade <= 0) return false;
        int criados = 0;
        for (int i = 0; i < quantidade; i++)
        {
            Vector3 posicaoSpawn = ResolverPosicaoTerrestreSegura(ponto.position, i, areaEscolhida);
            GameObject soldado = Instantiate(prefabsSoldado[0], posicaoSpawn, ponto.rotation);
            NavMeshAgent agente = soldado.GetComponent<NavMeshAgent>() ?? soldado.GetComponentInChildren<NavMeshAgent>(true);
            if (agente != null)
            {
                if (NavMesh.SamplePosition(posicaoSpawn, out NavMeshHit hitNav, 16f, NavMesh.AllAreas))
                {
                    soldado.transform.position = hitNav.position;
                    agente.enabled = true;
                    if (!agente.isOnNavMesh) agente.Warp(hitNav.position);
                }
                else
                {
                    agente.enabled = false;
                }
            }
            if (!areaEscolhida.RegistrarUnidade(soldado))
            {
                Destroy(soldado);
                continue;
            }
            Vincular(soldado, TipoUnidade.Infantaria);
            soldadosReforcados.Add(soldado);
            reservasDeSoldados--;
            criados++;
        }
        return criados > 0;
    }

    private static Vector3 ResolverPosicaoTerrestreSegura(Vector3 basePos, int indice, AreaOperacionalRebelde area)
    {
        float angulo = indice * 2.094395f;
        float raio = indice == 0 ? 0f : 2.6f;
        Vector3 candidata = basePos + new Vector3(Mathf.Cos(angulo) * raio, 0f, Mathf.Sin(angulo) * raio);
        if (RegistroSuperficieMapa.TryGetAltura(candidata, TipoSuperficieMapa.Chao, out float alturaTerreno))
        {
            candidata.y = alturaTerreno;
        }
        else if (Physics.Raycast(candidata + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            candidata.y = hit.point.y;
        }

        if (area != null && area.LimiteDaArea != null && !area.Contem(candidata))
        {
            candidata = basePos;
        }
        return candidata;
    }

    private bool EnviarApoioAereoDaSegundaBase()
    {
        if (reservasAereasSegundaBase <= 0 || baseAliada == null || !baseAliada.activeInHierarchy
            || prefabReforcoAereoSegundaBase == null) return false;
        GerenciadorAeroporto aeroporto = aeroportoClandestino != null
            ? aeroportoClandestino : baseAliada.GetComponent<GerenciadorAeroporto>();
        if (aeroporto == null || !aeroporto.isActiveAndEnabled || aeroporto.semEnergia) return false;

        int ativos = 0;
        for (int i = 0; i < aeronavesRebeldes.Count; i++) if (aeronavesRebeldes[i] != null) ativos++;
        for (int i = 0; i < helicopterosRebeldes.Count; i++) if (helicopterosRebeldes[i] != null) ativos++;
        if (ativos >= quantidadeMaximaAereaAtiva) return false;

        for (int i = 0; i < areasOperacionais.Count; i++)
        {
            AreaOperacionalRebelde area = areasOperacionais[i];
            if (area == null || area.Tipo != TipoAreaOperacionalRebelde.Aerea
                || area.BaseResponsavel != baseAliada || !area.RotaValida()) continue;
            Transform ponto = area.ObterPontoCriacaoValido();
            if (ponto == null) continue;
            GameObject objeto = Instantiate(prefabReforcoAereoSegundaBase, ponto.position, ponto.rotation, baseAliada.transform);
            ControleAviao aviao = objeto.GetComponent<ControleAviao>();
            if (aviao == null)
            {
                Helicoptero heli = objeto.GetComponent<Helicoptero>();
                if (heli == null || !aeroporto.RegistrarHelicopteroRecebido(heli) || !area.RegistrarUnidade(objeto))
                { Destroy(objeto); continue; }
                Vincular(objeto, TipoUnidade.Aereo);
                helicopterosRebeldes.Add(heli);
                RegistrarUnidadesComIndice(helicopterosRebeldes, TipoUnidadeRebelde.Helicoptero, helicopterosIndisponiveis);
                heli.ReceberOrdemPatrulhaAeroporto(ConverterRota(area));
            }
            else
            {
                if (!aeroporto.RegistrarAeronaveRecebida(aviao) || !area.RegistrarUnidade(objeto))
                { Destroy(objeto); continue; }
                Vincular(objeto, TipoUnidade.Aereo);
                aeronavesRebeldes.Add(aviao);
                RegistrarUnidadesComIndice(aeronavesRebeldes, TipoUnidadeRebelde.Aeronave, aeronavesIndisponiveis);
                aviao.ReceberOrdemPatrulha(ConverterRota(area));
            }
            reservasAereasSegundaBase--;
            return true;
        }
        return false;
    }

    private bool TemUnidadesVivas() => aeronavesRebeldes.Exists(x => x != null)
        || helicopterosRebeldes.Exists(x => x != null)
        || naviosRebeldes.Exists(x => x != null)
        || soldadosReforcados.Exists(x => x != null);
    private void RemoverContatosInvalidos()
    {
        if (contatosAtivos.Count == 0) return;
        List<int> remover = null;
        foreach (KeyValuePair<int, IdentidadeUnidade> contato in contatosAtivos)
        {
            if (contato.Value != null)
            {
                if (RelacaoHostil(contato.Value)) nivelAtividade = Mathf.Max(nivelAtividade, 2);
                continue;
            }
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
        reservasAereasSegundaBase = Mathf.Max(0, salvo.reservasAereasSegundaBase);
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
