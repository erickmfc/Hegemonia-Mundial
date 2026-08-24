using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renderiza uma leitura superior do mundo para a Carta Náutica do Quartel.
/// A câmera só renderiza quando a aba solicita uma atualização, evitando custo
/// contínuo enquanto o painel está fechado ou em outra aba.
/// </summary>
[DisallowMultipleComponent]
public sealed class CartaTerrenoRenderer : MonoBehaviour
{
    private const int LarguraTextura = 1024;
    private const int AlturaTextura = 512;

    private Camera cameraCarta;
    private RenderTexture texturaCarta;
    private bool renderPendente;
    private bool possuiRenderCache;
    private Vector3 ultimoCentro;
    private float ultimoRaio;
    private float ultimoAspecto;
    private bool ultimaVistaInclinada;

    public Texture Textura => texturaCarta;

    public Texture Renderizar(Vector3 centro, float raio, float aspecto)
    {
        return Renderizar(centro, raio, aspecto, false);
    }

    public Texture Renderizar(Vector3 centro, float raio, float aspecto, bool vistaInclinada)
    {
        GarantirRecursos();
        if (cameraCarta == null || texturaCarta == null)
        {
            return null;
        }

        raio = Mathf.Max(50f, raio);
        aspecto = Mathf.Clamp(aspecto, 1f, 4f);

        // O mapa cobre o raio operacional nos dois eixos. Em telas estreitas,
        // aumenta a altura ortográfica para não cortar as laterais.
        float fatorVertical = Mathf.Min(1f, aspecto);
        float tamanhoVertical = raio / Mathf.Max(0.1f, fatorVertical);
        float alturaCamera = Mathf.Max(800f, raio * 1.5f);

        bool mudouArea = !possuiRenderCache
            || (centro - ultimoCentro).sqrMagnitude > 0.25f
            || Mathf.Abs(raio - ultimoRaio) > 0.5f
            || Mathf.Abs(aspecto - ultimoAspecto) > 0.01f
            || ultimaVistaInclinada != vistaInclinada;

        if (mudouArea)
        {
            if (vistaInclinada)
            {
                Vector3 posicaoCamera = centro + new Vector3(0f, alturaCamera * 0.92f, -alturaCamera * 0.82f);
                cameraCarta.transform.SetPositionAndRotation(
                    posicaoCamera,
                    Quaternion.LookRotation(centro - posicaoCamera, Vector3.up));
                cameraCarta.orthographicSize = raio * 0.86f / Mathf.Max(0.1f, fatorVertical);
            }
            else
            {
                cameraCarta.transform.SetPositionAndRotation(
                    new Vector3(centro.x, centro.y + alturaCamera, centro.z),
                    Quaternion.Euler(90f, 0f, 0f));
                cameraCarta.orthographicSize = tamanhoVertical;
            }
            cameraCarta.farClipPlane = Mathf.Clamp(alturaCamera + raio * 3f + 500f, 2000f, 20000f);
            cameraCarta.targetTexture = texturaCarta;

            ultimoCentro = centro;
            ultimoRaio = raio;
            ultimoAspecto = aspecto;
            ultimaVistaInclinada = vistaInclinada;
            possuiRenderCache = true;
            renderPendente = true;
        }

        // A captura acontece no LateUpdate. Camera.Render dentro do OnGUI
        // entra no ciclo de renderizacao URP ja aberto pelo GameView e gera
        // "UniversalCameraData has already been created".
        return texturaCarta;
    }

    private void LateUpdate()
    {
        if (!renderPendente || cameraCarta == null || texturaCarta == null)
        {
            return;
        }

        renderPendente = false;
        cameraCarta.Render();
    }

