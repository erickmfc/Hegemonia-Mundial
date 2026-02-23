using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class IA_Arquiteto : MonoBehaviour
{
    private IA_Comandante chefe;

    [Header("Planejamento Urbano")]
    public Transform centroDaBase;
    public float raioPerimetroInicial = 40f;
    private float espiralAtual = 0f;
    private float espiralDistancia = 10f; // Distância entre anéis da espiral

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
        
        // Garante que centroDaBase não seja nulo
        if (centroDaBase == null)
        {
            if (comandante != null && comandante.basePrincipal != null)
                centroDaBase = comandante.basePrincipal;
            else if (comandante != null)
                centroDaBase = comandante.transform;
            else
                centroDaBase = transform;
        }
    }

    /// <summary>
    /// Encontra o próximo ponto válido para construir, seguindo um padrão espiral a partir do centro.
    /// </summary>
    public Vector3 EncontrarLocalConstrucao(string tipoPredio)
    {
        // Valida centroDaBase antes de usar
        if (centroDaBase == null)
        {
            // Erro silencioso/Warning que resolve sozinho
            Debug.LogWarning("[IA Arquiteto] `centroDaBase` não estava configurado. Tentando auto-atribuir usando o Comandante ou Transform atual.");
            
            if (chefe != null && chefe.basePrincipal != null)
                centroDaBase = chefe.basePrincipal;
            else if (chefe != null)
                centroDaBase = chefe.transform;
            else
                centroDaBase = transform; // Fallback final
                
            if (centroDaBase == null)
            {
                Debug.LogError("[IA Arquiteto] ERRO CRÍTICO: Impossível determinar centro da base mesmo após tentativas de recuperação!");
                return Vector3.zero;
            }
        }
        
        // Lógica Espiral com ângulo áureo para distribuição uniforme
        for (int i = 0; i < 20; i++)
        {
            espiralAtual += 137.5f; // Ângulo áureo — espalha uniformemente
            espiralDistancia += 20f; // 20m entre cada ponto (era 2.5m!)

            float rad = espiralAtual * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * espiralDistancia;
            float z = Mathf.Sin(rad) * espiralDistancia;

            Vector3 pontoCandidato = centroDaBase.position + new Vector3(x, 0, z);

            if (VerificarTerreno(pontoCandidato)) return pontoCandidato;

            if (i == 19) return pontoCandidato; // Fallback extremo
        }

        return Vector3.zero; // Não achou
    }

    bool VerificarTerreno(Vector3 ponto)
    {
        // 1. Verifica se tem algo construído perto (Raio de 20m para espalhar)
        if (Physics.CheckSphere(ponto, 20f))
        {
            return false;
        }

        // 2. Verifica se o terreno é plano o suficiente
        // Lança 5 raios: Centro, Frente, Trás, Esquerda, Direita
        float alturaCentro = Terrain.activeTerrain.SampleHeight(ponto);
        Vector3[] offsets = { Vector3.forward * 3, Vector3.back * 3, Vector3.left * 3, Vector3.right * 3 };

        foreach (var offset in offsets)
        {
            float alturaPonto = Terrain.activeTerrain.SampleHeight(ponto + offset);
            if (Mathf.Abs(alturaCentro - alturaPonto) > 1.0f) // Tolerância de 1 metro de desnível
            {
                return false; 
            }
        }

        return true;
    }

    /// <summary>
    /// Lógica específica para perímetro defensivo
    /// </summary>
    public Vector3 EncontrarPontoDefensivo()
    {
        if (centroDaBase == null) centroDaBase = transform;

        // Pega um ponto na borda do raio de 90m (exemplo)
        Vector3 direcaoAleatoria = Random.onUnitSphere;
        direcaoAleatoria.y = 0;
        direcaoAleatoria.Normalize();

        return centroDaBase.position + (direcaoAleatoria * raioPerimetroInicial);
    }
    private bool baseIniciada = false;

    void Start()
    {
        // Tenta encontrar o chefe se ainda não foi atribuído
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        if (chefe == null) chefe = GetComponentInParent<IA_Comandante>();

        // Tenta corrigir centroDaBase logo no início se possível
        if (centroDaBase == null)
        {
            if (chefe != null && chefe.basePrincipal != null) centroDaBase = chefe.basePrincipal;
            else if (chefe != null) centroDaBase = chefe.transform;
            else centroDaBase = transform;
        }

        // DESATIVADO: IA_Arquiteto_Pro agora cuida de toda a construção da base.
        // Invoke("ConstruirBaseInicial", 2.0f);
    }

    void ConstruirBaseInicial()
    {
        if (baseIniciada) return;
        if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0)
        {
            Debug.LogWarning("[IA Arquiteto] Catálogo vazio ou não carregado. Tentando novamente em 2s...");
            Invoke("ConstruirBaseInicial", 2.0f);
            return;
        }

        // Debug.Log("[IA Arquiteto] Iniciando construção da Base Inicial...");

        // 1. Encontra os prefabs essenciais no catálogo pelo NOME
        GameObject prefabRefinaria = BuscarNoCatalogo("Refinaria"); 
        GameObject prefabQuartel = BuscarNoCatalogo("Tenda"); 
        // GameObject prefabDefesa = BuscarNoCatalogo("Torreta");

        Construtor construtor = FindFirstObjectByType<Construtor>();
        if (construtor == null)
        {
            Debug.LogError("[IA Arquiteto] ERRO: Script Construtor não encontrado na cena!");
            return;
        }

        // 2. Constrói Refinaria (Recurso)
        if (prefabRefinaria != null)
        {
            Vector3 pos = EncontrarLocalConstrucao("Refinaria");
            GameObject predio = construtor.ConstruirEstruturaIA(prefabRefinaria, pos, Quaternion.identity);
            ConfigurarPredioIA(predio);
        }
        else 
        {
            // Debug.LogWarning("[IA Arquiteto] Não achei prefab de 'Refinaria' no catálogo.");
        }

        // 3. Constrói Tenda (Militar)
        if (prefabQuartel != null)
        {
            Vector3 pos = EncontrarLocalConstrucao("Tenda");
            GameObject predio = construtor.ConstruirEstruturaIA(prefabQuartel, pos, Quaternion.identity);
            ConfigurarPredioIA(predio);
        }

        // 4. Constrói HANGAR/AEROPORTO (Para Helicópteros)
        GameObject prefabHangar = BuscarNoCatalogo("Hangar");
        if (prefabHangar == null) prefabHangar = BuscarNoCatalogo("Aeroporto");
        if (prefabHangar == null) prefabHangar = BuscarNoCatalogo("Air");

        if (prefabHangar != null)
        {
             Vector3 pos = EncontrarLocalConstrucao("Hangar");
             // Afasta um pouco mais
             pos += new Vector3(15, 0, 15); 
             GameObject predio = construtor.ConstruirEstruturaIA(prefabHangar, pos, Quaternion.identity);
             ConfigurarPredioIA(predio);
             Debug.Log("✈️ [IA Arquiteto] Construindo Hangar para força aérea!");
        }

        baseIniciada = true;
    }

    void ConfigurarPredioIA(GameObject predio)
    {
        if (predio == null) return;

        // 1. Define Identidade (Time 2 = Inimigo IA)
        var id = predio.GetComponent<IdentidadeUnidade>();
        if (id == null) id = predio.AddComponent<IdentidadeUnidade>();
        
        id.teamID = 2; // Inimigo
        id.nomeDoPais = "Dominion AI";

        // 2. Se for Fábrica, avisa o General para ele usar
        var fabrica = predio.GetComponent<Fabrica>();
        if (fabrica != null && chefe != null && chefe.cerebroGeneral != null)
        {
            chefe.cerebroGeneral.RegistrarFabrica(fabrica);
        }
    }

    GameObject BuscarNoCatalogo(string nomeParcial)
    {
        if (MenuConstrucao.catalogoGlobal == null) return null;

        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
            if (item != null && item.nomeItem.ToLower().Contains(nomeParcial.ToLower()) && item.prefabDaUnidade != null)
            {
                return item.prefabDaUnidade;
            }
        }
        return null;
    }
    
    void ListarPrefabsDisponiveis()
    {
        // Debug
    }
}
