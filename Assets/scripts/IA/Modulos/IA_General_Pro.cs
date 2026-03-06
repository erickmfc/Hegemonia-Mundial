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
    public int soldadosDesejados = 12;
    public int tanquesDesejados = 10;
    public int helicopterosDesejados = 4;
    public int naviosDesejados = 3; // Nova meta naval
    public int transportesDesejados = 2; // Meta de caminhões/Jeeps

    [Header("Agressividade")]
    public int minimoParaAtacar = 5;
    public float intervaloAtaque = 15f;

    // Listas de Controle
    private List<GameObject> grupoSoldados = new List<GameObject>();
    private List<GameObject> grupoTanques = new List<GameObject>();
    private List<GameObject> grupoHelis = new List<GameObject>();
    private List<GameObject> grupoNavios = new List<GameObject>(); // Marinha
    private List<GameObject> grupoTransportes = new List<GameObject>(); // Transportes Terrestres
    private List<GameObject> grupoOutros = new List<GameObject>();

    [SerializeField] private List<Fabrica> minhasFabricas = new List<Fabrica>();
    [SerializeField] private List<Estaleiro> meusEstaleiros = new List<Estaleiro>(); // Lista separada para Estaleiros se tiver script específico
    [SerializeField] private List<Heliporto> meusHeliportos = new List<Heliporto>();

    private float _timerRecrutamento;
    private float _timerAtaque;
    private float _timerReorganizar;
    private float _timerTransporte;
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

        // 4. Transporte Inteligente
        _timerTransporte += Time.deltaTime;
        if (_timerTransporte >= 3.0f)
        {
            _timerTransporte = 0;
            GerenciarTransportes();
        }
    }

    void GerenciarTransportes()
    {
        Transform alvo = BuscarAlvo();
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        // HELICOPTEROS
        foreach(var hObj in grupoHelis)
        {
            if (hObj == null) continue;
            Helicoptero heli = hObj.GetComponent<Helicoptero>();
            if (heli)
            {
                if (alvo != null && Vector3.Distance(heli.transform.position, alvo.position) < 90f)
                {
                    // Chegou no front. Desembarcar tropas para atacar
                    if (heli.TemSoldados()) heli.OrdemPousoOuDesembarque();
                }
                else if (Vector3.Distance(heli.transform.position, centro) < 120f)
                {
                    // Na base. Recolher tropas antes do ataque
                    if (heli.TemEspaco() > 0 && !jaAtacou) heli.ChamarReforcos();
                }
            }
        }
        
        // TRANSPORTES NAVAIS
        foreach(var nObj in grupoNavios)
        {
            if (nObj == null) continue;
            HovercraftTransporte hover = nObj.GetComponent<HovercraftTransporte>();
            if (hover)
            {
                if (alvo != null && Vector3.Distance(hover.transform.position, alvo.position) < 90f)
                {
                    if (hover.TemCarga()) hover.IniciarDesembarque();
                }
                else if (Vector3.Distance(hover.transform.position, centro) < 150f)
                {
                    if (hover.TemEspacoLivre() && !jaAtacou) hover.IniciarEmbarque();
                }
            }
        }

        // TRANSPORTES TERRESTRES (CAMINHÕES E JEEPS)
        foreach(var tObj in grupoTransportes)
        {
            if (tObj == null) continue;
            TransporteTerrestre transp = tObj.GetComponent<TransporteTerrestre>();
            if (transp)
            {
                if (alvo != null && Vector3.Distance(transp.transform.position, alvo.position) < 60f)
                {
                    // Chegou na zona de guerra, solta a galera
                    if (transp.TemPassageiros) transp.DesembarcarTudo();
                }
                else if (Vector3.Distance(transp.transform.position, centro) < 150f)
                {
                    // Na base, se prepara recolhendo
                    if (!transp.EstaCheio() && !jaAtacou && grupoSoldados.Count > 0) 
                    {
                        transp.TentarEmbarcar();
                    }
                }
            }
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

        // 1. SOBREVIVÊNCIA BÁSICA (Garante mínimo de defesas)
        if (grupoSoldados.Count < 4 && temQuartel)
        {
            if (ComprarUnidade(true, false, false, "Soldado", "Rifle", "Infantaria", "Sniper", "Fuzileiro")) { _ultimoStatus = "🎖️ Recrutando Soldado"; return; }
        }
        
        // 2. FORÇA DE CHOQUE BÁSICA (Pelo menos 1 blindado)
        if (grupoTanques.Count < 2)
        {
             if (temFabricaVeiculos)
             {
                 if (ComprarUnidade(false, false, false, "Tank", "Tanque", "Leopard", "Blindado", "Leonc", "Hack", "UBU")) { _ultimoStatus = "🚜 Recrutando Tanque Prioritário"; return; }
             }
             else if (grupoTanques.Count == 0)
             {
                 _ultimoStatus = "⚠️ Esperando Fábrica de Veículos!";
             }
        }

        // 3. TRANSPORTES TERRESTRES (Caminhão/Hamer)
        if (grupoTransportes.Count < transportesDesejados && temFabricaVeiculos && chefe.dinheiro > 100)
        {
             if (ComprarUnidade(false, false, false, "Caminhao", "Hamer", "Jeep", "Transporte")) { _ultimoStatus = "🚚 Recrutando Transporte"; return; }
        }

        // 4. PREENCHER TROPAS (O resto dos soldados)
        if (grupoSoldados.Count < soldadosDesejados && temQuartel)
        {
            if (ComprarUnidade(true, false, false, "Soldado", "Rifle", "Infantaria", "Sniper", "Fuzileiro")) { _ultimoStatus = "🎖️ Recrutando Soldado Extra"; return; }
        }

        // 5. PREENCHER TANQUES (O resto dos veículos pesados)
        if (grupoTanques.Count < tanquesDesejados && temFabricaVeiculos)
        {
             if (ComprarUnidade(false, false, false, "Tank", "Tanque", "Leopard", "Blindado", "Leonc", "Hack", "UBU", "Panzer")) { _ultimoStatus = "🚜 Recrutando Tanque Secundário"; return; }
        }

        // 6. HELICÓPTEROS
        if (grupoHelis.Count < helicopterosDesejados && chefe.dinheiro > 600)
        {
             if (ComprarUnidade(false, false, true, "Heli", "Apache", "Cobra", "Falcon", "Ray", "Aviao")) { _ultimoStatus = "🚁 Recrutando Aéreo"; return; }
        }

        // 7. NAVIOS
        if (grupoNavios.Count < naviosDesejados && chefe.dinheiro > 800)
        {
            if (temEstaleiro)
            {
                bool temCarrier = grupoNavios.Any(n => n.name.ToLower().Contains("liberty") || n.name.ToLower().Contains("carrier") || n.name.ToLower().Contains("transporte"));
                if (chefe.dinheiro > 1800 && !temCarrier)
                {
                    if (ComprarUnidade(false, true, false, "Liberty", "Prime", "Carrier", "Transporte")) 
                    { _ultimoStatus = "⚓ Construindo Porta-Aviões (Liberty)..."; return; }
                }

                if (ComprarUnidade(false, true, false, "Fragata", "Corveta", "Submarino", "Navio", "Barco", "Hovercraft")) 
                { _ultimoStatus = "⚓ Construindo Navio..."; return; }
            }
        }

        // 8. SOBRA DE DINHEIRO (Reforços Aleatórios e Inifinitos)
        if (chefe.dinheiro > 2000)
        {
             if (Random.value > 0.4f && temFabricaVeiculos) ComprarUnidade(false, false, false, "Tank", "Tanque", "Blindado");
             else if (temQuartel) ComprarUnidade(true, false, false, "Soldado", "Infantaria");
        }
    }

    // =============================================
    // RECRUTAMENTO DE CIVIS (MÉTODO PÚBLICO)
    // =============================================
    public void RecrutarTurista()
    {
        // Tenta comprar algo civil
        if (ComprarUnidade(false, false, false, "civil", "turista", "caminhonete", "kombi", "onibus"))
        {
            _ultimoStatus = "🤝 Despachando Transporte Civil/Turistas para a fronteira.";
        }
    }

    bool ComprarUnidade(bool requerQuartel, bool ehNaval, bool ehHeli, params string[] keywords)
    {
        AtualizarListasDeFabricas(); 

        // Acha fábrica ou estaleiro
        Fabrica fabricaEscolhida = null;
        Estaleiro estaleiroEscolhido = null;
        Heliporto heliportoEscolhido = null;

        if (ehHeli)
        {
            if (meusHeliportos.Count > 0) heliportoEscolhido = meusHeliportos[0];
            else return false; // Sem heliporto = sem helicóptero! (Para a IA também)
        }
        else if (ehNaval)
        {
            if (meusEstaleiros.Count > 0) estaleiroEscolhido = meusEstaleiros[0]; 
            else fabricaEscolhida = minhasFabricas.FirstOrDefault(f => EhNaval(f));
        }
        else
        {
             fabricaEscolhida = minhasFabricas.FirstOrDefault(f => f.ehQuartel == requerQuartel && !EhNaval(f));
        }

        if (fabricaEscolhida == null && estaleiroEscolhido == null && heliportoEscolhido == null) return false;

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
            if (ehHeli && heliportoEscolhido != null)
            {
                Vector3 posPouso = heliportoEscolhido.ObterPontoDePousoMundial();
                posPouso += Vector3.up * 1.5f; // Garante que não clipe
                novo = Instantiate(itemEscolhido.prefabDaUnidade, posPouso, heliportoEscolhido.transform.rotation);
                
                // Dá o RG pro helicóptero ser do time da IA
                IdentidadeUnidade id = novo.GetComponent<IdentidadeUnidade>();
                if(id == null) id = novo.AddComponent<IdentidadeUnidade>();
                id.teamID = chefe.identidade.teamID;
                if (!string.IsNullOrEmpty(chefe.identidade.nomeComandante)) id.nomeDoPais = chefe.identidade.nomeComandante;

                RegistrarUnidade(novo);
                MoverUnidade(novo, CalcularPontoDeEncontro());
                _ultimoStatus = $"🚁 Desdobrando: {itemEscolhido.nomeItem}";
                return true;
            }
            else if (estaleiroEscolhido != null)
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

    private float _ultimoScanGlobal = -100f; // Cooldown para performance

    void AtualizarListasDeFabricas()
    {
        // Limpa listas para evitar nulls (rápido e não custa muito processamento)
        minhasFabricas.RemoveAll(f => f == null);
        meusEstaleiros.RemoveAll(e => e == null);
        meusHeliportos.RemoveAll(h => h == null);

        // OTIMIZAÇÃO: Evita fazer o Scan Global caríssimo da Unity se acabamos de fazer
        // (Especialmente num loop onde a IA compra 4 coisas na mesma fração de segundo)
        if (Time.time - _ultimoScanGlobal < 5.0f) return;
        _ultimoScanGlobal = Time.time;

        // Re-scan global
        var fabs = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach(var f in fabs) RegistrarFabrica(f);
        
        var ests = FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
        foreach(var e in ests) RegistrarEstaleiro(e);
        
        var helis = FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
        foreach(var h in helis) RegistrarHeliporto(h);
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
        
        MoverEmFormacao(grupoTransportes, destino - dir * 5f, dir, 8f);
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
        if(grupoTanques.Contains(u) || grupoSoldados.Contains(u) || grupoNavios.Contains(u) || grupoHelis.Contains(u) || grupoTransportes.Contains(u) || chefe.meusCivis.Contains(u)) return;

        string n = u.name.ToLower();

        // Checa se é civil
        if (n.Contains("civil") || n.Contains("turista") || n.Contains("onibus") || n.Contains("kombi"))
        {
            chefe.meusCivis.Add(u);
            
            // Mandá-lo viajar até a prefeitura do jogador:
            if (chefe.alvoAtaquePrincipal != null)
            {
                MoverUnidade(u, chefe.alvoAtaquePrincipal.position);
            }
            return; // Civis não vão para os grupos táticos
        }
        
        if(u.GetComponent<TransporteTerrestre>() != null) grupoTransportes.Add(u);
        else if(n.Contains("navio") || n.Contains("fragata") || n.Contains("corveta") || n.Contains("sub") || n.Contains("carrier") || n.Contains("liberty") || n.Contains("transporte naval") || n.Contains("hovercraft")) grupoNavios.Add(u);
        else if(n.Contains("tanque") || n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado") || n.Contains("leonc") || n.Contains("hack") || n.Contains("ubu")) grupoTanques.Add(u);
        else if(n.Contains("heli") || n.Contains("apache") || n.Contains("cobra") || n.Contains("falcon") || n.Contains("ray")) grupoHelis.Add(u);
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

    public void RegistrarHeliporto(Heliporto h)
    {
        if(h == null) return;
        
        var id = h.GetComponent<IdentidadeUnidade>();
        int meuTime = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; 

        if(id == null) 
        {
             if(Vector3.Distance(transform.position, h.transform.position) < 150) 
             {
                 id = h.gameObject.AddComponent<IdentidadeUnidade>();
                 id.teamID = meuTime;
             }
        }
        else if (id.teamID == 0) // Neutro?
        {
             if(Vector3.Distance(transform.position, h.transform.position) < 150) id.teamID = meuTime;
        }

        if(id != null && id.teamID == meuTime && !meusHeliportos.Contains(h))
            meusHeliportos.Add(h);
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
        grupoTransportes.RemoveAll(u => u == null);
        grupoNavios.RemoveAll(u => u == null);
        grupoHelis.RemoveAll(u => u == null);
        minhasFabricas.RemoveAll(f => f == null);
        meusEstaleiros.RemoveAll(e => e == null);
        meusHeliportos.RemoveAll(h => h == null);
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

        var heli = u.GetComponent<Helicoptero>();
        if (heli != null) 
        { 
            heli.Decolar(destino); 
            return; 
        }

        // NÃO interrompe o soldado se ele já estiver correndo para embarcar num helicóptero!
        if (Helicoptero.SoldadoEstaEmbarcando(u)) return;

        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl) { ctrl.MoverParaPonto(destino); return; }

        var nav = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav && nav.isOnNavMesh) { nav.SetDestination(destino); nav.isStopped = false; }
    }

    public Transform BuscarAlvo()
    {
        var alvos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(a => a.teamID == 1).ToList();
        if (alvos.Count == 0) return null;

        // 1. Prefeitura (Alvo Primário Supremo)
        var prefeitura = alvos.FirstOrDefault(a => a.name.ToLower().Contains("prefeitura") || a.GetComponent("ComplexoGovernamental") != null);
        if (prefeitura != null) return prefeitura.transform;

        // 2. Quartel General (Alvo Secundário Militar)
        var quartel = alvos.FirstOrDefault(a => 
        {
            var f = a.GetComponent<Fabrica>();
            return (f != null && f.ehQuartel) || a.name.ToLower().Contains("quartel");
        });
        if (quartel != null) return quartel.transform;

        // 3. Outras Fábricas e Estruturas Estratégicas
        var predio = alvos.FirstOrDefault(a => a.GetComponent<Fabrica>() != null || a.GetComponent<Estaleiro>() != null || a.GetComponent<Heliporto>() != null);
        if (predio != null) return predio.transform;

        // 4. Se não achou nenhum prédio, ataca a primeira unidade civil/militar que vir pela frente
        return alvos[0].transform;
    }
    
    void MoverTropasParaPontoDeEncontro()
    {
        if(chefe.basePrincipal == null) return;
        MoverEmFormacao(grupoTransportes, CalcularPontoDeEncontro() - chefe.basePrincipal.forward * 5f, chefe.basePrincipal.forward, 8f);
        MoverEmFormacao(grupoTanques, CalcularPontoDeEncontro(), chefe.basePrincipal.forward, 6f);
        MoverEmFormacao(grupoSoldados, CalcularPontoDeEncontro() - chefe.basePrincipal.forward * 10f, chefe.basePrincipal.forward, 3f);
    }
    
    int TotalUnidades() => grupoSoldados.Count + grupoTanques.Count + grupoHelis.Count + grupoNavios.Count + grupoTransportes.Count;

    // OnGUI de debug removido — poluía a tela sobrepondo o HUD de recursos.
    // Para ver status da IA, use o Console (Debug.Log) ou o Inspector.
}
