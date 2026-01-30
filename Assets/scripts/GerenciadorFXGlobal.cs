using UnityEngine;

public class GerenciadorFXGlobal : MonoBehaviour
{
    public static GerenciadorFXGlobal Instancia;

    [Header("Efeitos de Dano (Arraste os Prefabs da pasta Efeitos_dano)")]
    public GameObject prefabExplosao;      // Para "Explosao"
    public GameObject prefabFumacaClara;   // Para "fumaca_Clara" (Dano Leve)
    public GameObject prefabFumacaEscura;  // Para "fumaca_escura" (Dano Médio)
    public GameObject prefabFogoIntenso;   // Para "fogo_intenso" (Dano Crítico)
    public GameObject prefabFogoGrande;    // Para "Fogo_Grande" (Alternativo/Boss)
    public GameObject prefabFogoReto;      // Para "fogo_reto" (Motores/Jatos)

    [Header("Sons")]
    public AudioClip somExplosao;

    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Instancia um efeito "Fire and Forget" (ex: Explosão) na posição desejada.
    /// </summary>
    public void TocarExplosao(Vector3 posicao)
    {
        if (prefabExplosao != null)
        {
            Instantiate(prefabExplosao, posicao, Quaternion.identity);
        }
    }

    /// <summary>
    /// Cria um efeito contínuo (fumaça/fogo) anexado ao objeto pai. Retorna o GameObject criado para que quem chamou possa desligá-lo depois.
    /// </summary>
    public GameObject CriarEfeitoContinuo(string tipo, Transform pai)
    {
        GameObject prefabAlvo = null;

        switch (tipo)
        {
            case "FumacaLeve": prefabAlvo = prefabFumacaClara; break;
            case "FumacaEscura": prefabAlvo = prefabFumacaEscura; break;
            case "Fogo": prefabAlvo = prefabFogoIntenso; break;
            case "FogoGrande": prefabAlvo = prefabFogoGrande; break;
        }

        if (prefabAlvo != null)
        {
            GameObject novoFx = Instantiate(prefabAlvo, pai.position, Quaternion.identity, pai);
            novoFx.transform.localPosition = Vector3.zero; // Centraliza no pai (ou ajuste conforme necessário)
            return novoFx;
        }
        
        return null;
    }

    /// <summary>
    /// Instancia um efeito na posição, com escala. Usado pelo SistemaDeDanos simplificado.
    /// </summary>
    public void TocarEfeito(string tipo, Vector3 posicao, float tamanho)
    {
        GameObject prefab = null;

        switch (tipo)
        {
            case "FumacaLeve": prefab = prefabFumacaClara; break;
            case "FumacaEscura": prefab = prefabFumacaEscura; break;
            case "Fogo": prefab = prefabFogoIntenso; break;
            case "Explosao": prefab = prefabExplosao; break;
        }

        if (prefab != null)
        {
            GameObject fx = Instantiate(prefab, posicao, Quaternion.identity);
            fx.transform.localScale = Vector3.one * tamanho;
            
            // Opcional: Se for efeito de partícula sem script de auto-destruição, 
            // idealmente deveria ter um Destroy(fx, tempo), mas deixarei a cargo do prefab.
        }
    }
}
