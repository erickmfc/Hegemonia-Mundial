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
        if (centroDaBase == null) centroDaBase = transform;
    }

    /// <summary>
    /// Encontra o próximo ponto válido para construir, seguindo um padrão espiral a partir do centro.
    /// </summary>
    public Vector3 EncontrarLocalConstrucao(string tipoPredio)
    {
        // Lógica Espiral (Simples)
        // x = r * cos(theta)
        // z = r * sin(theta)
        
        // Tenta achar um ponto livre em 10 tentativas incrementais
        for (int i = 0; i < 10; i++)
        {
            espiralAtual += 30f; // Graus
            espiralDistancia += 0.5f; // Expande levemente

            float rad = espiralAtual * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * espiralDistancia;
            float z = Mathf.Sin(rad) * espiralDistancia;

            Vector3 pontoCandidato = centroDaBase.position + new Vector3(x, 0, z);

            // TODO: Raycast para verificar se o chão é plano e não tem nada em cima
            // if (VerificarTerreno(pontoCandidato)) return pontoCandidato;

            // Retorno temporário:
            return pontoCandidato;
        }

        return Vector3.zero; // Não achou
    }

    /// <summary>
    /// Lógica específica para perímetro defensivo
    /// </summary>
    public Vector3 EncontrarPontoDefensivo()
    {
        // Pega um ponto na borda do raio de 90m (exemplo)
        Vector3 direcaoAleatoria = Random.onUnitSphere;
        direcaoAleatoria.y = 0;
        direcaoAleatoria.Normalize();

        return centroDaBase.position + (direcaoAleatoria * raioPerimetroInicial);
    }
    private bool baseIniciada = false;

    void Start()
    {
        // Espera um pouco para garantir que o MenuConstrucao carregou o catálogo
        Invoke("ConstruirBaseInicial", 2.0f);
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

        Debug.Log("[IA Arquiteto] Iniciando construção da Base Inicial...");

        // 1. Encontra os prefabs essenciais no catálogo pelo NOME
        GameObject prefabRefinaria = BuscarNoCatalogo("Refinaria"); 
        GameObject prefabQuartel = BuscarNoCatalogo("Tenda"); 
        // GameObject prefabDefesa = BuscarNoCatalogo("Torreta");

        Construtor construtor = FindObjectOfType<Construtor>();
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
        else Debug.LogWarning("[IA Arquiteto] Não achei prefab de 'Refinaria' no catálogo.");

        // 3. Constrói Tenda (Militar)
        if (prefabQuartel != null)
        {
            Vector3 pos = EncontrarLocalConstrucao("Tenda");
            GameObject predio = construtor.ConstruirEstruturaIA(prefabQuartel, pos, Quaternion.identity);
            ConfigurarPredioIA(predio);
        }
        else Debug.LogWarning("[IA Arquiteto] Não achei prefab de 'Tenda' no catálogo.");

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
        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
            if (item != null && item.nomeItem.ToLower().Contains(nomeParcial.ToLower()) && item.prefabDaUnidade != null)
            {
                return item.prefabDaUnidade;
            }
        }
        return null;
    }
}
