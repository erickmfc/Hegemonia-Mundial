using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// IA Arquiteto Pro: Responsável por urbanismo militar, 
/// criando perímetros defensivos e organização de base.
/// </summary>
public class IA_Arquiteto_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    private bool baseIniciada = false;

    [Header("Configurações de Defesa")]
    public float distanciaMuro = 15f; // Aumentado para 15m para dar espaço
    public float larguraMuro = 4f; // Ajuste conforme o prefab

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Start()
    {
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        Invoke("PlanejarBaseMilitar", 3.0f);
    }

    void PlanejarBaseMilitar()
    {
        if (baseIniciada) return;

        // Catálogo Check
        if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0)
        {
            var menu = FindFirstObjectByType<MenuConstrucao>();
            if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0)
            {
                 Debug.LogWarning("IA Arquiteto: Catálogo vazio. Tentando novamente em 2s.");
                 Invoke("PlanejarBaseMilitar", 2.0f);
                 return;
            }
        }

        Debug.Log("🏗️ [IA Arquiteto] Construindo Base com Layout Espaçado...");

        // 1. Refinaria (Centro-Esquerda)
        ConstruirEstrutura("Refinaria", new Vector3(-10, 0, 0));

        // 2. Quartel (Centro-Direita) - Este será murado
        Vector3 posQuartel = new Vector3(15, 0, 0); 
        GameObject quartel = ConstruirEstrutura("Quartel", posQuartel);
        if (quartel == null) quartel = ConstruirEstrutura("Tenda", posQuartel); 

        // 2b. Fábrica de Veículos (Essencial para Tanques/Transportes)
        Vector3 posFabrica = new Vector3(15, 0, 15);
        ConstruirEstrutura("Fabrica", posFabrica); // Tenta achar algo com "Fabrica" no nome (ex: Fabrica_Veiculos)

        // 3. Muro - Quadrado Perfeito em volta do Quartel
        if (quartel != null)
        {
            CriarPerimetroQuadrado(quartel.transform.position, distanciaMuro);
        }

        baseIniciada = true;
    }

    GameObject ConstruirEstrutura(string nome, Vector3 offset)
    {
        if (chefe == null) chefe = GetComponentInParent<IA_Comandante>(); 
        if (chefe == null) return null;

        // Economia de Guerra: Não constrói se estiver muito pobre (exceto Quartel que é vital)
        if (chefe.dinheiro < 300 && nome != "Quartel") return null;

        GameObject prefab = BuscarNoCatalogo(nome);
        if (prefab == null) return null;

        Construtor construtor = FindFirstObjectByType<Construtor>();
        if (construtor == null) return null;

        Vector3 local = (chefe != null && chefe.basePrincipal != null ? chefe.basePrincipal.position : transform.position) + offset;
        local.y = Terrain.activeTerrain.SampleHeight(local);

        GameObject predio = construtor.ConstruirEstruturaIA(prefab, local, Quaternion.identity);
        if (predio != null) ConfigurarIdentidade(predio);
        
        return predio;
    }

    void CriarPerimetroQuadrado(Vector3 centro, float raio)
    {
        GameObject prefabMuro = BuscarNoCatalogo("Muro");
        if (prefabMuro == null) return;

        Construtor construtor = FindFirstObjectByType<Construtor>();
        
        // Calcula quantos muros cabem em cada lado (aprox)
        // Lado do quadrado = raio * 2
        // Quantidade = (raio * 2) / larguraMuro
        int pecasPorLado = Mathf.CeilToInt((raio * 2) / larguraMuro);
        float passo = larguraMuro;

        // 1. Parede Norte (Z+)
        // Vai de (-raio, raio) até (+raio, raio)
        ConstruirLinha(construtor, prefabMuro, 
            centro + new Vector3(-raio, 0, raio), 
            centro + new Vector3(raio, 0, raio), 
            pecasPorLado);

        // 2. Parede Sul (Z-)
        ConstruirLinha(construtor, prefabMuro, 
            centro + new Vector3(-raio, 0, -raio), 
            centro + new Vector3(raio, 0, -raio), 
            pecasPorLado);

        // 3. Parede Leste (X+)
        // Vai de (raio, -raio) até (raio, raio)
        ConstruirLinha(construtor, prefabMuro, 
            centro + new Vector3(raio, 0, -raio), 
            centro + new Vector3(raio, 0, raio), 
            pecasPorLado);

        // 4. Parede Oeste (X-)
        ConstruirLinha(construtor, prefabMuro, 
            centro + new Vector3(-raio, 0, -raio), 
            centro + new Vector3(-raio, 0, raio), 
            pecasPorLado);

        Debug.Log("🛡️ [Arquitetura] Perímetro Quadrado construído.");
    }

    void ConstruirLinha(Construtor construtor, GameObject prefab, Vector3 inicio, Vector3 fim, int qtd)
    {
        Vector3 dir = (fim - inicio).normalized;
        float distTotal = Vector3.Distance(inicio, fim);
        Quaternion rot = Quaternion.LookRotation(dir);

        for(int i = 0; i < qtd; i++)
        {
            // STOP: Se acabar o dinheiro, para de fazer muro! Prioridade é exército.
            if (chefe.dinheiro < 500) 
            {
                // Debug.Log("[IA Arquiteto] Pausando construção de muros para poupar recursos.");
                break;
            }

            // Interpolação linear para distribuir os muros
            float t = (float)i / (float)qtd;
            Vector3 pos = Vector3.Lerp(inicio, fim, t);
            
            // Ajuste de altura
            pos.y = Terrain.activeTerrain.SampleHeight(pos);

            GameObject m = construtor.ConstruirEstruturaIA(prefab, pos, rot);
            ConfigurarIdentidade(m);
        }
    }

    void ConfigurarIdentidade(GameObject obj)
    {
        if (obj == null) return;
        var id = obj.GetComponent<IdentidadeUnidade>();
        if (id == null) id = obj.AddComponent<IdentidadeUnidade>();
        id.teamID = 2; // Time da IA
        
        // Se for fábrica, registra no General
        var fab = obj.GetComponent<Fabrica>();
        if (fab != null && chefe != null && chefe.cerebroGeneral != null)
        {
            chefe.cerebroGeneral.RegistrarFabrica(fab);
        }
    }

    GameObject BuscarNoCatalogo(string nome)
    {
        if (MenuConstrucao.catalogoGlobal == null) return null;
        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
            if (item != null && item.nomeItem.ToLower().Contains(nome.ToLower())) return item.prefabDaUnidade;
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
