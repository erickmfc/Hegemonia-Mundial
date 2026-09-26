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
    private Bounds limitesTerritoriais;
    private bool limitesTerritoriaisProntos;

    public event Action<string, int, int> OnTerritoryOwnerChanged;

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
        if (!limitesTerritoriaisProntos) AtualizarLimitesTerritoriais();
        Vector3 min = limitesTerritoriais.min;
        float largura = limitesTerritoriais.size.x;
        float profundidade = limitesTerritoriais.size.z;
        if (!limitesTerritoriaisProntos || largura <= 0.001f || profundidade <= 0.001f)
        {
            uv = Vector2.zero;
            return false;
        }
        uv = new Vector2((worldPosition.x - min.x) / largura, 1f - (worldPosition.z - min.z) / profundidade);
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    public Vector3 MapUvToWorld(Vector2 uv)
    {
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
        if (mapaPolitico != null && TryWorldToMapUv(ponto, out Vector2 uv))
        {
            ResultadoConsultaTerritorio politico = mapaPolitico.ConsultarUv(uv);
            if (politico.encontrouRegiao)
            {
                if (proprietariosCapturados.TryGetValue(politico.territorioId, out int proprietario))
                {
                    politico.ownerCountryTeamId = proprietario;
                    politico.neutral = proprietario <= 0;
                }
                return politico;
            }

            if (EhAguasInternacionais(ponto))
            {
                return new ResultadoConsultaTerritorio
                {
                    encontrouRegiao = true,
                    territorioId = "aguas-internacionais",
                    ownerCountryTeamId = 0,
                    aguasInternacionais = true,
                    tipo = TipoRegiaoPolitica.AguasTerritoriais
                };
            }
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
                tipo = TipoRegiaoPolitica.Terra
            };
        }
        return ResultadoConsultaTerritorio.NaoDefinido;
    }

    public bool TentarCapturarTerritorio(string territorioId, int novoOwnerTeamId)
    {
        if (novoOwnerTeamId <= 0) return false;
        RegiaoPolitica regiao = MapaPolitico != null ? MapaPolitico.EncontrarRegiao(territorioId) : null;
        if (regiao == null || !regiao.capturable) return false;
        int donoAnterior = ObterDonoDaRegiao(territorioId);
        if (donoAnterior == novoOwnerTeamId && !regiao.neutral) return false;
        proprietariosCapturados[territorioId] = novoOwnerTeamId;
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
            resultado.Add(new SaveProprietarioTerritorio { territorioId = par.Key, ownerCountryTeamId = par.Value });
        resultado.Sort((a, b) => string.CompareOrdinal(a.territorioId, b.territorioId));
        return resultado;
    }

    public void RestaurarProprietariosCapturados(IList<SaveProprietarioTerritorio> estados)
    {
        proprietariosCapturados.Clear();
        if (estados == null) return;
        for (int i = 0; i < estados.Count; i++)
        {
            SaveProprietarioTerritorio estado = estados[i];
            RegiaoPolitica regiao = estado != null && MapaPolitico != null ? MapaPolitico.EncontrarRegiao(estado.territorioId) : null;
            if (regiao != null && regiao.capturable && estado.ownerCountryTeamId > 0)
                proprietariosCapturados[estado.territorioId] = estado.ownerCountryTeamId;
        }
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
        if (mapaPolitico != null && TryWorldToMapUv(ponto, out Vector2 uv))
        {
            ResultadoConsultaTerritorio politico = mapaPolitico.ConsultarUv(uv);
            if (politico.encontrouRegiao)
            {
                if (proprietariosCapturados.TryGetValue(politico.territorioId, out int novoDono)) return novoDono;
                return Mathf.Max(0, politico.ownerCountryTeamId);
            }
            if (EhAguasInternacionais(ponto)) return 0;
        }

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
