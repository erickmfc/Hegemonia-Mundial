using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Script para o prédio do Heliporto.
/// Gerencia o ponto de pouso e o menu para chamar helicópteros.
/// Ao clicar no heliporto, abre um menu estilo "C" para selecionar helicópteros.
/// </summary>
public class Heliporto : MonoBehaviour
{
    [Header("Configuração do Ponto de Pouso")]
    [Tooltip("Offset local do ponto de pouso. Aumente o Y para o helicóptero não afundar na plataforma.")]
    public Vector3 pontoDePousoLocal = new Vector3(0, 1.2f, 0); 
    
    [Tooltip("Visualização do ponto de pouso no Gizmo")]
    public float tamanhoPlatforma = 5f;
    
    [Tooltip("Cor do gizmo da plataforma")]
    public Color corPlataforma = new Color(0, 1, 0, 0.5f);

    [Header("Helicópteros no Heliporto")]
    [Tooltip("Lista de helicópteros atualmente pousados aqui")]
    public List<Helicoptero> helicopterosPousados = new List<Helicoptero>();
    
    [Tooltip("Número máximo de helicópteros que podem pousar")]
    public int capacidadeMaxima = 1;

    private Animator anim;
    private IdentidadeUnidade identidade;

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    void Start()
    {
        identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();

        // Registrar no Gerente para compras futuras
        GerenteDeJogo gerente = FindFirstObjectByType<GerenteDeJogo>();
        if(gerente != null) gerente.RegistrarHeliporto(this);

        // Limpa lista de helicópteros pousados
        helicopterosPousados.RemoveAll(h => h == null);
    }

    void Update()
    {
        // Menu removido. Ponto de pouso opera passivamente.
    }

    /// <summary>
    /// Retorna a posição mundial do ponto de pouso.
    /// </summary>
    public Vector3 ObterPontoDePousoMundial()
    {
        return transform.TransformPoint(pontoDePousoLocal);
    }

    /// <summary>
    /// Verifica se há espaço para mais helicópteros.
    /// </summary>
    public bool TemEspacoParaPousar()
    {
        helicopterosPousados.RemoveAll(h => h == null);
        return helicopterosPousados.Count < capacidadeMaxima;
    }

    /// <summary>
    /// Chamado quando um helicóptero pousa neste heliporto.
    /// </summary>
    public void HelicopteroPousou(Helicoptero heli)
    {
        if (heli != null && !helicopterosPousados.Contains(heli))
        {
            helicopterosPousados.Add(heli);
            Debug.Log($"[Heliporto] Helicóptero {heli.nomeHelicoptero} pousou. Total: {helicopterosPousados.Count}");
        }
    }

    /// <summary>
    /// Chamado quando um helicóptero decola deste heliporto.
    /// </summary>
    public void HelicopteroDecolou(Helicoptero heli)
    {
        if (heli != null && helicopterosPousados.Contains(heli))
        {
            helicopterosPousados.Remove(heli);
            Debug.Log($"[Heliporto] Helicóptero {heli.nomeHelicoptero} decolou. Restantes: {helicopterosPousados.Count}");
        }
    }

    // ========================================
    // === VISUAL (GIZMOS) ===
    // ========================================

    void OnDrawGizmos()
    {
        // Desenha o ponto de pouso
        Gizmos.color = corPlataforma;
        Vector3 pontoMundial = transform.TransformPoint(pontoDePousoLocal);
        
        // Marca o ponto EXATO onde o helicóptero vai ficar (pivô dele)
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(pontoMundial, 0.3f);
        Gizmos.DrawWireSphere(pontoMundial, 0.5f);

        // Linha indicando a altura em relação ao chão do objeto
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, pontoMundial);

        // Plataforma visual
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(pontoMundial, new Vector3(tamanhoPlatforma, 0.1f, tamanhoPlatforma));
    }

    void OnDrawGizmosSelected()
    {
        // Quando selecionado, mostra mais detalhes
        Gizmos.color = Color.yellow;
        Vector3 pontoMundial = transform.TransformPoint(pontoDePousoLocal);
        Gizmos.DrawWireCube(pontoMundial, new Vector3(tamanhoPlatforma + 1, 1f, tamanhoPlatforma + 1));
    }
}
