using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[ExecuteAlways]
public class GerenteDeTerritorio : MonoBehaviour
{
    public static GerenteDeTerritorio Instancia;

    private List<MarcadorTerritorio> marcadores = new List<MarcadorTerritorio>();
    [SerializeField] private DadosMapaTerritorial mapaPolitico;
    [SerializeField] private float nivelMarPolitico;
    [SerializeField] private bool usarLimitesDeTerreno = true;
    [SerializeField] private Bounds limitesMapaExplicitos = new Bounds(Vector3.zero, new Vector3(10000f, 1000f, 10000f));
    private readonly Dictionary<string, int> proprietariosCapturados = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> neutralidadeTerritorial = new Dictionary<string, bool>(StringComparer.Ordinal);
    private sealed class ProgressoCapturaTerritorial
    {
        public int equipe;
        public float segundos;
    }

    [Header("Captura territorial")]
    [Tooltip("Intervalo entre as leituras de presença das unidades em regiões capturáveis.")]
    [SerializeField, Min(0.25f)] private float intervaloVerificacaoCaptura = 1f;
    [Tooltip("Tempo contínuo de ocupação terrestre necessário para reivindicar uma região neutra.")]
    [SerializeField, Min(1f)] private float segundosParaCapturarTerritorio = 30f;
    private readonly Dictionary<string, ProgressoCapturaTerritorial> progressoCaptura = new Dictionary<string, ProgressoCapturaTerritorial>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> equipesPresentesPorTerritorio = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<string> territoriosContestados = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<IdentidadeUnidade> bufferUnidadesCaptura = new List<IdentidadeUnidade>(128);
    private float proximaVerificacaoCaptura;
    private Bounds limitesTerritoriais;
    private bool limitesTerritoriaisProntos;
    private GlobalWorldDefinition definicaoMapaGlobal;

    public event Action<string, int, int> OnTerritoryOwnerChanged;
    /// <summary>Disparado somente quando a ocupação terrestre conclui a captura no ciclo físico de presença.</summary>
    public event Action<string, int, int> OnTerritoryCapturedByOccupation;

    public DadosMapaTerritorial MapaPolitico
    {
        get { GarantirMapaPolitico(); return mapaPolitico; }
        set { mapaPolitico = value; mapaPolitico?.InvalidarIndice(); }
    }