    private void GarantirRecursos()
    {
        if (texturaCarta == null)
        {
            texturaCarta = new RenderTexture(LarguraTextura, AlturaTextura, 24, RenderTextureFormat.ARGB32)
            {
                name = "RT_CartaTerreno",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            texturaCarta.Create();
        }

        if (cameraCarta != null)
        {
            return;
        }

        GameObject objetoCamera = new GameObject("Camera_CartaTerreno");
        objetoCamera.transform.SetParent(null);
        cameraCarta = objetoCamera.AddComponent<Camera>();
        cameraCarta.name = "Camera_CartaTerreno";
        cameraCarta.orthographic = true;
        cameraCarta.clearFlags = CameraClearFlags.SolidColor;
        cameraCarta.backgroundColor = new Color(0.035f, 0.16f, 0.23f, 1f);
        cameraCarta.nearClipPlane = 0.1f;
        cameraCarta.depth = -100f;
        cameraCarta.allowHDR = false;
        cameraCarta.allowMSAA = false;
        cameraCarta.useOcclusionCulling = false;
        cameraCarta.cullingMask = ~0;

        int camadaUI = LayerMask.NameToLayer("UI");
        if (camadaUI >= 0)
        {
            cameraCarta.cullingMask &= ~(1 << camadaUI);
        }

        cameraCarta.targetTexture = texturaCarta;
        cameraCarta.enabled = false;
    }

    private void OnDestroy()
    {
        if (cameraCarta != null)
        {
            Destroy(cameraCarta.gameObject);
            cameraCarta = null;
        }

        if (texturaCarta != null)
        {
            texturaCarta.Release();
            Destroy(texturaCarta);
            texturaCarta = null;
        }
    }
}

/// <summary>
/// Coleta em baixa frequencia os dados usados exclusivamente pela Carta
/// Nautica do Quartel. Este componente nao envia ordens e nao altera unidades.
/// Ele fica no mesmo script de runtime da carta para permanecer incluido no
/// Assembly-CSharp mesmo em projetos que ainda tenham o meta legado da view.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuartelCartaTopograficaView : MonoBehaviour
{
    public enum ModoVisualizacao
    {
        Topografico2D,
        Topografico3D
    }

    [Serializable]
    public sealed class UnidadeTelemetria
    {
        public string id;
        public string nome;
        public string tipo;
        public int equipe;
        public bool aliada;
        public Vector3 posicao;
        public Vector3 destino;
        public bool possuiDestino;
        public string estado;
        public string situacao;
        public string missao;
        public string fonteMovimento;
        public string baseAtual;
        public string vaga;
        public string rumo;
        public string armamento;
        public float velocidadeMetrosPorSegundo;
        public float altitudeAbsoluta;
        public float elevacaoTerreno;
        public float alturaAcimaDoSolo;
        public float combustivelPercentual;
        public float combustivelAtual;
        public float combustivelCapacidade;
        public float integridadePercentual;
        public float distanciaPercorrida;
        public float distanciaRestante;
        public float tempoEstimadoSegundos;
        public int misseisDisponiveis;
        public int misseisMaximos;
        public int torpedosDisponiveis;
        public int torpedosMaximos;
        public List<Vector3> rotaPercorrida = new List<Vector3>(24);
    }

    [Serializable]
    public sealed class MissilTelemetria
    {
        public string id;
        public string nome;
        public string tipo;
        public string origem;
        public int equipe;
        public bool aliado;
        public Vector3 posicao;
        public Vector3 pontoLancamento;
        public Vector3 alvoAtual;
        public Vector3 pontoProvavelImpacto;
        public float distanciaLancadorAlvo;
        public float distanciaPercorrida;
        public float distanciaRestante;
        public float velocidadeMetrosPorSegundo;
        public float tempoDesdeLancamento;
        public string estado;
        public bool guiagemPerdida;
    }

    private sealed class HistoricoUnidade
    {
        public Vector3 ultimaPosicao;
        public float distanciaPercorrida;
        public readonly List<Vector3> rota = new List<Vector3>(24);
        public bool inicializado;
    }

    private readonly List<IdentidadeUnidade> identidades = new List<IdentidadeUnidade>(256);
    private readonly List<MissileThreatTracker> ameacas = new List<MissileThreatTracker>(64);
    private readonly List<UnidadeTelemetria> unidades = new List<UnidadeTelemetria>(128);
    private readonly List<MissilTelemetria> misseis = new List<MissilTelemetria>(64);
    private readonly Dictionary<string, HistoricoUnidade> historico = new Dictionary<string, HistoricoUnidade>(StringComparer.Ordinal);
    private readonly HashSet<int> identidadesProcessadas = new HashSet<int>();

    private float proximaAmostragem;
    private Transform centroMapa;
    private int equipeMapa;
    private float raioMapa;

    public ModoVisualizacao Modo { get; set; } = ModoVisualizacao.Topografico2D;
    public IReadOnlyList<UnidadeTelemetria> Unidades => unidades;
    public IReadOnlyList<MissilTelemetria> Misseis => misseis;
    public Transform CentroMapa => centroMapa;
    public float RaioMapa => raioMapa;
    public float ProximaAmostragem => proximaAmostragem;

    public void Atualizar(Transform centro, int equipe, float raio, bool forcar = false)
    {
        if (centro == null) return;
        if (!forcar && Time.unscaledTime < proximaAmostragem) return;

        centroMapa = centro;
        equipeMapa = equipe;
        raioMapa = Mathf.Max(100f, raio);
        proximaAmostragem = Time.unscaledTime + 0.75f;

        AtualizarUnidades();
        AtualizarMisseis();
    }

    public UnidadeTelemetria EncontrarUnidade(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < unidades.Count; i++)
        {
            if (unidades[i] != null && string.Equals(unidades[i].id, id, StringComparison.Ordinal)) return unidades[i];
        }
        return null;
    }

