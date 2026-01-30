using UnityEngine;
using System.Collections.Generic;

public class IA_Comandante : MonoBehaviour
{
    public static IA_Comandante Instancia;

    [Header("Módulos da IA Suprema (Cérebros)")]
    // Estes scripts devem estar anexados ao mesmo GameObject ou serão criados automaticamente
    public IA_Economia cerebroEconomico;
    public IA_Arquiteto cerebroArquiteto;
    public IA_General cerebroGeneral;
    public IA_Combate cerebroCombate;

    [Tooltip("Intervalo entre decisões da IA")]
    public float intervaloDeProcessamento = 1.0f;

    [Header("Identidade e Personalidade")]
    public IdentidadeIA identidade;

    [Header("Scripts Customizáveis (Espaço Livre)")]
    [Tooltip("Arraste seus scripts extras aqui para acessá-los facilmente")]
    public MonoBehaviour scriptExtra1;
    public MonoBehaviour scriptExtra2;
    public MonoBehaviour scriptExtra3;
    public MonoBehaviour scriptExtra4;

    [Header("Recursos do Comandante")]
    public float dinheiro = 2000f; // Capital inicial

    [Header("Gerenciamento de Unidades")]
    public List<GameObject> minhasUnidades = new List<GameObject>();
    
    [Tooltip("Local onde novas unidades serão instanciadas")]
    public Transform pontoDeSpawn;
    
    [Tooltip("Base Principal ou QG")]
    public Transform basePrincipal;

    private float _timer;

    void Awake()
    {
        if (Instancia == null) Instancia = this;
        
        // Auto-Detectar ou Adicionar módulos se faltarem
        if (!TryGetComponent(out cerebroEconomico)) cerebroEconomico = gameObject.AddComponent<IA_Economia>();
        if (!TryGetComponent(out cerebroArquiteto)) cerebroArquiteto = gameObject.AddComponent<IA_Arquiteto>();
        if (!TryGetComponent(out cerebroGeneral)) cerebroGeneral = gameObject.AddComponent<IA_General>();
        if (!TryGetComponent(out cerebroCombate)) cerebroCombate = gameObject.AddComponent<IA_Combate>();
    }

    void Start()
    {
        // Inicializa os sub-cérebros
        cerebroEconomico.Inicializar(this);
        cerebroArquiteto.Inicializar(this);
        cerebroGeneral.Inicializar(this);
        cerebroCombate.Inicializar(this);

        if (pontoDeSpawn == null) pontoDeSpawn = transform;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= intervaloDeProcessamento)
        {
            _timer = 0;
            
            // Ciclo de Pensamento da IA
            cerebroEconomico.ProcessarEconomia();
            cerebroGeneral.ProcessarEstrategia();
            cerebroCombate.LimparMemoria();
            
            // O Arquiteto trabalha sob demanda
        }
    }

    /// <summary>
    ///  Chamado quando uma nova unidade é criada por esta IA.
    ///  Configura identidade e avisa o cérebro.
    /// </summary>
    public void RegistrarUnidade(GameObject unidade)
    {
        if (unidade == null) return;

        if (!minhasUnidades.Contains(unidade))
        {
            minhasUnidades.Add(unidade);

            // Garante que a unidade tenha a identidade correta (Time da IA)
            var id = unidade.GetComponent<IdentidadeUnidade>();
            if (id != null)
            {
                // Busca ID da IA do Gerenciador de Partida, se existir
                if (GerenciadorDePartida.Instancia != null)
                {
                    id.teamID = GerenciadorDePartida.Instancia.idIA;
                    id.nomeDoPais = "Dominion AI"; 
                }
            }

            // Avisa o General que temos mais um soldado
            if (cerebroGeneral != null)
            {
                cerebroGeneral.RegistrarSoldado(unidade);
            }
        }
    }

    /// <summary>
    /// Método para gastar dinheiro. Retorna true se teve sucesso.
    /// </summary>
    public bool GastarDinheiro(float valor)
    {
        if (dinheiro >= valor)
        {
            dinheiro -= valor;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Método para ganhar dinheiro (renda passiva, coletas, etc)
    /// </summary>
    public void AdicionarDinheiro(float valor)
    {
        dinheiro += valor;
    }
}