    void Awake()
    {
        GarantirMapaPolitico();
        AtualizarLimitesTerritoriais();
        SceneManager.sceneLoaded += AoCarregarCena;
        if (Instancia == null) 
        {
            Instancia = this;
            
            // Se o Gerente foi criado depois do jogo iniciar (pelo Construtor),
            // busca e registra todos os marcadores que já nasceram na fase.
            MarcadorTerritorio[] todosMarcadores = Object.FindObjectsByType<MarcadorTerritorio>(FindObjectsSortMode.None);
            foreach (var m in todosMarcadores)
            {
                RegistrarMarcador(m);
            }
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void Update()
    {
        Hegemonia.RTS.RTSGameSession sessao = Hegemonia.RTS.RTSGameSession.Instancia;
        if (!Application.isPlaying
            || Instancia != this
            || (sessao != null && sessao.Phase != Hegemonia.RTS.RTSSessionPhase.Playing)
            || Time.time < proximaVerificacaoCaptura)
        {
            return;
        }

        float intervalo = Mathf.Max(0.25f, intervaloVerificacaoCaptura);
        proximaVerificacaoCaptura = Time.time + intervalo;
        AtualizarCapturasTerritoriais(intervalo);
    }

    private void AtualizarCapturasTerritoriais(float intervalo)
    {
        GarantirMapaPolitico();
        if (mapaPolitico == null || mapaPolitico.Regioes == null || mapaPolitico.Regioes.Count == 0)
        {
            progressoCaptura.Clear();
            return;
        }

        // Só forças terrestres podem reivindicar regiões neutras configuradas
        // como capturáveis. Uma presença de outra equipe contesta a região.
        equipesPresentesPorTerritorio.Clear();
        territoriosContestados.Clear();
        RegistroEntidadesJogo.FillUnidades(bufferUnidadesCaptura);

        for (int i = 0; i < bufferUnidadesCaptura.Count; i++)
        {
            IdentidadeUnidade unidade = bufferUnidadesCaptura[i];
            if (!UnidadePodeCapturarTerritorio(unidade)) continue;

            ResultadoConsultaTerritorio territorio = ObterTerritorioNaPosicao(unidade.transform.position);
            if (!territorio.encontrouRegiao
                || territorio.aguasInternacionais
                || territorio.tipo != TipoRegiaoPolitica.Terra
                || !territorio.capturable
                || !territorio.neutral
                || territorio.ownerCountryTeamId > 0
                || string.IsNullOrEmpty(territorio.territorioId))
            {
                continue;
            }

            int equipeExistente;
            if (!equipesPresentesPorTerritorio.TryGetValue(territorio.territorioId, out equipeExistente))
            {
                equipesPresentesPorTerritorio[territorio.territorioId] = unidade.teamID;
            }
            else if (equipeExistente != unidade.teamID)
            {
                territoriosContestados.Add(territorio.territorioId);
            }
        }

        for (int i = 0; i < mapaPolitico.Regioes.Count; i++)
        {
            RegiaoPolitica regiao = mapaPolitico.Regioes[i];
            if (regiao == null || string.IsNullOrEmpty(regiao.territorioId) || !regiao.capturable
                || regiao.tipo != TipoRegiaoPolitica.Terra)
            {
                continue;
            }

            string territorioId = regiao.territorioId;
            bool neutralidadeAtual = neutralidadeTerritorial.TryGetValue(territorioId, out bool neutralidadeSalva)
                ? neutralidadeSalva
                : regiao.neutral;
            if (!neutralidadeAtual || ObterDonoDaRegiao(territorioId) > 0)
            {
                progressoCaptura.Remove(territorioId);
                continue;
            }

            int equipe;
            if (territoriosContestados.Contains(territorioId)
                || !equipesPresentesPorTerritorio.TryGetValue(territorioId, out equipe))
            {
                progressoCaptura.Remove(territorioId);
                continue;
            }

            ProgressoCapturaTerritorial progresso;
            if (!progressoCaptura.TryGetValue(territorioId, out progresso) || progresso.equipe != equipe)
            {
                progresso = new ProgressoCapturaTerritorial { equipe = equipe, segundos = 0f };
                progressoCaptura[territorioId] = progresso;
            }

            progresso.segundos += intervalo;
            if (progresso.segundos >= Mathf.Max(1f, segundosParaCapturarTerritorio))
            {
                int donoAnterior = ObterDonoDaRegiao(territorioId);
                if (TentarCapturarTerritorio(territorioId, equipe))
                {
                    progressoCaptura.Remove(territorioId);
                    OnTerritoryCapturedByOccupation?.Invoke(territorioId, donoAnterior, equipe);
                }
            }
        }
    }

    private static bool UnidadePodeCapturarTerritorio(IdentidadeUnidade unidade)
    {
        if (unidade == null || !unidade.isActiveAndEnabled || unidade.teamID <= 0
            || (unidade.tipoUnidade != TipoUnidade.Infantaria && unidade.tipoUnidade != TipoUnidade.Veiculo))
        {
            return false;
        }

        SistemaDeDanos danos = unidade.GetComponent<SistemaDeDanos>();
        return danos == null || danos.vidaAtual > 0f;
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        AtualizarLimitesTerritoriais();
        MarcadorTerritorio[] todosMarcadores = Object.FindObjectsByType<MarcadorTerritorio>(FindObjectsSortMode.None);
        for (int i = 0; i < todosMarcadores.Length; i++) RegistrarMarcador(todosMarcadores[i]);
    }

    private void GarantirMapaPolitico()
    {
        if (mapaPolitico != null) return;
        mapaPolitico = Resources.Load<DadosMapaTerritorial>(DadosMapaTerritorial.NomeResource);
        if (mapaPolitico == null)
        {
            Texture2D imagem = Resources.Load<Texture2D>("fase");
            mapaPolitico = DadosMapaTerritorial.CriarModeloFase(imagem);
        }
    }

    public void AtualizarLimitesTerritoriais()
    {
        limitesTerritoriaisProntos = false;
        if (!usarLimitesDeTerreno)
        {
            limitesTerritoriais = limitesMapaExplicitos;
            limitesTerritoriaisProntos = limitesTerritoriais.size.x > 0f && limitesTerritoriais.size.z > 0f;
            return;
        }

        GlobalTerrainStreamer[] streamers = FindObjectsByType<GlobalTerrainStreamer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < streamers.Length; i++)
        {
            GlobalWorldDefinition world = streamers[i] != null ? streamers[i].world : null;
            if (world == null || world.worldSize.x <= 0.001f || world.mapFootprintHeight <= 0.001f) continue;

            definicaoMapaGlobal = world;
            Vector3 centro = new Vector3(
                (world.MapMinX + world.MapMaxX) * 0.5f,
                nivelMarPolitico,
                (world.MapMinZ + world.MapMaxZ) * 0.5f);
            Vector3 tamanho = new Vector3(
                world.worldSize.x,
                Mathf.Max(1f, limitesMapaExplicitos.size.y),
                world.mapFootprintHeight);
            limitesTerritoriais = new Bounds(centro, tamanho);
            limitesTerritoriaisProntos = true;
            return;
        }
        definicaoMapaGlobal = null;

        Terrain[] terrenos = Terrain.activeTerrains;
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null || terreno.terrainData == null || !terreno.gameObject.scene.IsValid()) continue;
            Vector3 escala = terreno.transform.lossyScale;
            Vector3 tamanho = Vector3.Scale(terreno.terrainData.size, new Vector3(
                Mathf.Abs(escala.x) > 0.001f ? Mathf.Abs(escala.x) : 1f,
                Mathf.Abs(escala.y) > 0.001f ? Mathf.Abs(escala.y) : 1f,
                Mathf.Abs(escala.z) > 0.001f ? Mathf.Abs(escala.z) : 1f));
            Bounds bounds = new Bounds(terreno.GetPosition() + tamanho * 0.5f, tamanho);
            if (!limitesTerritoriaisProntos)
            {
                limitesTerritoriais = bounds;
                limitesTerritoriaisProntos = true;
            }
            else limitesTerritoriais.Encapsulate(bounds);
        }