    public MissilTelemetria EncontrarMissil(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < misseis.Count; i++)
        {
            if (misseis[i] != null && string.Equals(misseis[i].id, id, StringComparison.Ordinal)) return misseis[i];
        }
        return null;
    }

    public float ObterElevacaoTerreno(Vector3 posicao)
    {
        Terrain[] terrenos = Terrain.activeTerrains;
        float melhorDistancia = float.PositiveInfinity;
        float melhorAltura = 0f;
        bool encontrou = false;

        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null || terreno.terrainData == null) continue;
            Bounds limites = terreno.terrainData.bounds;
            Vector3 origem = terreno.transform.position;
            float minX = origem.x + limites.min.x;
            float maxX = origem.x + limites.max.x;
            float minZ = origem.z + limites.min.z;
            float maxZ = origem.z + limites.max.z;
            if (posicao.x < minX || posicao.x > maxX || posicao.z < minZ || posicao.z > maxZ) continue;

            float distancia = (posicao.x - (minX + maxX) * 0.5f) * (posicao.x - (minX + maxX) * 0.5f)
                + (posicao.z - (minZ + maxZ) * 0.5f) * (posicao.z - (minZ + maxZ) * 0.5f);
            if (distancia > melhorDistancia) continue;
            melhorDistancia = distancia;
            melhorAltura = terreno.SampleHeight(posicao) + origem.y;
            encontrou = true;
        }

        return encontrou ? melhorAltura : 0f;
    }

    private void AtualizarUnidades()
    {
        unidades.Clear();
        identidadesProcessadas.Clear();
        RegistroEntidadesJogo.FillUnidades(identidades);
        if (identidades.Count == 0)
        {
            IdentidadeUnidade[] encontrados = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                if (encontrados[i] != null && !identidades.Contains(encontrados[i])) identidades.Add(encontrados[i]);
            }
        }

        for (int i = 0; i < identidades.Count; i++)
        {
            IdentidadeUnidade identidade = identidades[i];
            if (identidade == null || !identidade.gameObject.activeInHierarchy) continue;
            if (!identidadesProcessadas.Add(identidade.GetInstanceID())) continue;

            Vector3 posicao = identidade.transform.position;
            bool aliada = identidade.teamID == equipeMapa;
            if (!aliada)
            {
                BoeingE3Reconhecimento.ContatoReconhecimento contato;
                if (!BoeingE3Reconhecimento.TryObterContato(equipeMapa, identidade.GetInstanceID(), out contato) || contato == null) continue;
                posicao = contato.ultimaPosicaoConhecida;
            }

            Vector3 local = centroMapa.InverseTransformPoint(posicao);
            if (Mathf.Abs(local.x) > raioMapa || Mathf.Abs(local.z) > raioMapa) continue;
            unidades.Add(CriarUnidade(identidade, posicao, aliada));
        }

        LimparHistoricoAntigo();
    }

    private UnidadeTelemetria CriarUnidade(IdentidadeUnidade identidade, Vector3 posicao, bool aliada)
    {
        GameObject objeto = identidade.gameObject;
        ControleAviao aviao = objeto.GetComponent<ControleAviao>() ?? objeto.GetComponentInParent<ControleAviao>();
        ControleUnidade controle = objeto.GetComponent<ControleUnidade>() ?? objeto.GetComponentInParent<ControleUnidade>();
        ControleNavioRealista navio = objeto.GetComponent<ControleNavioRealista>() ?? objeto.GetComponentInParent<ControleNavioRealista>();
        ControleSubmarino submarino = objeto.GetComponent<ControleSubmarino>() ?? objeto.GetComponentInParent<ControleSubmarino>();
        BoeingE3Reconhecimento e3 = objeto.GetComponent<BoeingE3Reconhecimento>() ?? objeto.GetComponentInParent<BoeingE3Reconhecimento>();
        ControleAviaoCaca caca = objeto.GetComponent<ControleAviaoCaca>() ?? objeto.GetComponentInParent<ControleAviaoCaca>();
        CombustivelUnidade combustivel = objeto.GetComponent<CombustivelUnidade>() ?? objeto.GetComponentInParent<CombustivelUnidade>();
        SistemaDeDanos danos = objeto.GetComponent<SistemaDeDanos>() ?? objeto.GetComponentInParent<SistemaDeDanos>();

        string id = ObterId(objeto);
        HistoricoUnidade memoria;
        if (!historico.TryGetValue(id, out memoria))
        {
            memoria = new HistoricoUnidade();
            historico[id] = memoria;
        }
        if (memoria.inicializado) memoria.distanciaPercorrida += Vector3.Distance(memoria.ultimaPosicao, posicao);
        memoria.ultimaPosicao = posicao;
        memoria.inicializado = true;
        if (memoria.rota.Count == 0 || Vector3.Distance(memoria.rota[memoria.rota.Count - 1], posicao) >= 18f)
        {
            memoria.rota.Add(posicao);
            while (memoria.rota.Count > 24) memoria.rota.RemoveAt(0);
        }

        EstadoControleUnidadeSnapshot estadoControle = controle != null ? controle.ObterEstadoControle() : default(EstadoControleUnidadeSnapshot);
        Vector3 destino = Vector3.zero;
        bool possuiDestino = false;
        if (estadoControle.possuiDestinoOrdenado)
        {
            destino = estadoControle.ultimoDestino;
            possuiDestino = true;
        }
        else if (aviao != null && aviao.alvoGPSVoo != Vector3.zero)
        {
            destino = aviao.alvoGPSVoo;
            possuiDestino = aviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao || aviao.estadoAtual == ControleAviao.EstadoAviao.Decolando;
        }
        else if (caca != null && caca.DestinoAtual != Vector3.zero)
        {
            destino = caca.DestinoAtual;
            possuiDestino = true;
        }

        float elevacao = ObterElevacaoTerreno(posicao);
        float velocidade = aviao != null ? aviao.VelocidadeVooAtual : navio != null ? navio.VelocidadeAtual : controle != null ? controle.ObterVelocidadeAtualReal() : 0f;
        float capacidade = combustivel != null ? combustivel.Capacidade : 0f;
        float atual = combustivel != null ? combustivel.CombustivelAtual : 0f;
        float integridade = danos != null && danos.vidaMaxima > 0f ? Mathf.Clamp01(danos.vidaAtual / danos.vidaMaxima) : 1f;
        int misseisDisponiveis = 0;
        int misseisMaximos = 0;
        int torpedosDisponiveis = 0;
        int torpedosMaximos = 0;
        string armamento = "Sem leitura de armamento";

        if (submarino != null)
        {
            misseisDisponiveis = submarino.misseisDisponiveis;
            misseisMaximos = Mathf.Max(misseisDisponiveis, 22);
            torpedosDisponiveis = submarino.torpedosDisponiveis;
            torpedosMaximos = Mathf.Max(torpedosDisponiveis, 1);
            armamento = "Misseis " + misseisDisponiveis + " | Torpedos " + torpedosDisponiveis;
        }
        else
        {
            LancadorMisselCaca lancadorCaca = objeto.GetComponent<LancadorMisselCaca>() ?? objeto.GetComponentInChildren<LancadorMisselCaca>(true);
            LancadorNaval lancadorNaval = objeto.GetComponent<LancadorNaval>() ?? objeto.GetComponentInChildren<LancadorNaval>(true);
            if (lancadorCaca != null)
            {
                misseisDisponiveis = lancadorCaca.municaoAtual;
                misseisMaximos = lancadorCaca.municaoMaxima;
                armamento = "Misseis ar-ar " + misseisDisponiveis + "/" + misseisMaximos;
            }
            if (lancadorNaval != null)
            {
                misseisDisponiveis = lancadorNaval.municaoTotal;
                misseisMaximos = lancadorNaval.municaoMaxima;
                torpedosDisponiveis = lancadorNaval.torpedosTotal;
                torpedosMaximos = lancadorNaval.torpedosMaximos;
                armamento = "Misseis " + misseisDisponiveis + "/" + misseisMaximos + " | Torpedos " + torpedosDisponiveis + "/" + torpedosMaximos;
            }
            if (navio != null && lancadorCaca == null && lancadorNaval == null)
            {
                torpedosDisponiveis = navio.torpedosDisponiveis;
                torpedosMaximos = Mathf.Max(torpedosDisponiveis, 1);
                armamento = "Torpedos " + torpedosDisponiveis;
            }
        }

        string estado = aviao != null ? aviao.estadoAtual.ToString() : submarino != null ? (submarino.estaSubmerso ? "SUBMERSO" : "SUPERFICIE") : controle != null ? controle.OrdemAtual.ToString() : "SEM CONTROLADOR";
        string tipo = identidade.tipoUnidade.ToString().ToUpperInvariant();
        string missao = e3 != null ? "Reconhecimento e retransmissao" : controle != null && controle.OrdemAtual == OrdemControleUnidade.Patrulhando ? "Patrulha" : estado;
        string baseAtual = "Sem base registrada";
        string vaga = string.Empty;
        string autoridade = controle != null ? controle.ExecutorAtual : string.Empty;
        AeronaveEmbarcadaV2 aeronaveV2 = aviao != null ? aviao.GetComponent<AeronaveEmbarcadaV2>() : null;
        RegistroAeronavePortaAvioesV2 registroV2 = aeronaveV2 != null ? aeronaveV2.Registro : null;
        if (registroV2 != null)
        {
            baseAtual = string.IsNullOrWhiteSpace(registroV2.portaAvioesAtual) ? baseAtual : registroV2.portaAvioesAtual;
            vaga = !string.IsNullOrWhiteSpace(registroV2.vagaOcupada) ? registroV2.vagaOcupada : registroV2.vagaReservada;
            autoridade = string.IsNullOrWhiteSpace(aeronaveV2.DonoMovimento) ? autoridade : aeronaveV2.DonoMovimento;
        }

        float distanciaRestante = possuiDestino ? Vector3.Distance(posicao, destino) : 0f;
        float tempoEstimado = velocidade > 0.1f ? distanciaRestante / velocidade : 0f;
        return new UnidadeTelemetria
        {
            id = id,
            nome = identidade.name,
            tipo = tipo,
            equipe = identidade.teamID,
            aliada = aliada,
            posicao = posicao,
            destino = destino,
            possuiDestino = possuiDestino,
            estado = estado,
            situacao = aliada ? estado : "CONTATO INIMIGO",
            missao = missao,
            fonteMovimento = autoridade,
            baseAtual = baseAtual,
            vaga = string.IsNullOrWhiteSpace(vaga) ? "Sem vaga registrada" : vaga,
            rumo = ObterRumo(objeto.transform.forward),
            armamento = armamento,
            velocidadeMetrosPorSegundo = Mathf.Max(0f, velocidade),
            altitudeAbsoluta = posicao.y,
            elevacaoTerreno = elevacao,
            alturaAcimaDoSolo = Mathf.Max(0f, posicao.y - elevacao),
            combustivelPercentual = capacidade > 0f ? Mathf.Clamp01(atual / capacidade) : 1f,
            combustivelAtual = atual,
            combustivelCapacidade = capacidade,
            integridadePercentual = integridade,
            distanciaPercorrida = memoria.distanciaPercorrida,
            distanciaRestante = distanciaRestante,
            tempoEstimadoSegundos = tempoEstimado,
            misseisDisponiveis = misseisDisponiveis,
            misseisMaximos = misseisMaximos,
            torpedosDisponiveis = torpedosDisponiveis,
            torpedosMaximos = torpedosMaximos,
            rotaPercorrida = CopiarRota(memoria.rota)
        };
    }

    private void AtualizarMisseis()
    {
        misseis.Clear();
        MissileThreatTracker.CopiarAmeacasAtivas(ameacas);
        for (int i = 0; i < ameacas.Count; i++)
        {
            MissileThreatTracker tracker = ameacas[i];
            if (tracker == null || tracker.RaizMissil == null) continue;
            Vector3 posicao = tracker.RaizMissil.position;
            Vector3 local = centroMapa.InverseTransformPoint(posicao);
            if (Mathf.Abs(local.x) > raioMapa || Mathf.Abs(local.z) > raioMapa) continue;

            Vector3 alvo = tracker.PontoAlvoConhecido;
            float distanciaTotal = Vector3.Distance(tracker.PontoLancamento, alvo);
            float distanciaPercorrida = Vector3.Distance(tracker.PontoLancamento, posicao);
            misseis.Add(new MissilTelemetria
            {
                id = "missil-" + tracker.MissileId,
                nome = "MISSIL " + tracker.MissileId.ToString("00"),
                tipo = ResolverTipoMissil(tracker.RaizMissil.gameObject),
                origem = tracker.NomeOrigem,
                equipe = tracker.TeamOrigem,
                aliado = tracker.TeamOrigem == equipeMapa,
                posicao = posicao,
                pontoLancamento = tracker.PontoLancamento,
                alvoAtual = alvo,
                pontoProvavelImpacto = alvo,
                distanciaLancadorAlvo = distanciaTotal,
                distanciaPercorrida = distanciaPercorrida,
                distanciaRestante = Mathf.Max(0f, distanciaTotal - distanciaPercorrida),
                velocidadeMetrosPorSegundo = tracker.ObterVelocidadeAtual().magnitude,
                tempoDesdeLancamento = tracker.TempoDesdeLancamento,
                estado = "EM VOO",
                guiagemPerdida = !tracker.PossuiAlvoDinamico
            });
        }
    }

    private void LimparHistoricoAntigo()
    {
        if (historico.Count <= 256) return;
        List<string> remover = new List<string>();
        foreach (KeyValuePair<string, HistoricoUnidade> item in historico)
        {
            bool presente = false;
            for (int i = 0; i < unidades.Count; i++)
            {
                if (unidades[i] != null && unidades[i].id == item.Key) { presente = true; break; }
            }
            if (!presente) remover.Add(item.Key);
        }
        for (int i = 0; i < remover.Count; i++) historico.Remove(remover[i]);
    }

    private static List<Vector3> CopiarRota(List<Vector3> origem)
    {
        List<Vector3> copia = new List<Vector3>(origem != null ? origem.Count : 0);
        if (origem != null) copia.AddRange(origem);
        return copia;
    }

    private static string ObterId(GameObject objeto)
    {
        SaveableEntity saveable = objeto != null ? objeto.GetComponent<SaveableEntity>() : null;
        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId)) return saveable.UniqueId;
        return objeto == null ? "unidade-sem-objeto" : "runtime-" + objeto.GetInstanceID();
    }

    private static string ResolverTipoMissil(GameObject objeto)
    {
        if (objeto == null) return "MISSIL";
        string nome = objeto.name.ToUpperInvariant();
        if (objeto.GetComponent<MisselNaval>() != null || nome.Contains("NAVAL")) return "ANTINAVIO";
        if (objeto.GetComponent<MisselSubmarino>() != null || nome.Contains("SUB")) return "SUBMARINO";
        if (objeto.GetComponent<MisselICBM>() != null || nome.Contains("ICBM")) return "BALISTICO";
        if (objeto.GetComponent<MisselCaca>() != null || nome.Contains("CAC")) return "AR-AR";
        return "MISSIL TATICO";
    }

    private static string ObterRumo(Vector3 frente)
    {
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.01f) return "INDEFINIDO";
        float graus = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;
        if (graus < 0f) graus += 360f;
        string[] rumos = { "N", "NE", "L", "SE", "S", "SO", "O", "NO" };
        int indice = Mathf.RoundToInt(graus / 45f) % rumos.Length;
        return rumos[indice];
    }
}
