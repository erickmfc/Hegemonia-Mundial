using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// IA General Pro: Gerencia recrutamento contínuo e combate ativo (Terra, Ar e MAR).
/// </summary>
public class IA_General_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    
    [Header("Composição Desejada")]
    public int soldadosDesejados = 8;
    public int tanquesDesejados = 3;
    public int helicopterosDesejados = 2;
    public int naviosDesejados = 2; // Nova meta naval

    [Header("Agressividade")]
    public int minimoParaAtacar = 5;
    public float intervaloAtaque = 15f;

    // Listas de Controle
    private List<GameObject> grupoSoldados = new List<GameObject>();
    private List<GameObject> grupoTanques = new List<GameObject>();
    private List<GameObject> grupoHelis = new List<GameObject>();
    private List<GameObject> grupoNavios = new List<GameObject>(); // Marinha
    private List<GameObject> grupoOutros = new List<GameObject>();

    [SerializeField] private List<Fabrica> minhasFabricas = new List<Fabrica>();
    [SerializeField] private List<Estaleiro> meusEstaleiros = new List<Estaleiro>(); // Lista separada para Estaleiros se tiver script específico

    private float _timerRecrutamento;
    private float _timerAtaque;
    private float _timerReorganizar;
    private string _ultimoStatus = "Iniciando...";

    private bool jaAtacou = false;
    private Vector3 ultimoAlvoPosicao;

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Start()
    {
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        if (chefe == null) 
        {
             // Tenta achar na cena
             chefe = FindFirstObjectByType<IA_Comandante>();
        }
        if (chefe != null && chefe.cerebroGeneral == null) chefe.cerebroGeneral = this;
    }

    void Update()
    {
        if (chefe == null) return;

        // 1. Recrutamento
        _timerRecrutamento += Time.deltaTime;
        if (_timerRecrutamento >= 2.0f)
        {
            _timerRecrutamento = 0;
            TentarRecrutar();
        }

        // 2. Reorganizar
        _timerReorganizar += Time.deltaTime;
        if (_timerReorganizar >= 5.0f)
        {
            _timerReorganizar = 0;
            LimparMortos();
            if (!jaAtacou) MoverTropasParaPontoDeEncontro();
        }

        // 3. Combate
        _timerAtaque += Time.deltaTime;
        if (_timerAtaque >= intervaloAtaque)
        {
            _timerAtaque = 0;
            AvaliarCombate();
        }
    }

    // =============================================
    // RECRUTAMENTO
    // =============================================
    void TentarRecrutar()
    {
        if (chefe.dinheiro < 100) 
        {
            _ultimoStatus = "💰 Sem dinheiro ($" + (int)chefe.dinheiro + ")";
            return; 
        }

        LimparMortos(); // Importante limpar nulos antes de contar

        bool temQuartel = minhasFabricas.Any(f => f.ehQuartel);
        bool temFabricaVeiculos = minhasFabricas.Any(f => !f.ehQuartel && !EhNaval(f));
        bool temEstaleiro = meusEstaleiros.Count > 0 || minhasFabricas.Any(f => EhNaval(f));

        // 1. SOLDADOS
        if (grupoSoldados.Count < soldadosDesejados)
        {
            if (temQuartel)
            {
                if (ComprarUnidade(true, false, "Soldado", "Rifle", "Infantaria")) { _ultimoStatus = "🎖️ Recrutando Soldado"; return; }
            }
        }

        // 2. NAVIOS (Prioridade se tiver estaleiro)
        if (grupoNavios.Count < naviosDesejados && chefe.dinheiro > 1000)
        {
            if (temEstaleiro)
            {
                // Tenta primeiro o grandão (Liberty Prime/Carrier) se tiver grana
                bool temCarrier = grupoNavios.Any(n => n.name.ToLower().Contains("liberty") || n.name.ToLower().Contains("carrier") || n.name.ToLower().Contains("transporte"));
                
                if (chefe.dinheiro > 2200 && !temCarrier)
                {
                    // Tenta Liberty, Carrier ou Transporte
                    if (ComprarUnidade(false, true, "Liberty", "Prime", "Carrier", "Transporte")) 
                    { 
                        _ultimoStatus = "⚓ Construindo Porta-Aviões (Liberty)..."; 
                        return; 
                    }
                }

                // Senão vai nos comuns
                if (ComprarUnidade(false, true, "Fragata", "Corveta", "Submarino", "Navio", "Barco")) 
                { 
                    _ultimoStatus = "⚓ Construindo Navio..."; 
                    return; 
                }
            }
        }

        // 3. TANQUES
        if (grupoTanques.Count < tanquesDesejados && chefe.dinheiro > 800)
        {
             if (temFabricaVeiculos)
             {
                 if (ComprarUnidade(false, false, "Tanque", "Leopard", "Blindado")) { _ultimoStatus = "🚜 Recrutando Tanque"; return; }
             }
             else if (!temFabricaVeiculos && grupoTanques.Count == 0)
             {
                 _ultimoStatus = "⚠️ Preciso de Fábrica de Veículos!";
             }
        }

        // 4. HELICÓPTEROS
        if (grupoHelis.Count < helicopterosDesejados && chefe.dinheiro > 1200)
        {
             if (ComprarUnidade(false, false, "Heli", "Apache", "Cobra")) { _ultimoStatus = "🚁 Recrutando Heli"; return; }
        }

        // Sobra de dinheiro = Reforços aleatórios
        if (chefe.dinheiro > 3000)
        {
             if (Random.value > 0.5f && temFabricaVeiculos) ComprarUnidade(false, false, "Tanque");
             else if (temQuartel) ComprarUnidade(true, false, "Soldado");
        }
    }

    bool ComprarUnidade(bool requerQuartel, bool ehNaval, params string[] keywords)
    {
        AtualizarListasDeFabricas(); 

        // Acha fábrica ou estaleiro
        Fabrica fabricaEscolhida = null;
        Estaleiro estaleiroEscolhido = null;

        if (ehNaval)
        {
            if (meusEstaleiros.Count > 0) estaleiroEscolhido = meusEstaleiros[0]; 
            else fabricaEscolhida = minhasFabricas.FirstOrDefault(f => EhNaval(f));
        }
        else
        {
             fabricaEscolhida = minhasFabricas.FirstOrDefault(f => f.ehQuartel == requerQuartel && !EhNaval(f));
        }

        if (fabricaEscolhida == null && estaleiroEscolhido == null) return false;

        // Acha Prefab no Catálogo
        if (MenuConstrucao.catalogoGlobal == null) return false;

        DadosConstrucao itemEscolhido = null;
        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
            if (item == null || item.prefabDaUnidade == null) continue;
            if (item.preco > chefe.dinheiro) continue;

            string nome = item.nomeItem.ToLower();
            bool itemEhNaval = nome.Contains("navio") || nome.Contains("barco") || nome.Contains("sub") || 
                               nome.Contains("carrier") || nome.Contains("liberty") || nome.Contains("transporte") ||
                               nome.Contains("hovercraft") || nome.Contains("estaleiro") == false && item.categoria == DadosConstrucao.CategoriaItem.Marinha;

            // Se eu quero naval, o item TEM que ser naval. 
            // Se eu quero terrestre (não naval), o item NÃO PODE ser naval.
            if (ehNaval != itemEhNaval) continue;

            foreach (var k in keywords)
            {
                if (nome.Contains(k.ToLower())) 
                {
                    itemEscolhido = item;
                    break;
                }
            }
            if (itemEscolhido != null) break;
        }

        if (itemEscolhido == null) return false;

        // Tenta Produzir
        if (chefe.GastarDinheiro(itemEscolhido.preco))
        {
            GameObject novo = null;
            if (estaleiroEscolhido != null)
            {
                if (estaleiroEscolhido.ConstruirUnidade(itemEscolhido.prefabDaUnidade))
                {
                    _ultimoStatus = $"⚓ Ordem dada ao Estaleiro: {itemEscolhido.nomeItem}";
                    // O estaleiro vai instanciar depois
                    return true;
                }
            }
            else if (fabricaEscolhida != null)
            {
                novo = fabricaEscolhida.ProduzirUnidade(itemEscolhido.prefabDaUnidade);
                if (novo != null)
                {
                    RegistrarUnidade(novo);
                    if(!ehNaval) MoverUnidade(novo, CalcularPontoDeEncontro());
                    return true;
                }
            }

            // Devolução se falhar
            chefe.AdicionarDinheiro(itemEscolhido.preco);
        }
        return false;
    }

     void AtualizarListasDeFabricas()
    {
        // Limpa listas para evitar nulls
        minhasFabricas.RemoveAll(f => f == null);
        meusEstaleiros.RemoveAll(e => e == null);

        // Re-scan global rápido
        var fabs = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach(var f in fabs) RegistrarFabrica(f);
        
        var ests = FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
        foreach(var e in ests) RegistrarEstaleiro(e);
    }

    // =============================================
    // COMBATE
    // =============================================
    void AvaliarCombate()
    {
        if (TotalUnidades() < minimoParaAtacar) return;
        
        Transform alvo = BuscarAlvo();
        if (alvo == null) return;

        _ultimoStatus = "⚔️ ATAQUE TOTAL!";
        jaAtacou = true;
        
        // Terra/Ar
        LancarAtaqueCoordenado(alvo.position);
        
        // Mar (Ataque independente)
        if (grupoNavios.Count > 0)
        {
             Vector3 ataqueNaval = alvo.position;
             ataqueNaval.y = 0; // Nível do mar
             MoverGrupo(grupoNavios, ataqueNaval);
        }
    }

    void LancarAtaqueCoordenado(Vector3 destino)
    {
        Vector3 dir = (destino - chefe.transform.position).normalized;
        if(dir == Vector3.zero) dir = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        
        MoverEmFormacao(grupoTanques, destino, dir, 6f);
        MoverEmFormacao(grupoSoldados, destino - dir * 15f + right * 15f, dir, 3f);
        MoverEmFormacao(grupoSoldados, destino - dir * 15f - right * 15f, dir, 3f);
        MoverEmFormacao(grupoHelis, destino, dir, 15f);
    }

    // =============================================
    // REGISTROS E UTIL
    // =============================================
    
    public void RegistrarUnidade(GameObject u)
    {
        if (u == null) return;
        if(grupoTanques.Contains(u) || grupoSoldados.Contains(u) || grupoNavios.Contains(u) || grupoHelis.Contains(u)) return;

        string n = u.name.ToLower();
        if(n.Contains("navio") || n.Contains("fragata") || n.Contains("corveta") || n.Contains("sub") || n.Contains("carrier") || n.Contains("liberty") || n.Contains("transporte")) grupoNavios.Add(u);
        else if(n.Contains("tanque") || n.Contains("leopard") || n.Contains("blindado")) grupoTanques.Add(u);
        else if(n.Contains("heli") || n.Contains("apache") || n.Contains("cobra")) grupoHelis.Add(u);
        else grupoSoldados.Add(u);
    }

    public void RegistrarSoldado(GameObject u)
    {
        RegistrarUnidade(u);
    }

    public void RegistrarFabrica(Fabrica f)
    {
         if(f == null) return;
         var id = f.GetComponent<IdentidadeUnidade>();
         // Se não tiver identidade, assume que é meu se estiver perto?? Não, melhor exigir identidade
         if(id != null && id.teamID == chefe.identidade.teamID && !minhasFabricas.Contains(f))
            minhasFabricas.Add(f);
    }
    
    public void RegistrarEstaleiro(Estaleiro e)
    {
        if(e == null) return;
        
        var id = e.GetComponent<IdentidadeUnidade>();
        int meuTime = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; // Default Inimigo = 2

        // Fallback: se estaleiro nao tem identidade, assume meu temporariamente se estiver < 100m
        if(id == null) 
        {
             // Cuidado para não roubar estaleiro do player (TeamID 1)
             // Só assume se não tiver ID nenhum E estiver perto
             if(Vector3.Distance(transform.position, e.transform.position) < 150) 
             {
                 id = e.gameObject.AddComponent<IdentidadeUnidade>();
                 id.teamID = meuTime;
             }
        }
        else if (id.teamID == 0) // Neutro?
        {
             if(Vector3.Distance(transform.position, e.transform.position) < 150) id.teamID = meuTime;
        }

        if(id != null && id.teamID == meuTime && !meusEstaleiros.Contains(e))
            meusEstaleiros.Add(e);
    }

    bool EhNaval(MonoBehaviour b) 
    {
        if(b == null) return false;
        string nomes = b.name.ToLower();
        return nomes.Contains("naval") || nomes.Contains("pier") || nomes.Contains("estaleiro");
    }

    void LimparMortos()
    {
        grupoTanques.RemoveAll(u => u == null);
        grupoSoldados.RemoveAll(u => u == null);
        grupoNavios.RemoveAll(u => u == null);
        grupoHelis.RemoveAll(u => u == null);
        minhasFabricas.RemoveAll(f => f == null);
        meusEstaleiros.RemoveAll(e => e == null);
    }

    // ... (Métodos auxiliares)
    Vector3 CalcularPontoDeEncontro()
    {
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : chefe.transform.position;
        return centro + Vector3.forward * 30f;
    }
        
    void MoverEmFormacao(List<GameObject> grupo, Vector3 centro, Vector3 direcaoFrente, float espacamento)
    {
        if (grupo.Count == 0) return;
        int colunas = Mathf.CeilToInt(Mathf.Sqrt(grupo.Count)); 
        Vector3 direita = Vector3.Cross(Vector3.up, direcaoFrente).normalized;

        for (int i = 0; i < grupo.Count; i++)
        {
            if (grupo[i] == null) continue;
            int linha = i / colunas;
            int col = i % colunas;
            Vector3 pos = centro + (direita * (col - colunas/2f) * espacamento) - (direcaoFrente * linha * espacamento);
            MoverUnidade(grupo[i], pos);
        }
    }

    void MoverGrupo(List<GameObject> grupo, Vector3 destino) { foreach(var u in grupo) MoverUnidade(u, destino); }

    void MoverUnidade(GameObject u, Vector3 destino)
    {
        if (u == null) return;
        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl) { ctrl.MoverParaPonto(destino); return; }
        var nav = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav && nav.isOnNavMesh) { nav.SetDestination(destino); nav.isStopped = false; }
    }

    Transform BuscarAlvo()
    {
        var alvos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach(var a in alvos) if(a.teamID == 1) return a.transform;
        return null; // ou retorna uma base neutra
    }
    
    void MoverTropasParaPontoDeEncontro()
    {
        if(chefe.basePrincipal == null) return;
        MoverEmFormacao(grupoTanques, CalcularPontoDeEncontro(), chefe.basePrincipal.forward, 6f);
        MoverEmFormacao(grupoSoldados, CalcularPontoDeEncontro() - chefe.basePrincipal.forward * 10f, chefe.basePrincipal.forward, 3f);
    }
    
    int TotalUnidades() => grupoSoldados.Count + grupoTanques.Count + grupoHelis.Count + grupoNavios.Count;

    // OnGUI de debug removido — poluía a tela sobrepondo o HUD de recursos.
    // Para ver status da IA, use o Console (Debug.Log) ou o Inspector.
}