        if (!limitesTerritoriaisProntos)
        {
            limitesTerritoriais = limitesMapaExplicitos;
            limitesTerritoriaisProntos = limitesTerritoriais.size.x > 0f && limitesTerritoriais.size.z > 0f;
        }
    }

    public bool TryWorldToMapUv(Vector3 worldPosition, out Vector2 uv)
    {
        bool dentroDaCoberturaDoMapa;
        return TryWorldToMapUv(worldPosition, out uv, out dentroDaCoberturaDoMapa)
            && dentroDaCoberturaDoMapa;
    }

    /// <summary>
    /// Converts against the political map bounds and separately reports whether
    /// the point is covered. A valid conversion outside [0,1] is important:
    /// legacy territory may only be considered outside political coverage.
    /// </summary>
    public bool TryWorldToMapUv(Vector3 worldPosition, out Vector2 uv, out bool dentroDaCoberturaDoMapa)
    {
        if (definicaoMapaGlobal != null)
            return definicaoMapaGlobal.TryWorldToMapUv(worldPosition, out uv, out dentroDaCoberturaDoMapa);

        if (!limitesTerritoriaisProntos) AtualizarLimitesTerritoriais();
        Vector3 min = limitesTerritoriais.min;
        float largura = limitesTerritoriais.size.x;
        float profundidade = limitesTerritoriais.size.z;
        if (!limitesTerritoriaisProntos || largura <= 0.001f || profundidade <= 0.001f)
        {
            uv = Vector2.zero;
            dentroDaCoberturaDoMapa = false;
            return false;
        }
        uv = new Vector2((worldPosition.x - min.x) / largura, 1f - (worldPosition.z - min.z) / profundidade);
        dentroDaCoberturaDoMapa = uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        return true;
    }

    public Vector3 MapUvToWorld(Vector2 uv)
    {
        if (definicaoMapaGlobal != null)
        {
            Vector3 pontoGlobal = definicaoMapaGlobal.MapUvToWorld(uv, nivelMarPolitico);
            Terrain terrenoGlobal = EncontrarTerrain(pontoGlobal);
            if (terrenoGlobal != null)
                pontoGlobal.y = terrenoGlobal.SampleHeight(pontoGlobal) + terrenoGlobal.GetPosition().y;
            return pontoGlobal;
        }

        if (!limitesTerritoriaisProntos) AtualizarLimitesTerritoriais();
        Vector3 min = limitesTerritoriais.min;
        Vector3 ponto = new Vector3(min.x + Mathf.Clamp01(uv.x) * limitesTerritoriais.size.x,
            nivelMarPolitico, min.z + (1f - Mathf.Clamp01(uv.y)) * limitesTerritoriais.size.z);
        Terrain terreno = EncontrarTerrain(ponto);
        if (terreno != null) ponto.y = terreno.SampleHeight(ponto) + terreno.GetPosition().y;
        return ponto;
    }

    /// <summary>Consulta primeiro a geometria política e só usa o legado se ela não cobrir o ponto.</summary>
    public ResultadoConsultaTerritorio ObterTerritorioNaPosicao(Vector3 ponto)
    {
        GarantirMapaPolitico();
        Vector2 uv = Vector2.zero;
        bool dentroDaCoberturaDoMapa = false;
        bool possuiPosicaoMapa = mapaPolitico != null
            && TryWorldToMapUv(ponto, out uv, out dentroDaCoberturaDoMapa);
        if (possuiPosicaoMapa && dentroDaCoberturaDoMapa)
        {
            ResultadoConsultaTerritorio politico = mapaPolitico.ConsultarUv(uv);
            if (politico.encontrouRegiao)
            {
                if (proprietariosCapturados.TryGetValue(politico.territorioId, out int proprietario))
                {
                    politico.ownerCountryTeamId = proprietario;
                }
                if (neutralidadeTerritorial.TryGetValue(politico.territorioId, out bool neutro)) politico.neutral = neutro;
                politico.worldPosition = ponto;
                return politico;
            }

            if (EhAguasInternacionais(ponto))
            {
                return new ResultadoConsultaTerritorio
                {
                    encontrouRegiao = true,
                    territorioId = "aguas-internacionais",
                    ownerCountryTeamId = -1,
                    aguasInternacionais = true,
                    tipo = TipoRegiaoPolitica.AguasTerritoriais,
                    worldPosition = ponto,
                    mapPosition = uv,
                    possuiMapPosition = true,
                    fonte = FonteConsultaTerritorial.AguasInternacionais
                };
            }

            // A cobertura política é soberana mesmo onde exista um vão entre
            // polígonos. Não deixar marcadores/zones legados preencherem esse
            // espaço com uma soberania que não foi desenhada.
            ResultadoConsultaTerritorio indefinidoNaCobertura = ResultadoConsultaTerritorio.NaoDefinido;
            indefinidoNaCobertura.worldPosition = ponto;
            indefinidoNaCobertura.mapPosition = uv;
            indefinidoNaCobertura.possuiMapPosition = true;
            return indefinidoNaCobertura;
        }

        // Água física fora das águas territoriais definidas continua sendo
        // internacional, inclusive fora da extensão UV da camada política.
        if (EhAguasInternacionais(ponto))
        {
            return new ResultadoConsultaTerritorio
            {
                encontrouRegiao = true,
                territorioId = "aguas-internacionais",
                ownerCountryTeamId = -1,
                aguasInternacionais = true,
                tipo = TipoRegiaoPolitica.AguasTerritoriais,
                worldPosition = ponto,
                mapPosition = uv,
                possuiMapPosition = possuiPosicaoMapa && dentroDaCoberturaDoMapa,
                fonte = FonteConsultaTerritorial.AguasInternacionais
            };
        }

        // Regiões políticas desenhadas são soberanas. Legado permanece apenas como fallback.
        int donoLegado = ObterDonoLegadoDoPonto(ponto);
        if (donoLegado != 0)
        {
            return new ResultadoConsultaTerritorio
            {
                encontrouRegiao = true,
                territorioId = "legado-marker-" + donoLegado,
                ownerCountryTeamId = donoLegado,
                tipo = TipoRegiaoPolitica.Terra,
                worldPosition = ponto,
                mapPosition = uv,
                possuiMapPosition = possuiPosicaoMapa && dentroDaCoberturaDoMapa,
                fonte = FonteConsultaTerritorial.Legado
            };
        }
        ResultadoConsultaTerritorio indefinido = ResultadoConsultaTerritorio.NaoDefinido;
        indefinido.worldPosition = ponto;
        indefinido.mapPosition = uv;
        indefinido.possuiMapPosition = possuiPosicaoMapa && dentroDaCoberturaDoMapa;
        return indefinido;
    }

    /// <summary>
    /// A fundação de uma Prefeitura transfere somente uma região política
    /// terrestre explicitamente neutra. A captura militar continua seguindo
    /// seu próprio tempo de ocupação em TentarCapturarTerritorio.
    /// </summary>
    public bool TentarFundarTerritorio(Vector3 ponto, int novoOwnerTeamId)
    {
        if (novoOwnerTeamId <= 0) return false;

        ResultadoConsultaTerritorio territorio = ObterTerritorioNaPosicao(ponto);
        if (!territorio.encontrouRegiao
            || territorio.aguasInternacionais
            || territorio.tipo != TipoRegiaoPolitica.Terra
            || territorio.fonte != FonteConsultaTerritorial.PoligonoPolitico)
        {
            return false;
        }

        RegiaoPolitica regiao = MapaPolitico != null ? MapaPolitico.EncontrarRegiao(territorio.territorioId) : null;
        if (regiao == null) return false;

        int donoAnterior = ObterDonoDaRegiao(territorio.territorioId);
        bool neutroAnterior = neutralidadeTerritorial.TryGetValue(territorio.territorioId, out bool neutro)
            ? neutro
            : regiao.neutral;
        // Uma Prefeitura pode fundar tanto uma região neutra quanto uma
        // região terrestre ainda sem país associado (-1). Territórios já
        // pertencentes a uma equipe continuam protegidos.
        if (donoAnterior > 0 || (donoAnterior == 0 && !neutroAnterior)) return false;

        proprietariosCapturados[territorio.territorioId] = novoOwnerTeamId;
        neutralidadeTerritorial[territorio.territorioId] = false;
        OnTerritoryOwnerChanged?.Invoke(territorio.territorioId, donoAnterior, novoOwnerTeamId);
        return true;
    }

    public bool TentarCapturarTerritorio(string territorioId, int novoOwnerTeamId)
    {
        if (novoOwnerTeamId <= 0) return false;
        RegiaoPolitica regiao = MapaPolitico != null ? MapaPolitico.EncontrarRegiao(territorioId) : null;
        if (regiao == null || !regiao.capturable) return false;
        int donoAnterior = ObterDonoDaRegiao(territorioId);
        bool neutroAnterior = neutralidadeTerritorial.TryGetValue(territorioId, out bool neutro)
            ? neutro
            : regiao.neutral;
        if (donoAnterior == novoOwnerTeamId && (!neutroAnterior || proprietariosCapturados.ContainsKey(territorioId))) return false;
        proprietariosCapturados[territorioId] = novoOwnerTeamId;
        neutralidadeTerritorial[territorioId] = false;
        OnTerritoryOwnerChanged?.Invoke(territorioId, donoAnterior, novoOwnerTeamId);
        return true;
    }

    public int ObterDonoDaRegiao(string territorioId)
    {
        if (string.IsNullOrEmpty(territorioId)) return -1;
        if (proprietariosCapturados.TryGetValue(territorioId, out int capturado)) return capturado;
        RegiaoPolitica regiao = MapaPolitico != null ? MapaPolitico.EncontrarRegiao(territorioId) : null;
        return regiao != null ? regiao.ownerCountryTeamId : -1;
    }

    public List<SaveProprietarioTerritorio> CopiarProprietariosCapturados()
    {
        List<SaveProprietarioTerritorio> resultado = new List<SaveProprietarioTerritorio>(proprietariosCapturados.Count);
        foreach (KeyValuePair<string, int> par in proprietariosCapturados)
        {
            RegiaoPolitica regiao = MapaPolitico != null ? MapaPolitico.EncontrarRegiao(par.Key) : null;
            if (regiao == null) continue;
            resultado.Add(new SaveProprietarioTerritorio
            {
                territorioId = par.Key,
                ownerCountryTeamId = par.Value,
                neutral = neutralidadeTerritorial.TryGetValue(par.Key, out bool neutro) ? neutro : regiao.neutral
            });
        }
        resultado.Sort((a, b) => string.CompareOrdinal(a.territorioId, b.territorioId));
        return resultado;
    }

    public void RestaurarProprietariosCapturados(IList<SaveProprietarioTerritorio> estados)
    {
        RestaurarEstadoPolitico(estados, true);
    }

    /// <summary>
    /// Restores owners over the asset configuration. Saves before format 17
    /// did not carry neutral, so their default bool must never override the
    /// base map value. A legacy positive owner on a capturable neutral region
    /// is the old representation of a successful capture.
    /// </summary>
    public void RestaurarEstadoPolitico(IList<SaveProprietarioTerritorio> estados, bool neutralSnapshotAvailable)
    {
        GarantirMapaPolitico();
        proprietariosCapturados.Clear();
        neutralidadeTerritorial.Clear();
        if (estados == null) return;
        for (int i = 0; i < estados.Count; i++)
        {
            SaveProprietarioTerritorio estado = estados[i];
            RegiaoPolitica regiao = estado != null && MapaPolitico != null ? MapaPolitico.EncontrarRegiao(estado.territorioId) : null;
            if (regiao == null || estado == null || string.IsNullOrWhiteSpace(estado.territorioId)) continue;

            if (!neutralSnapshotAvailable)
            {
                // Legacy records contain only an owner. The asset remains the
                // source for neutral/capturable configuration except for the
                // old capture convention on a neutral capturable territory.
                if (regiao.capturable && estado.ownerCountryTeamId > 0)
                {
                    proprietariosCapturados[estado.territorioId] = estado.ownerCountryTeamId;
                    neutralidadeTerritorial[estado.territorioId] = false;
                }
                continue;
            }

            if (estado.ownerCountryTeamId > 0) proprietariosCapturados[estado.territorioId] = estado.ownerCountryTeamId;
            neutralidadeTerritorial[estado.territorioId] = estado.neutral;
        }
    }

    public ResultadoConsultaTerritorio ObterEstadoDaRegiao(string territorioId)
    {
        RegiaoPolitica regiao = MapaPolitico != null ? MapaPolitico.EncontrarRegiao(territorioId) : null;
        if (regiao == null) return ResultadoConsultaTerritorio.NaoDefinido;
        return new ResultadoConsultaTerritorio
        {
            encontrouRegiao = true,
            territorioId = regiao.territorioId,
            ownerCountryTeamId = ObterDonoDaRegiao(territorioId),
            neutral = neutralidadeTerritorial.TryGetValue(territorioId, out bool neutro) ? neutro : regiao.neutral,
            capturable = regiao.capturable,
            tipo = regiao.tipo,
            fonte = FonteConsultaTerritorial.PoligonoPolitico
        };
    }

    private bool EhAguasInternacionais(Vector3 ponto)
    {
        if (ponto.y < nivelMarPolitico - 0.5f) return true;
        Terrain terreno = EncontrarTerrain(ponto);
        if (terreno == null) return false;
        return terreno.SampleHeight(ponto) + terreno.GetPosition().y < nivelMarPolitico - 0.5f;
    }

    private static Terrain EncontrarTerrain(Vector3 ponto)
    {
        Terrain[] terrenos = Terrain.activeTerrains;
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null || terreno.terrainData == null) continue;
            Vector3 escala = terreno.transform.lossyScale;
            Vector3 tamanho = Vector3.Scale(terreno.terrainData.size, new Vector3(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z)));
            Bounds bounds = new Bounds(terreno.GetPosition() + tamanho * 0.5f, tamanho);
            if (bounds.Contains(ponto)) return terreno;
        }
        return null;
    }

    public void RegistrarMarcador(MarcadorTerritorio marcador)
    {
        if (!marcadores.Contains(marcador)) marcadores.Add(marcador);
    }

    public void RemoverMarcador(MarcadorTerritorio marcador)
    {
        if (marcadores.Contains(marcador)) marcadores.Remove(marcador);
    }

    /// <summary>
    /// Retorna o TeamID de quem é dono do ponto. Retorna 0 se for neutro.
    /// Resolve distributivamente distâncias geométricas em formato de QUADRADO.
    /// </summary>
    public int ObterDonoDoPonto(Vector3 ponto)
    {
        GarantirMapaPolitico();
        if (mapaPolitico != null
            && TryWorldToMapUv(ponto, out Vector2 uv, out bool dentroDaCoberturaDoMapa)
            && dentroDaCoberturaDoMapa)
        {
            ResultadoConsultaTerritorio politico = mapaPolitico.ConsultarUv(uv);
            if (politico.encontrouRegiao)
            {
                if (proprietariosCapturados.TryGetValue(politico.territorioId, out int novoDono)) return novoDono;
                return Mathf.Max(0, politico.ownerCountryTeamId);
            }
            // Uma lacuna dentro da camada política é indefinida e não recebe
            // propriedade legada. A API inteira representa ambos como 0.
            return 0;
        }
        if (EhAguasInternacionais(ponto)) return 0;
        // Os polígonos têm precedência absoluta; reservas de expansão e raios
        // de marcadores só podem responder onde não há região política definida.
        return ObterDonoLegadoDoPonto(ponto);
    }

    private int ObterDonoLegadoDoPonto(Vector3 ponto)
    {
        // Uma parcela de fronteira reivindicada representa uma expansão
        // contínua, não apenas o raio visual da bandeira que a fundou. A
        // autoridade da parcela só responde quando ela está reservada ou
        // ocupada; zonas livres continuam neutras e seguem o cálculo normal.
        GerenciadorExpansaoFronteira expansao = GerenciadorExpansaoFronteira.Instancia;
        ZonaFronteiraExpansionavel zona = expansao != null ? expansao.EncontrarNoPonto(ponto) : null;
        if (zona != null
            && zona.TeamDono > 0
            && (zona.Estado == EstadoZonaFronteiraExpansionavel.Reservada
                || zona.Estado == EstadoZonaFronteiraExpansionavel.Ocupada))
        {
            return zona.TeamDono;
        }

        int donoVencedor = 0;
        // Float para achar quem vence a sobreposição num conflito do formato quadrado.
        float menorDistanciaQuadrada = float.MaxValue; 
        
        // --- Corredor Nulo ---
        int donoSecundario = 0; // Se houver empate/sobreposição, guardamos o segundo dono 

        foreach (var m in marcadores)
        {
            if (m == null || !m.gameObject.activeInHierarchy) continue;

            // Transforma o cálculo redondo de Euler para Box/Quadrado Absoluto:
            // A distância "quadrada" para borda é a diferença máxima nos seus dois eixos X e Z
            float distX = Mathf.Abs(ponto.x - m.transform.position.x);
            float distZ = Mathf.Abs(ponto.z - m.transform.position.z);
            float distanciaQuadradaLocal = Mathf.Max(distX, distZ); // Corte em linha reta p/ divisas perfeitamente retas

            // Vê se a pessoa ta dentro da expansão da nossa base (Bandeira=100m, Prefeitura=300m)
            if (distanciaQuadradaLocal <= m.raioDeDominio)
            {
                if (distanciaQuadradaLocal < menorDistanciaQuadrada)
                {
                    // O vencedor anterior vira secundário (Corredor compartilhado se a diferença for pouca)
                    if (donoVencedor != 0 && donoVencedor != m.teamID)
                    {
                        // Se a diferença entre a distância para a fronteira A e fronteira B for menos de 5 metros, é Corredor Nulo!
                        // Mas aqui apenas guardamos. A matemática principal ocorre fora ou mantemos o mais forte.
                        donoSecundario = donoVencedor;
                    }

                    menorDistanciaQuadrada = distanciaQuadradaLocal;
                    donoVencedor = m.teamID;
                }
                else if (Mathf.Abs(distanciaQuadradaLocal - menorDistanciaQuadrada) < 5.0f && m.teamID != donoVencedor)
                {
                    // Margem de 5 metros onde é exatamene o meio da divisa: CORREDOR NULO MATEMÁTICO.
                    // Ambos têm jurisprudência quase igual.
                    donoSecundario = m.teamID;
                }
            }
        }

        return donoVencedor;
    }



    /// <summary>
    /// Regra: "Não se pode por na mesma faixa de terra duas prefeituras."
    /// Usamos NavMesh para testar se há conexão terrestre contínua entre o ponto desejado e as prefeituras existentes.
    /// </summary>
    public bool PodeConstruirPrefeitura(Vector3 ponto)
    {
        foreach (var m in marcadores)
        {
            // O loop verifica se existe OUTRO governo central registrado (Independente de quem)
            if (m != null && m.ehPrefeitura)
            {
                // Verifica distância bruta primeiro. Se estiver incrivelmente colado, bloqueia.
                if (Vector3.Distance(ponto, m.transform.position) < 50f) return false;

                // Tenta calcular um caminho de NavMesh entre a tentativa do mouse e a Prefeitura anterior...
                NavMeshPath caminho = new NavMeshPath();
                if (NavMesh.CalculatePath(ponto, m.transform.position, 1, caminho))
                {
                    if (caminho.status == NavMeshPathStatus.PathComplete)
                    {
                        // Estões conectados na mesma faixa de terra/ilha do mapa e tem passagem de andada pra eles
                        return false;
                    }
                }
            }
        }
        
        return true;
    }
}
