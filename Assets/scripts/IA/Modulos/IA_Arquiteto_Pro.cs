using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// IA Arquiteto Pro: Responsável por urbanismo militar.
/// Constrói base com layout aberto e espaçado, SEM prender unidades.
/// </summary>
public class IA_Arquiteto_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    private bool baseIniciada = false;

    [Header("Configurações de Construção")]
    public float espacamentoEdificios = 20f; // Distância entre prédios
    
    // Controle de espiral para evitar sobreposição

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Start()
    {
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        Invoke("PlanejarBaseMilitar", 3.0f);

        // --- MANUTENÇÃO DE BASE ---
        // Verifica a cada 10 segundos se a base está intacta
        InvokeRepeating("VerificarIntegridadeDaBase", 15.0f, 10.0f);
    }

    void VerificarIntegridadeDaBase()
    {
        if (!baseIniciada || chefe == null) return;

        Debug.Log("🏗️ [IA Arquiteto] Verificando integridade da base...");
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        // Verifica se tem QUARTEL (Soldados)
        if (!ExistePredio("Quartel") && !ExistePredio("Tenda"))
        {
            Debug.LogWarning("⚠️ [IA Arquiteto] Quartel destruído! Reconstruindo...");
            Vector3 pos = EncontrarPosicaoAberta(centro, 1);
            // Tenta achar posição livre caso a original esteja ocupada por destroços
            if (Physics.CheckSphere(pos, 5.0f)) pos += Vector3.right * 10f; 
            
            GameObject novo = ConstruirEstrutura("Quartel", pos);
            if (novo == null) ConstruirEstrutura("Tenda", pos);
        }

        // Verifica se tem HANGAR (Veículos)
        if (!ExistePredio("Hangar") && !ExistePredio("Fabrica"))
        {
            Debug.LogWarning("⚠️ [IA Arquiteto] Hangar destruído! Reconstruindo...");
            Vector3 pos = EncontrarPosicaoAberta(centro, 2);
             if (Physics.CheckSphere(pos, 8.0f)) pos += Vector3.left * 10f;

            ConstruirEstrutura("Hangar", pos);
        }
    }

    bool ExistePredio(string nomeParcial)
    {
        Fabrica[] fabricas = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach (var f in fabricas)
        {
            // Verifica se é do meu time e se o nome bate
            var id = f.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == chefe.identidade.teamID && f.name.ToLower().Contains(nomeParcial.ToLower())) 
            {
                return true; 
            }
        }
        return false;
    }

    void PlanejarBaseMilitar()
    {
        if (baseIniciada) return;
        
        // ... (código original de PlanejarBaseMilitar mantido abaixo)
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

        Debug.Log("🏗️ [IA Arquiteto Pro] Construindo Base com Layout ABERTO...");

        Vector3 centro = (chefe != null && chefe.basePrincipal != null) 
            ? chefe.basePrincipal.position 
            : transform.position;

        // 1. Refinaria (Economia - Prioridade Máxima)
        ConstruirEstrutura("Refinaria", EncontrarPosicaoAberta(centro, 0));

        // 2. Quartel/Tenda (Produção de soldados)
        Vector3 posQuartel = EncontrarPosicaoAberta(centro, 1);
        GameObject quartel = ConstruirEstrutura("Quartel", posQuartel);
        if (quartel == null) quartel = ConstruirEstrutura("Tenda", posQuartel);

        // 3. Hangar (Produção de veículos/helis)
        Vector3 posHangar = EncontrarPosicaoAberta(centro, 2);
        
        // Tenta vários nomes possíveis para achar a fábrica de veículos
        GameObject hangar = ConstruirEstrutura("Hangar", posHangar);
        if (hangar == null) hangar = ConstruirEstrutura("Fabrica", posHangar);
        
        // 4. Defesas pontuais
        ConstruirDefesasPontuais(centro);

        baseIniciada = true;
    }

    /// <summary>
    /// Encontra uma posição aberta usando padrão espiral para evitar sobreposição.
    /// </summary>
    Vector3 EncontrarPosicaoAberta(Vector3 centro, int indice)
    {
        // Distribui em círculo ao redor do centro com espaçamento generoso
        float angulo = indice * 120f; // 120 graus entre cada prédio (3 pontos em círculo)
        float raio = espacamentoEdificios;
        
        float rad = angulo * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad) * raio, 0, Mathf.Sin(rad) * raio);
        Vector3 pos = centro + offset;
        
        // Ajuste de altura ao terreno
        if (Terrain.activeTerrain != null)
            pos.y = Terrain.activeTerrain.SampleHeight(pos);
        
        return pos;
    }

    /// <summary>
    /// Constrói defesas pontuais (muros curtos/barreiras) em pontos estratégicos,
    /// deixando SEMPRE passagens amplas para as unidades circularem.
    /// </summary>
    void ConstruirDefesasPontuais(Vector3 centro)
    {
        GameObject prefabMuro = BuscarNoCatalogo("Muro");
        if (prefabMuro == null) return;
        if (chefe.dinheiro < 800) return; // Só constrói defesas se tiver recursos de sobra

        Construtor construtor = FindFirstObjectByType<Construtor>();
        if (construtor == null) return;

        // Coloca APENAS 2-3 segmentos curtos de muro em direções estratégicas
        // (voltados para longe da base, como cobertura), NUNCA fechando um perímetro
        float raioDefesa = 30f;
        
        // 3 pontos defensivos com gaps enormes entre eles
        float[] angulos = { 0f, 120f, 240f };
        
        foreach (float ang in angulos)
        {
            if (chefe.dinheiro < 300) break; // Para se o dinheiro acabar

            float rad = ang * Mathf.Deg2Rad;
            Vector3 posBarreira = centro + new Vector3(
                Mathf.Cos(rad) * raioDefesa, 
                0, 
                Mathf.Sin(rad) * raioDefesa
            );
            
            if (Terrain.activeTerrain != null)
                posBarreira.y = Terrain.activeTerrain.SampleHeight(posBarreira);
            
            // Rotação: muro perpendicular à direção do centro (cobertura lateral)
            Quaternion rot = Quaternion.LookRotation(
                Vector3.Cross(Vector3.up, (posBarreira - centro).normalized)
            );
            
            GameObject m = construtor.ConstruirEstruturaIA(prefabMuro, posBarreira, rot);
            ConfigurarIdentidade(m);
        }
        
        Debug.Log("🛡️ [Arquitetura] Barreiras defensivas pontuais colocadas (layout ABERTO).");
    }

    GameObject ConstruirEstrutura(string nome, Vector3 posicao)
    {
        if (chefe == null) chefe = GetComponentInParent<IA_Comandante>(); 
        if (chefe == null) return null;

        // Economia: Não constrói se estiver muito pobre (exceto Quartel que é vital)
        if (chefe.dinheiro < 300 && nome != "Quartel" && nome != "Tenda") return null;

        GameObject prefab = BuscarNoCatalogo(nome);
        if (prefab == null) return null;

        Construtor construtor = FindFirstObjectByType<Construtor>();
        if (construtor == null) return null;

        // Ajuste de altura
        if (Terrain.activeTerrain != null)
            posicao.y = Terrain.activeTerrain.SampleHeight(posicao);

        GameObject predio = construtor.ConstruirEstruturaIA(prefab, posicao, Quaternion.identity);
        if (predio != null) ConfigurarIdentidade(predio);
        
        return predio;
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
