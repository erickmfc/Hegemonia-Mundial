using System.Collections;
using UnityEngine;

/// <summary>
/// Base fixa para mísseis estratégicos. Ela nunca recebe ordem de movimento;
/// apenas seleciona a área e coordena a preparação, lançamento e recarga.
/// </summary>
public class SiloLancadorEstrategico : MonoBehaviour
{
    public enum CargaDisponivel { Convencional, Nuclear }

    [Header("Lançador")]
    public Transform pontoDeSaida;
    public GameObject prefabMisselEstrategico;
    public string nomeDaBase = "Base Estratégica";
    public float alcanceMaximo = 3500f;
    public float tempoPreparacao = 12f;
    public float tempoRecarga = 30f;
    public int misseisDisponiveis = 4;
    public CargaDisponivel carga = CargaDisponivel.Convencional;
    public bool podeUsarCargaNuclear = false;

    [Header("Seleção")]
    public bool marcarAlvoComBotaoDireito = true;
    public bool mostrarAreaDeAlcance = true;

    public bool ProntoParaLancar => prontoParaLancar && misseisDisponiveis > 0;
    public bool EmMarcacaoDeAlvo => modoMarcacao;
    public float AlcanceMaximo => alcanceMaximo;

    private bool prontoParaLancar = true;
    private bool modoMarcacao;
    private Camera cameraPrincipal;
    private ControleUnidade controle;
    private LineRenderer linhaAlcance;
    private GameObject marcadorAlvo;
    private GameObject misselEmEspera;

    private void Awake()
    {
        cameraPrincipal = Camera.main;
        controle = GetComponent<ControleUnidade>();
        if (pontoDeSaida == null) pontoDeSaida = transform;
        CriarMisselVisivelNaPlataforma();
        CriarVisualizadorAlcance();
    }

    private void CriarMisselVisivelNaPlataforma()
    {
        if (misselEmEspera != null || prefabMisselEstrategico == null || pontoDeSaida == null) return;
        misselEmEspera = Instantiate(prefabMisselEstrategico, pontoDeSaida.position, pontoDeSaida.rotation, pontoDeSaida);
        misselEmEspera.name = "ICBM em espera - " + nomeDaBase;
        misselEmEspera.transform.localPosition = Vector3.zero;
        misselEmEspera.transform.localRotation = Quaternion.identity;
        // O foguete fica visível na plataforma, mas não executa voo, colisão ou
        // dano até o comando de lançamento.
        foreach (MonoBehaviour componente in misselEmEspera.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (componente != null) componente.enabled = false;
        }
        foreach (Collider colisor in misselEmEspera.GetComponentsInChildren<Collider>(true))
        {
            if (colisor != null) colisor.enabled = false;
        }
    }

    private void Update()
    {
        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
        bool selecionado = controle != null && controle.selecionado;
        if (linhaAlcance != null) linhaAlcance.enabled = selecionado && mostrarAreaDeAlcance;

        if (selecionado && modoMarcacao && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;
            if (TentarObterPontoDoMundo(out Vector3 ponto)) TentarLancarNaArea(ponto);
        }
    }

    public void ArmarMarcacaoAlvo()
    {
        modoMarcacao = true;
        Debug.Log($"[{nomeDaBase}] Área de lançamento armada. Escolha um ponto até {alcanceMaximo:F0}m.", this);
    }

    public void CancelarMarcacaoAlvo()
    {
        modoMarcacao = false;
        if (marcadorAlvo != null) marcadorAlvo.SetActive(false);
    }

    public bool TentarLancarNaArea(Vector3 pontoAlvo)
    {
        float distancia = Vector3.Distance(transform.position, pontoAlvo);
        if (!ProntoParaLancar)
        {
            Debug.LogWarning($"[{nomeDaBase}] Sem mísseis disponíveis ou em recarga.", this);
            return false;
        }
        if (distancia > alcanceMaximo)
        {
            Debug.LogWarning($"[{nomeDaBase}] Alvo fora do alcance: {distancia:F0}m / {alcanceMaximo:F0}m.", this);
            return false;
        }
        if (prefabMisselEstrategico == null)
        {
            Debug.LogError($"[{nomeDaBase}] Prefab do míssil estratégico não configurado.", this);
            return false;
        }

        CancelarMarcacaoAlvo();
        StartCoroutine(SequenciaDeLancamento(pontoAlvo));
        return true;
    }

    private IEnumerator SequenciaDeLancamento(Vector3 pontoAlvo)
    {
        prontoParaLancar = false;
        misseisDisponiveis = Mathf.Max(0, misseisDisponiveis - 1);
        Debug.Log($"[{nomeDaBase}] Preparando lançamento. Execução em {tempoPreparacao:F1}s.", this);
        yield return new WaitForSeconds(Mathf.Max(0f, tempoPreparacao));

        Vector3 posicao = pontoDeSaida != null ? pontoDeSaida.position : transform.position;
        Quaternion rotacao = pontoDeSaida != null ? pontoDeSaida.rotation : transform.rotation;
        if (misselEmEspera != null)
        {
            Destroy(misselEmEspera);
            misselEmEspera = null;
        }
        GameObject objeto = PoolDeObjetosCombate.Spawn(prefabMisselEstrategico, posicao, rotacao);
        MisselEstrategicoLongoAlcance missel = objeto != null ? objeto.GetComponent<MisselEstrategicoLongoAlcance>() : null;
        if (missel == null && objeto != null) missel = objeto.AddComponent<MisselEstrategicoLongoAlcance>();
        if (missel != null)
        {
            bool nuclear = carga == CargaDisponivel.Nuclear && podeUsarCargaNuclear;
            missel.IniciarLancamento(pontoAlvo, nuclear, this);
            Debug.Log($"[{nomeDaBase}] Míssil lançado: carga={(nuclear ? "NUCLEAR" : "CONVENCIONAL")}, alvo={pontoAlvo}.", this);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, tempoRecarga));
        prontoParaLancar = true;
        CriarMisselVisivelNaPlataforma();
    }

    private bool TentarObterPontoDoMundo(out Vector3 ponto)
    {
        ponto = Vector3.zero;
        if (cameraPrincipal == null) return false;
        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(raio, out RaycastHit hit, 10000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        ponto = hit.point;
        return true;
    }

    private void CriarVisualizadorAlcance()
    {
        linhaAlcance = gameObject.GetComponent<LineRenderer>();
        if (linhaAlcance == null) linhaAlcance = gameObject.AddComponent<LineRenderer>();
        linhaAlcance.positionCount = 65;
        linhaAlcance.loop = true;
        linhaAlcance.useWorldSpace = true;
        linhaAlcance.widthMultiplier = 0.35f;
        linhaAlcance.material = new Material(Shader.Find("Sprites/Default"));
        linhaAlcance.material.color = new Color(1f, 0.15f, 0.05f, 0.55f);
        for (int i = 0; i < linhaAlcance.positionCount; i++)
        {
            float a = i / 64f * Mathf.PI * 2f;
            linhaAlcance.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a)) * alcanceMaximo);
        }
        linhaAlcance.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!mostrarAreaDeAlcance) return;
        Gizmos.color = carga == CargaDisponivel.Nuclear ? new Color(1f, 0.1f, 0.05f, 0.25f) : new Color(1f, 0.6f, 0.1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, alcanceMaximo);
    }
}
