using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Linq;

/// <summary>
/// IA Arquiteto Pro: Responsável por urbanismo militar.
/// Constrói base com layout aberto e espaçado, SEM prender unidades.
/// </summary>
public class IA_Arquiteto_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    private bool baseIniciada = false;

    [Header("Configurações de Construção")]
    public float espacamentoEdificios = 25f; // Aumentado para evitar aperto
    public float nivelDoMar = 0f; // Altura da água para estaleiros

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Start()
    {
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        
        // Aguarda um pouco para garantir que o catálogo do MenuConstrucao carregou
        Invoke("PlanejarBaseMilitar", 4.0f);

        // --- MANUTENÇÃO DE BASE ---
        // Verifica a cada 15 segundos se a base está intacta e expande se tiver dinheiro
        InvokeRepeating("VerificarIntegridadeEExpandir", 15.0f, 15.0f);
    }

    void VerificarIntegridadeEExpandir()
    {
        if (!baseIniciada || chefe == null) return;

        Debug.Log("🏗️ [IA Arquiteto] Verificando base...");
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        // 1. QUARTEL (Soldados)
        if (!ExistePredio("Quartel") && !ExistePredio("Tenda"))
        {
            ConstruirNaTerra("Tenda", centro, 500); 
        }

        // 2. FÁBRICA DE VEÍCULOS (Tanques)
        if (!ExistePredio("Veiculos") && !ExistePredio("Hangar"))
        {
            ConstruirNaTerra("Veiculos", centro, 800); 
        }
        else if (ContarPredios("Veiculos") < 4 && chefe.dinheiro > 1800f)
        {
            // O jogador tem dinheiro, cria mais fábricas DITANTES (50m+)
            Debug.Log("🏗️ [IA Arquiteto] Economia Forte! Expandindo base com novo Hangar de Veículos e Tropas.");
            ConstruirNaTerra("Veiculos", centro, 1000, 50f);
        }


        // 4. HELIPORTO E AEROPORTO
        if (!ExistePredio("Aeroporto") && chefe.dinheiro >= 500f)
        {
            // O aeroporto deve estar MUITO LONGE (240 metros de espaçamento para ficar no limite da linha verde)
            Debug.Log("🏗️ [IA Arquiteto] Economia Forte! Projetando construção do Aeroporto Tático Militar...");
            ConstruirNaTerra("Aeroporto", centro, 500, 240f); 
        }
        else if (!ExistePredio("Heliporto") && chefe.dinheiro >= 3000f)
        {
            ConstruirNaTerra("Heliporto", centro, 3000, 30f); 
        }
        // 4. ESTALEIRO (Navais)
        // Se tiver grana pro estaleiro (que custa 2500 no menu, então > 2500)
        if (chefe.dinheiro > 2200 && !ExistePredio("Estaleiro") && !ExistePredio("Naval"))
        {
            Debug.Log("🏗️ [IA Arquiteto] Tentando encontrar litoral para Estaleiro...");
            Vector3 posAgua = EncontrarAgua(centro, 40f, 150f);
            if (posAgua != Vector3.zero)
            {
                 Debug.Log($"🏗️ [IA Arquiteto] Água encontrada em {posAgua}! Construindo Estaleiro.");
                 Vector3 dirMar = (posAgua - centro).normalized;
                 ConstruirNaAgua("Estaleiro", posAgua, dirMar);
            }
        }

        // 5. CINTURÃO DE DEFESA (Torretas e AA)
        if (chefe.dinheiro > 1000)
        {
            // O catálogo geralmente tem nomes como "TorretaAntiaerea", "Torreta Terra", etc
            int qtdAA = ContarPredios("Antiaerea") + ContarPredios("Aerea") + ContarPredios("AA");
            int qtdSolo = ContarPredios("Torreta") + ContarPredios("Defesa"); 
            // Subtrair AA das terrestres caso o nome "Torreta" englobe as "TorretaAntiaerea"
            qtdSolo -= qtdAA; 
            if (qtdSolo < 0) qtdSolo = 0;

            // Prioriza o céu se não tiver NENHUMA
            if (qtdAA < 3 && chefe.dinheiro >= 800)
            {
                Debug.Log("🏗️ [IA Arquiteto] Protegendo espaço aéreo do General! Construindo Bateria Antiaérea.");
                ConstruirDefesaInteligente("Antiaerea", centro, 800);
            }
            // Depois as terrestres
            else if (qtdSolo < 5 && chefe.dinheiro >= 500)
            {
                Debug.Log("🏗️ [IA Arquiteto] Reforçando o perímetro terrestre! Construindo Torreta/Bunker.");
                ConstruirDefesaInteligente("Torreta", centro, 500);
            }
        }
    }

    int ContarPredios(string nomeParcial)
    {
        if (chefe == null || chefe.identidade == null) return 0;
        int count = 0;

        Fabrica[] fabricas = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach (var f in fabricas)
        {
            if (f == null) continue;
            var id = f.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == chefe.identidade.teamID) 
            {
                string nomeLimpo = f.name.ToLower();
                if (nomeLimpo.Contains(nomeParcial.ToLower())) count++;
                else if (nomeParcial == "Veiculos" && (nomeLimpo.Contains("hangar") || nomeLimpo.Contains("fabrica") || nomeLimpo.Contains("veiculo"))) count++;
                else if (nomeParcial == "Hangar" && (nomeLimpo.Contains("veiculo") || nomeLimpo.Contains("hangar"))) count++;
                else if (nomeParcial == "Tenda" && (nomeLimpo.Contains("quartel") || nomeLimpo.Contains("tenda"))) count++;
            }
        }
        
        if (nomeParcial.Contains("Estaleiro"))
        {
            Estaleiro[] estaleiros = FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
            foreach(var e in estaleiros) 
            {
                 if(e == null) continue;
                 var id = e.GetComponent<IdentidadeUnidade>();
                 if (id != null && id.teamID == chefe.identidade.teamID) count++;
            }
        }

        if (nomeParcial.Contains("Heliporto"))
        {
            Heliporto[] heliportos = FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
            foreach(var h in heliportos)
            {
                if(h == null) continue;
                var id = h.GetComponent<IdentidadeUnidade>();
                if (id != null && id.teamID == chefe.identidade.teamID) count++;
            }
        }

        return count;
    }

    bool ExistePredio(string nomeParcial)
    {
        return ContarPredios(nomeParcial) > 0;
    }

    void PlanejarBaseMilitar()
    {
        if (baseIniciada) return;

        // Catálogo Check
        if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0)
        {
             Debug.LogWarning("IA Arquiteto: Catálogo vazio. Tentando novamente em 2s.");
             Invoke("PlanejarBaseMilitar", 2.0f);
             return;
        }

        Debug.Log("🏗️ [IA Arquiteto Pro] Iniciando Construção da Base Inicial...");

        Vector3 centro = (chefe != null && chefe.basePrincipal != null) 
            ? chefe.basePrincipal.position 
            : transform.position;

        // 0. Fundar a Capital / Prefeitura primeiro (Soberania Máxima daquele raio)
        if (!ExistePredio("Prefeitura") && !ExistePredio("Complexo")) ConstruirNaTerra("Prefeitura", centro, 0);

        // 0.5 Expandir a borda imediata com uma bandeira proxima do centro (Opcional mas recomendado)
        if (!ExistePredio("Bandeira") && !ExistePredio("Flag")) ConstruirNaTerra("Bandeira", centro, 0);

        // 1. Quartel/Tenda (Prioridade Absoluta)
        if (!ExistePredio("Tenda")) ConstruirNaTerra("Tenda", centro, 0);

        // 2. Fábrica de Veículos
        if (!ExistePredio("Veiculos")) ConstruirNaTerra("Veiculos", centro, 500); 

        // 3. AEROPORTO (Construção Expressa a pedido do General - Construir nos primeiros segundos no limite da fronteira verde!)
        // Ignorar preço de $5.000 para forçar a IA Suprema a tê-lo mesmo com pouco cash inicial 
        if (!ExistePredio("Aeroporto") && chefe.dinheiro >= 100f) 
        {
             Debug.Log("🏗️ [IA Arquiteto] Ordem Expressa do Comando Supremo: Erguendo aeroporto fronteiriço nos 10s iniciais!");
             ConstruirNaTerra("Aeroporto", centro, 100, 240f);
        }



        baseIniciada = true;
    }

    // --- MÉTODOS DE CONSTRUÇÃO ---

    void ConstruirNaTerra(string nomeChave, Vector3 centro, int custoMinimo, float espacamentoCustom = -1f)
    {
        if (chefe == null) return;
        if (chefe.dinheiro < custoMinimo) return;

        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) 
        {
            Debug.LogWarning($"⚠️ [IA Arquiteto] PREFAB FALTANDO: '{nomeChave}' não encontrado no catálogo.");
            return;
        }

        bool ehBandeiraOuPref = nomeChave.ToLower().Contains("bandeira") || nomeChave.ToLower().Contains("flag") || nomeChave.ToLower().Contains("prefeit");
        
        // --- NOVO: Aeroporto é uma instalação furtiva extra-muros ---
        bool ehAeroporto = nomeChave.ToLower().Contains("aeroporto");
        if (ehAeroporto) ehBandeiraOuPref = true; // Imunidade de Território para a Base Aérea (Não sofre embargo do dono!)

        float espMaior = espacamentoCustom > 0f ? espacamentoCustom : espacamentoEdificios;
        
        // Se pedir uma distância imensa (E.g Aeroporto a 600m), nós usamos os 600m para afastar o ponto central, mas a bolha de Colisão para empurrar as árvores e afins fica só em uns 80m. (Para caber a pista larga).
        // Se empurrassemos a colisão inteira de 600m, o Unity nunca acharia um gramado de mais de um 1.2 kilometros vazios.
        float bolhaDeColisao = (espMaior > 150f) ? 100f : espMaior;

        // Tenta 20 vezes achar um lugar livre e LONGE de outros prédios
        for (int i = 0; i < 20; i++)
        {
            Vector3 pos = EncontrarPosicaoEspiral(centro, i, 0f, espMaior);
            
            // Verifica colisão com MARGEM segura para encaixar o prédio
            if (!TemPredioProximo(pos, bolhaDeColisao)) 
            {
                // ============================================
                // REGRAS DE TERRITÓRIO E SOBERANIA PARA A IA
                // ============================================
                if (GerenteDeTerritorio.Instancia != null && !ehBandeiraOuPref)
                {
                    int dono = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(pos);
                    int idInimigo = chefe.identidade.teamID;
                    
                    if (dono != idInimigo)
                    {
                        // Se for terra inimiga (tem dono e não é ela), IA pula para outro local
                        if (dono != 0) continue; 
                        
                        // Se for Neuta (Dono 0), a IA expande sua fronteira!
                        // 1. Tenta construir uma Prefeitura se for uma nova ilha (Permitido pelo Gerente)
                        bool podePrefeitura = GerenteDeTerritorio.Instancia.PodeConstruirPrefeitura(pos);
                        GameObject prefabExpansao = null;
                        int custoExpansao = 0;
                        string nomeAcao = "";

                        if (podePrefeitura && chefe.dinheiro >= 1000f) // Asume custo da prefeitura por volta de 1000
                        {
                            prefabExpansao = BuscarNoCatalogo("Prefeitura");
                            if (prefabExpansao == null) prefabExpansao = BuscarNoCatalogo("Complexo");
                            custoExpansao = 1000;
                            nomeAcao = "Prefeitura / Capital";
                        }

                        // 2. Se não der pra construir prefeitura (ou falta $ ou já tem uma na ilha), usa a Bandeira local
                        if (prefabExpansao == null)
                        {
                            prefabExpansao = BuscarNoCatalogo("Bandeira");
                            if (prefabExpansao == null) prefabExpansao = BuscarNoCatalogo("Flag");
                            custoExpansao = 100; // Custo estimando da bandeira
                            nomeAcao = "Bandeira";
                        }

                        if (prefabExpansao != null && chefe.dinheiro >= custoExpansao) 
                        {
                             Vector3 posBandeira = pos;
                             if (Terrain.activeTerrain != null) posBandeira.y = Terrain.activeTerrain.SampleHeight(posBandeira);
                             
                             Debug.Log($"🚩 [IA Arquiteto] Território virgem detectado! Erguendo {nomeAcao} para reivindicar área.");
                             SpawnarPredio(prefabExpansao, posBandeira, Quaternion.identity);
                        }
                        // IMPORTANTE: Como fundamos uma prefeitura/bandeira que leva tempo para registrar no Gerente,
                        // cancelamos o plano original de construir o prédio AQUI e AGORA para não bugar.
                        // O método de manutenção voltará a tentar construir o prédio no próximo ciclo do Update/Invoke!
                        return; 
                    }
                }

                // Ajusta ao terreno
                float yTerra = 0;
                if (Terrain.activeTerrain != null) yTerra = Terrain.activeTerrain.SampleHeight(pos);
                pos.y = yTerra;

                SpawnarPredio(prefab, pos, Quaternion.identity);

                // O Aeroporto é tão grande que isolamos ele fora da cidade, mas para os mísseis e turrets protegerem ele, fincamos uma prefeitura invisivel ou bandeira de soberania la imediatamente!
                if (ehAeroporto && chefe.dinheiro > 100)
                {
                    GameObject prefabBandeiraX = BuscarNoCatalogo("Bandeira");
                    if (prefabBandeiraX != null)
                    {
                         Vector3 posPostoAvancado = pos + new Vector3(30, 0, 30);
                         if (Terrain.activeTerrain != null) posPostoAvancado.y = Terrain.activeTerrain.SampleHeight(posPostoAvancado);
                         SpawnarPredio(prefabBandeiraX, posPostoAvancado, Quaternion.identity);
                    }
                }
                return;
            }
        }
        
        // SE FALHOU TUDO: Constrói longe numa direção aleatória
        Debug.LogWarning($"⚠️ [IA Arquiteto] Forçando construção de {nomeChave} em ponto distante.");
        Vector3 dirAleatoria = Random.insideUnitSphere;
        dirAleatoria.y = 0;
        Vector3 posForcada = centro + dirAleatoria.normalized * (espacamentoEdificios * 4f);
        if (Terrain.activeTerrain != null) posForcada.y = Terrain.activeTerrain.SampleHeight(posForcada);
        SpawnarPredio(prefab, posForcada, Quaternion.identity);
    }

    void ConstruirDefesaInteligente(string nomeChave, Vector3 centro, int custoMinimo)
    {
        if (chefe == null || chefe.dinheiro < custoMinimo) return;

        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) return;

        int meuTime = chefe.identidade.teamID;

        // Distribui defesas como uma 'rosa dos ventos'
        // Teste de 16 direções diferentes e distâncias variáveis
        for (int i = 0; i < 16; i++)
        {
             float ang = (360f / 16f) * i * Mathf.Deg2Rad;
             // Varia a distância (mais perto para garantir que cabe no raio de território da IA)
             float distanciaBorda = espacamentoEdificios * Random.Range(1.0f, 2.8f); 
             float raioAfastamentoPredio = 15f;
             
             if (nomeChave.ToLower().Contains("anti") || nomeChave.ToLower().Contains("aerea") || nomeChave.ToLower().Contains("ares"))
             {
                 // Empurra a torreta anti-aérea para as extremidades e afasta de construções
                 distanciaBorda = Random.Range(120f, 200f); 
                 raioAfastamentoPredio = 80f; // Evita fortemente construir onde há outros prédios para o míssil não acertar
             }

             Vector3 dirExt = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
             Vector3 posSugerida = centro + (dirExt * distanciaBorda);
             
             if (Terrain.activeTerrain != null) posSugerida.y = Terrain.activeTerrain.SampleHeight(posSugerida);

             // 1. Não pode encavalar e obstruir fábricas/veículos (Raio variável para AA não bater em prédios)
             if (TemPredioProximo(posSugerida, raioAfastamentoPredio)) continue;

             // 2. REGRA DE OURO DA DEFESA: Ela só pode erguer torreta se for DENTRO ou muito perto do Território dela
             // Para não invadir a cidade alheia com uma torreta na parede deles rs
             if (GerenteDeTerritorio.Instancia != null)
             {
                 int dono = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(posSugerida);
                 // Aceita território DELA, ou território neutro muito colado na base dela, mas JAMAIS na base Inimiga.
                 if (dono != meuTime && dono != 0) continue; 
             }
             
             // Achamos o Ponto Tático
             SpawnarPredio(prefab, posSugerida, Quaternion.LookRotation(dirExt));
             return; // Só despacha uma por vez!
        }
    }

    void ConstruirNaAgua(string nomeChave, Vector3 posicaoCosta, Vector3 direcaoMar)
    {
        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) return;

        Quaternion rot = Quaternion.LookRotation(direcaoMar);
        
        // V3: Empurrar 35m para dentro da água.
        Vector3 posFinal = posicaoCosta + (direcaoMar.normalized * 35f); 
        posFinal.y = nivelDoMar; 

        SpawnarPredio(prefab, posFinal, rot);
    }

    Vector3 EncontrarAgua(Vector3 centro, float raioMin, float raioMax)
    {
        // V3 Relaxado: Se não achar água funda (-0.5), aceita água rasa (0.0) se for longe.
        // Isso ajuda em mapas onde o oceano é apenas mesh e o terreno é flat 0.
        
        int tentativas = 36; 
        float raioLimite = Mathf.Max(raioMax, 400f); // Busca bem longe

        for (int i = 0; i < tentativas; i++)
        {
            float angulo = (360f / tentativas) * i * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angulo), 0, Mathf.Sin(angulo));
            bool estavaNaTerra = true;

            for (float dist = raioMin; dist < raioLimite; dist += 10f) // Passos maiores (10m)
            {
                Vector3 pontoTeste = centro + dir * dist;
                float altura = 0f;
                if (Terrain.activeTerrain != null) altura = Terrain.activeTerrain.SampleHeight(pontoTeste);

                // Critério: <= 0.2f (Aceita nível do mar exato ou pouco acima se for praia)
                // Se a água for um plano em Y=0, o terreno embaixo pode ser -10 ou 0.
                bool estaNaAgua = (altura <= nivelDoMar - 0.1f); 

                if (estavaNaTerra && estaNaAgua)
                {
                    Vector3 pontoCosta = pontoTeste - (dir * 5f); 
                    // Aceita qualquer lugar que caiba o estaleiro
                    return pontoCosta;
                }
                estavaNaTerra = !estaNaAgua;
            }
        }
        return Vector3.zero;
    }

    // --- Métodos Recuperados e Fix de Compilação ---

    void SpawnarPredio(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        // GOD MODE: Construção Instantânea para destravar a IA
        GameObject novo = Instantiate(prefab, pos, rot);
        ConfigurarIdentidade(novo);
        if(chefe != null) chefe.GastarDinheiro(200); 
        Debug.Log($"🏗️ [IA Arquiteto] Construção Instantânea REALIZADA: {prefab.name} em {pos}");
    }

    Vector3 EncontrarPosicaoEspiral(Vector3 centro, int indice, float alturaFixa, float espacCustom = -1f)
    {
        float usarEspac = espacCustom > 0f ? espacCustom : espacamentoEdificios;
        // Ângulo áureo (137.5°) — distribui pontos uniformemente sem empilhar
        float angulo = indice * 137.5f;
        // Raio cresce com espaçamento mínimo generoso
        float raio = usarEspac + (indice * usarEspac * 0.8f);
        
        float rad = angulo * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad) * raio, 0, Mathf.Sin(rad) * raio);
        
        return centro + offset;
    }

    bool TemPredioProximo(Vector3 posicao, float raioMinimo)
    {
        // NOVO: GPS e Leitor de BoxCollider/Identidade para impedir TODAS as sobreposições.
        Collider[] vizinhos = Physics.OverlapSphere(posicao, raioMinimo);
        foreach (var col in vizinhos)
        {
            if (col == null || col is TerrainCollider) continue;

            // Busca os componentes no objeto e nos pais
            if (col.GetComponentInParent<Fabrica>() != null) return true;
            if (col.GetComponentInParent<Estaleiro>() != null) return true;
            if (col.GetComponentInParent<Heliporto>() != null) return true;
            
            var danos = col.GetComponentInParent<SistemaDeDanos>();
            if (danos != null && danos.ehEstrutura) return true;
            
            // Avalia unidades paradas e BoxColliders (Evita colocar casa em cima de tanque ou base inimiga)
            if (col.GetComponentInParent<ControleUnidade>() != null) return true;
            if (col.GetComponentInParent<IdentidadeUnidade>() != null) return true;

            // Bloqueio rigoroso de BoxColliders (Qualquer cubo/estrutura física com mais de 3 metros)
            if (col is BoxCollider && col.bounds.size.magnitude > 3f) return true;
        }
        return false;
    }



    void ConfigurarIdentidade(GameObject obj)
    {
        if (obj == null) return;
        var id = obj.GetComponent<IdentidadeUnidade>();
        if (id == null) id = obj.AddComponent<IdentidadeUnidade>();
        
        if (chefe != null && chefe.identidade != null)
        {
            id.teamID = chefe.identidade.teamID;
        }
        else
        {
            id.teamID = 2; // Fallback
        }
        
        // Se for fábrica, registra no General
        var fab = obj.GetComponent<Fabrica>();
        if (fab != null && chefe != null && chefe.cerebroGeneral != null)
        {
            chefe.cerebroGeneral.RegistrarFabrica(fab);
        }
    }

    GameObject BuscarNoCatalogo(string nomeChave)
    {
        if (MenuConstrucao.catalogoGlobal == null) return null;
        
        // Busca
        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
             if (item.nomeItem.ToLower().Contains(nomeChave.ToLower())) return item.prefabDaUnidade;
        }

        // Sinônimos - usando busca iterativa
        if (nomeChave == "Veiculos") 
        {
            foreach (var item in MenuConstrucao.catalogoGlobal) {
                string nm = item.nomeItem.ToLower();
                if (nm.Contains("hangar") || nm.Contains("fabrica") || nm.Contains("factory") || nm.Contains("veiculos")) return item.prefabDaUnidade;
            }
        }
        
        if (nomeChave == "Tenda") 
        {
            foreach (var item in MenuConstrucao.catalogoGlobal) {
                string nm = item.nomeItem.ToLower();
                if (nm.Contains("quartel") || nm.Contains("barraca") || nm.Contains("infantaria") || nm.Contains("tenda")) return item.prefabDaUnidade;
            }
        }

        if (nomeChave == "Antiaerea") 
        {
            foreach (var item in MenuConstrucao.catalogoGlobal) {
                string nm = item.nomeItem.ToLower();
                if (nm.Contains("anti") || nm.Contains("aérea") || nm.Contains("aerea") || nm.Contains("patriot") || nm.Contains("sam") || nm.Contains("missil") || nm.Contains("ares")) return item.prefabDaUnidade;
            }
        }

        if (nomeChave == "Torreta") 
        {
            foreach (var item in MenuConstrucao.catalogoGlobal) {
                string nm = item.nomeItem.ToLower();
                if (nm.Contains("torreta") || nm.Contains("defesa") || nm.Contains("bunker") || nm.Contains("canhao") || nm.Contains("metralhadora")) return item.prefabDaUnidade;
            }
        }
        
        return null;
    }


    public Vector3 EncontrarPontoDefensivo()
    {
        Vector3 centro = (chefe != null && chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;
        Vector3 direcaoAleatoria = Random.onUnitSphere;
        direcaoAleatoria.y = 0;
        return centro + (direcaoAleatoria.normalized * 30f);
    }
}
