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
            Debug.LogWarning("⚠️ [IA Arquiteto] Quartel destruído! Reconstruindo...");
            ConstruirNaTerra("Tenda", centro, 500); // Tenda é o nome no catálogo geralmente
        }

        // 2. FÁBRICA DE VEÍCULOS (Tanques)
        // O nome no menu é "Base de Veiculos". Buscamos por "Veiculos" ou "Hangar".
        if (!ExistePredio("Veiculos") && !ExistePredio("Hangar") && !ExistePredio("Fabrica"))
        {
            Debug.LogWarning("⚠️ [IA Arquiteto] Fábrica de Veículos faltando! Construindo...");
            ConstruirNaTerra("Veiculos", centro, 2000); 
        }

        // 3. HELIPORTO (Aéreos)
        // Facilitar um pouco o acesso aéreo (era 3000)
        if (chefe.dinheiro > 1000 && ContarPredios("Heliporto") < 2)
        {
            Debug.Log("🏗️ [IA Arquiteto] Expandindo: Construindo Heliporto.");
            ConstruirNaTerra("Heliporto", centro, 500);
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
                else if (nomeParcial == "Veiculos" && (nomeLimpo.Contains("hangar") || nomeLimpo.Contains("fabrica"))) count++;
                else if (nomeParcial == "Hangar" && nomeLimpo.Contains("veiculos")) count++;
                else if (nomeParcial == "Tenda" && nomeLimpo.Contains("quartel")) count++;
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
                if (id == null) id = h.GetComponentInParent<IdentidadeUnidade>();
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



        baseIniciada = true;
    }

    // --- MÉTODOS DE CONSTRUÇÃO ---

    void ConstruirNaTerra(string nomeChave, Vector3 centro, int custoMinimo)
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

        // Tenta 20 vezes achar um lugar livre e LONGE de outros prédios
        for (int i = 0; i < 20; i++)
        {
            Vector3 pos = EncontrarPosicaoEspiral(centro, i, 0f);
            
            // Verifica colisão com MARGEM GRANDE (20m) para espalhar
            if (!TemPredioProximo(pos, espacamentoEdificios)) 
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

    Vector3 EncontrarPosicaoEspiral(Vector3 centro, int indice, float alturaFixa)
    {
        // Ângulo áureo (137.5°) — distribui pontos uniformemente sem empilhar
        float angulo = indice * 137.5f;
        // Raio cresce com espaçamento mínimo generoso
        float raio = espacamentoEdificios + (indice * espacamentoEdificios * 0.8f);
        
        float rad = angulo * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad) * raio, 0, Mathf.Sin(rad) * raio);
        
        return centro + offset;
    }

    /// <summary>
    /// Verifica se já existe algum prédio/estrutura dentro do raio.
    /// Usa OverlapSphere para checar colisões reais.
    /// </summary>
    bool TemPredioProximo(Vector3 posicao, float raioMinimo)
    {
        Collider[] vizinhos = Physics.OverlapSphere(posicao, raioMinimo);
        foreach (var col in vizinhos)
        {
            if (col == null) continue;
            // Checa se é uma estrutura (Fábrica, Estaleiro, ou qualquer objeto com IdentidadeUnidade + Estrutura)
            if (col.GetComponent<Fabrica>() != null) return true;
            if (col.GetComponent<Estaleiro>() != null) return true;
            if (col.GetComponent<Heliporto>() != null) return true; // Adicionado para impedir sobreposição de heliportos
            // Checa SistemaDeDanos.ehEstrutura (prédios, muros, etc.)
            var danos = col.GetComponent<SistemaDeDanos>();
            if (danos != null && danos.ehEstrutura) return true;
            // Também rejeita se houver qualquer objeto grande (scale > 3)
            if (col.transform.localScale.magnitude > 5f) return true;
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
