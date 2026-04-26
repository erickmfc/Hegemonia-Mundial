using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class DesenharLinhasOrdem : MonoBehaviour
{
    private static int frameCliqueConsumido = -1;

    public static bool ConsumiuCliqueEsteFrame()
    {
        return frameCliqueConsumido == Time.frameCount;
    }

    private static void ConsumirCliqueNoFrame()
    {
        frameCliqueConsumido = Time.frameCount;
    }

    public LineRenderer lineRenderer;
    private GerenteSelecao gerenteSelecao;

    [Header("Marcadores")]
    public GameObject prefabMarcadorPatrulha;

    [Header("Estados Atuais")]
    public bool modoPatrulhaAtivo = false;
    public bool modoSeguirAtivo = false;

    public List<Vector3> pontosPatrulha = new List<Vector3>();

    void Start()
    {
        gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();
        CriarLineRenderer();
    }

    void CriarLineRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.5f;
        lineRenderer.endWidth = 0.5f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(1f, 0.85f, 0f, 0.6f); // Amarelo/Dourado semi-transparente
        lineRenderer.endColor = new Color(1f, 0.85f, 0f, 0.6f);
        lineRenderer.positionCount = 0;
    }

    public void IniciarModoPatrulha()
    {
        modoPatrulhaAtivo = true;
        modoSeguirAtivo = false;
        pontosPatrulha.Clear();
        lineRenderer.positionCount = 0;
        Debug.Log("MODO PATRULHA: Clique com o botão direito para marcar caminho inicial e continue clicando para adicionar mais pontos de patrulha. ESC ou ENTER para finalizar os desenhos.");
    }

    public void IniciarModoSeguir()
    {
        modoSeguirAtivo = true;
        modoPatrulhaAtivo = false;
        lineRenderer.positionCount = 0;
        Debug.Log("MODO SEGUIR: clique com o botao direito em uma unidade aliada. ESC cancela.");
    }

    void Update()
    {
        if (gerenteSelecao == null || gerenteSelecao.unidadesSelecionadas.Count == 0)
        {
            LimparTudo();
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (modoPatrulhaAtivo && Input.GetMouseButtonDown(1))
        {
            Vector3 pontoPatrulha;
            if (TryObterPontoPatrulha(out pontoPatrulha))
            {
                ConsumirCliqueNoFrame();
                pontosPatrulha.Add(pontoPatrulha);
                AtualizarLinhaVisualPatrulha();
                MostrarMarcadorPatrulha(pontoPatrulha);

                // Aplica a ordem em tempo real para as unidades já iniciarem a navegação da patrulha
                AplicarOrdemPatrulha();
                
                // Removemos a exigência de segurar SHIFT, agora todo clique adiciona um ponto na rota livremente
            }
        }

        if (modoPatrulhaAtivo && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            if (pontosPatrulha.Count >= 1)
            {
                AplicarOrdemPatrulha();
                Debug.Log("Patrulha confirmada e iniciada.");
            }
            else
            {
                Debug.LogWarning("Patrulha precisa de pelo menos 1 ponto.");
            }

            LimparTudo();
            return;
        }

        if (modoPatrulhaAtivo && Input.GetKeyDown(KeyCode.Backspace) && pontosPatrulha.Count > 0)
        {
            pontosPatrulha.RemoveAt(pontosPatrulha.Count - 1);
            AtualizarLinhaVisualPatrulha();
        }

        if (modoSeguirAtivo && Input.GetMouseButtonDown(1))
        {
            if (Camera.main == null)
            {
                return;
            }

            ConsumirCliqueNoFrame();
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                ControleUnidade alvo = hit.collider.GetComponentInParent<ControleUnidade>();
                if (alvo != null)
                {
                    AplicarOrdemSeguir(alvo.transform);
                    MostrarMarcadorPatrulha(alvo.transform.position, new Color(0.2f, 0.85f, 1f, 0.95f), 14f, 2.2f);
                    Debug.Log("Ordem de SEGUIR confirmada.");
                    LimparTudo();
                }
                else
                {
                    Debug.LogWarning("Voce nao clicou em uma unidade para seguir.");
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LimparTudo();
        }
    }

    void LimparTudo()
    {
        modoPatrulhaAtivo = false;
        modoSeguirAtivo = false;
        pontosPatrulha.Clear();
        lineRenderer.positionCount = 0;
    }

    /// <summary>Cancela o modo patrulha ou seguir sem confirmar a ordem. Chamado pelo clique esquerdo.</summary>
    public void CancelarModo()
    {
        LimparTudo();
    }

    void MostrarMarcadorPatrulha(Vector3 pontoPatrulha)
    {
        // Cor Amarela/Dourada padrão para patrulha
        MostrarMarcadorPatrulha(pontoPatrulha, new Color(1f, 0.85f, 0f, 0.95f), 11f, 3f);
    }

    void MostrarMarcadorPatrulha(Vector3 pontoPatrulha, Color cor, float escala, float tempoVida)
    {
        // Prioridade 1: prefab dedicado neste componente
        if (prefabMarcadorPatrulha != null)
        {
            GameObject marcador = Instantiate(prefabMarcadorPatrulha, pontoPatrulha + Vector3.up * 0.1f, Quaternion.identity);
            marcador.transform.localScale = new Vector3(escala, escala, escala);
            // REMOVIDO: AplicarCorMarcador - Mantém as cores originais da animação amarela
            Destroy(marcador, tempoVida);
            return;
        }

        // Prioridade 2: prefab de patrulha configurado no GerenteSelecao
        if (gerenteSelecao != null && gerenteSelecao.prefabMarcadorPatrulha != null)
        {
            GameObject marcador = Instantiate(gerenteSelecao.prefabMarcadorPatrulha, pontoPatrulha + Vector3.up * 0.1f, Quaternion.identity);
            marcador.transform.localScale = new Vector3(escala * 1.5f, escala * 1.5f, escala * 1.5f);
            // REMOVIDO: AplicarCorMarcador - Mantém as cores originais da animação amarela
            Destroy(marcador, tempoVida);
            return;
        }

        // Prioridade 3: fallback geométrico (aqui sim pintamos de amarelo via script)
        CriarMarcadorFallback(pontoPatrulha, cor, escala, tempoVida);
    }

    void CriarMarcadorFallback(Vector3 pontoPatrulha, Color cor, float escala, float tempoVida)
    {
        GameObject raiz = new GameObject("MarcadorOrdemFallback");
        raiz.transform.position = pontoPatrulha + Vector3.up * 0.12f;

        GameObject disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disco.name = "Disco";
        disco.transform.SetParent(raiz.transform, false);
        disco.transform.localScale = new Vector3(escala * 0.09f, 0.03f, escala * 0.09f);
        Destroy(disco.GetComponent<Collider>());

        GameObject farol = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        farol.name = "Farol";
        farol.transform.SetParent(raiz.transform, false);
        farol.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        farol.transform.localScale = Vector3.one * Mathf.Max(0.6f, escala * 0.08f);
        Destroy(farol.GetComponent<Collider>());

        AplicarCorMarcador(raiz, cor);
        Destroy(raiz, tempoVida);
    }

    void AplicarCorMarcador(GameObject marcador, Color cor)
    {
        if (marcador == null)
        {
            return;
        }

        Renderer[] renderers = marcador.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererAtual = renderers[i];
            if (rendererAtual == null)
            {
                continue;
            }

            // Usando Sprites/Default assegura que não fica com shader de erro rosa independente do URP
            Material materialInstancia = new Material(Shader.Find("Sprites/Default"));
            materialInstancia.color = cor;
            rendererAtual.material = materialInstancia;
            rendererAtual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    bool TryObterPontoPatrulha(out Vector3 ponto)
    {
        ponto = Vector3.zero;

        if (Camera.main == null)
        {
            return false;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            ponto = hit.point;
            return true;
        }

        if (!SelecaoContemUnidadeNaval())
        {
            return false;
        }

        UnityEngine.Plane planoAgua = new UnityEngine.Plane(Vector3.up, Vector3.zero);
        float distanciaPlano;
        if (!planoAgua.Raycast(ray, out distanciaPlano))
        {
            return false;
        }

        ponto = ray.GetPoint(distanciaPlano);
        return true;
    }

    bool SelecaoContemUnidadeNaval()
    {
        if (gerenteSelecao == null)
        {
            return false;
        }

        for (int i = 0; i < gerenteSelecao.unidadesSelecionadas.Count; i++)
        {
            ControleUnidade unidade = gerenteSelecao.unidadesSelecionadas[i];
            if (unidade != null && unidade.EhUnidadeNaval())
            {
                return true;
            }
        }

        return false;
    }

    void AtualizarLinhaVisualPatrulha()
    {
        if (pontosPatrulha.Count == 0 || gerenteSelecao.unidadesSelecionadas.Count == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // Exibe loop completo: unidade → p0 → p1 → ... → pN → unidade (fecha o ciclo)
        const float alturaLinha = 3f;
        Vector3 posInicial = gerenteSelecao.unidadesSelecionadas[0].transform.position;
        posInicial.y += alturaLinha;

        int totalPontos = pontosPatrulha.Count + 2; // +1 início, +1 fecha o loop
        lineRenderer.positionCount = totalPontos;
        lineRenderer.SetPosition(0, posInicial);

        for (int i = 0; i < pontosPatrulha.Count; i++)
        {
            Vector3 p = pontosPatrulha[i];
            p.y += alturaLinha;
            lineRenderer.SetPosition(i + 1, p);
        }

        // Fecha o loop voltando ao ponto inicial (cria o circuito de patrulha visualmente)
        lineRenderer.SetPosition(totalPontos - 1, posInicial);
    }

    void AplicarOrdemPatrulha()
    {
        foreach (ControleUnidade unidade in gerenteSelecao.unidadesSelecionadas)
        {
            if (unidade == null)
            {
                continue;
            }

            unidade.EmitirOrdemPatrulha(pontosPatrulha);
        }
    }

    void AplicarOrdemSeguir(Transform alvo)
    {
        ControleUnidade unidadeAlvo = alvo != null ? alvo.GetComponent<ControleUnidade>() : null;
        foreach (ControleUnidade unidade in gerenteSelecao.unidadesSelecionadas)
        {
            if (unidade == null || unidade.transform == alvo)
            {
                continue;
            }

            if (unidadeAlvo != null && !PodeSeguirAlvo(unidade, unidadeAlvo))
            {
                Debug.LogWarning($"[{unidade.name}] ignorou SEGUIR porque o alvo nao e aliado.");
                continue;
            }

            unidade.EmitirOrdemSeguir(alvo);
        }
    }

    bool PodeSeguirAlvo(ControleUnidade seguidor, ControleUnidade alvo)
    {
        if (seguidor == null || alvo == null)
        {
            return false;
        }

        IdentidadeUnidade idSeguidor = seguidor.GetComponent<IdentidadeUnidade>();
        IdentidadeUnidade idAlvo = alvo.GetComponent<IdentidadeUnidade>();
        if (idSeguidor == null || idAlvo == null)
        {
            return true;
        }

        return idSeguidor.teamID == idAlvo.teamID;
    }

}

public class ComportamentoPatrulhaUniversal : MonoBehaviour
{
    private List<Vector3> pontos;
    private int indiceAtual = 0;
    private int indiceDesignado = -1;
    private float tempoUltimoComando = 0f;
    private ControleUnidade controle;
    private NavMeshAgent agente;
    private ControleNavioRealista navioRealista;
    private bool ehNaval;
    private bool ehAereo;

    public void Configurar(List<Vector3> novosPontos)
    {
        pontos = new List<Vector3>(novosPontos);
        indiceAtual = 0;
        indiceDesignado = -1;
        controle = GetComponent<ControleUnidade>();
        agente = GetComponent<NavMeshAgent>();
        navioRealista = GetComponent<ControleNavioRealista>();
        ehNaval = controle != null && controle.EhUnidadeNaval();
        ehAereo = controle != null && controle.DominioAtual == DominioControleUnidade.Aereo;
    }

    void Update()
    {
        if (pontos == null || pontos.Count == 0 || controle == null)
        {
            return;
        }

        if (controle.OrdemAtual != OrdemControleUnidade.Patrulhando)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        Vector3 alvo = pontos[indiceAtual];
        
        // Verifica distância no plano 2D para evitar que subidas curtas façam o carro engasgar
        Vector3 posPlano = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 alvoPlano = new Vector3(alvo.x, 0, alvo.z);
        float distancia = Vector3.Distance(posPlano, alvoPlano);
        
        // Margem de chegada: navios com ControleNavioRealista usam distanciaChegada própria
        float margem;
        if (ehAereo)
        {
            margem = 45f;
        }
        else if (ehNaval && navioRealista != null)
        {
            margem = navioRealista.distanciaChegada + 5f;
        }
        else if (ehNaval)
        {
            margem = 25f;
        }
        else if (agente != null && agente.enabled && !agente.updatePosition)
        {
            margem = 15f;
        }
        else
        {
            margem = 6f;
        }

        if (distancia < margem)
        {
            indiceAtual++;
            if (indiceAtual >= pontos.Count)
            {
                indiceAtual = 0;
            }
            // Reseta para forçar emissão imediata pro novo ponto
            indiceDesignado = -1;
            return;
        }

        // Guarda Global: Se o destino já foi designado e a unidade já está navegando para ele,
        // NÃO re-emite o comando (isso evita o "hiccup" de recalcular path a cada 3 segundos).
        bool agenteTemRotaAtiva = agente != null && agente.enabled && agente.isOnNavMesh
                                  && (agente.hasPath || agente.pathPending);
        
        if (indiceDesignado == indiceAtual && agenteTemRotaAtiva)
        {
            return; 
        }

        // Intervalo de segurança (caso a unidade se perca ou pare por colisão externa)
        float intervaloSeguranca = ehNaval ? 10f : 5f;


        if (indiceDesignado != indiceAtual || Time.time - tempoUltimoComando > intervaloSeguranca)
        {
            indiceDesignado = indiceAtual;
            tempoUltimoComando = Time.time;
            controle.EmitirOrdemMover(alvo, false);
        }
    }
}

public class ComportamentoSeguirUniversal : MonoBehaviour
{
    private Transform alvoSeguido;
    private ControleUnidade controle;
    private float distanciaIdeal = 45f;
    private float tempoProximaAtualizacao = 0f;
    private bool ehNaval;
    private bool ehAereo;
    private float intervaloAtualizacao = 0.5f;
    private float offsetLateralNaval = 0f;

    public void Configurar(Transform novoAlvo)
    {
        alvoSeguido = novoAlvo;
        controle = GetComponent<ControleUnidade>();
        ehNaval = controle != null && controle.EhUnidadeNaval();
        ehAereo = controle != null && controle.DominioAtual == DominioControleUnidade.Aereo;
        distanciaIdeal = ehNaval ? 170f : (ehAereo ? (controle != null && controle.TemHelicopteroExterno ? 60f : 140f) : 45f);
        intervaloAtualizacao = ehNaval ? 0.2f : (ehAereo ? 0.25f : 0.5f);
        offsetLateralNaval = ehNaval ? (((GetInstanceID() & 1) == 0) ? 50f : -50f) : 0f;
    }

    void Update()
    {
        if (alvoSeguido == null || controle == null)
        {
            Destroy(this);
            return;
        }

        if (controle.OrdemAtual != OrdemControleUnidade.Seguindo)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        if (Time.time <= tempoProximaAtualizacao)
        {
            return;
        }

        if (ehNaval)
        {
            AtualizarSeguimentoNaval();
        }
        else if (ehAereo)
        {
            AtualizarSeguimentoAereo();
        }
        else
        {
            AtualizarSeguimentoPadrao();
        }

        tempoProximaAtualizacao = Time.time + intervaloAtualizacao;
    }

    void OnDestroy()
    {
        if (controle != null)
        {
            controle.RestaurarVelocidadeOriginal();
        }
    }

    void AtualizarSeguimentoPadrao()
    {
        float distancia = Vector3.Distance(transform.position, alvoSeguido.position);

        if (distancia > distanciaIdeal)
        {
            Vector3 posicaoEscolta = alvoSeguido.position - (alvoSeguido.forward * (distanciaIdeal * 0.8f));
            controle.EmitirOrdemMover(posicaoEscolta, false);

            if (distancia < distanciaIdeal + 40f)
            {
                ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
                if (controleLider != null)
                {
                    float velLider = controleLider.ObterVelocidadeAtualReal();
                    if (velLider > 0.5f)
                    {
                        controle.AplicarLimiteVelocidade(velLider * 1.15f);
                    }
                    else
                    {
                        controle.RestaurarVelocidadeOriginal();
                    }
                }
            }
            else
            {
                controle.RestaurarVelocidadeOriginal();
            }
        }
        else
        {
            ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
            if (distancia < distanciaIdeal * 0.6f)
            {
                controle.AplicarLimiteVelocidade(0.1f);
                controle.EmitirOrdemMover(transform.position, false);
            }
            else if (controleLider != null)
            {
                controle.AplicarLimiteVelocidade(controleLider.ObterVelocidadeAtualReal());
            }
        }
    }

    void AtualizarSeguimentoAereo()
    {
        ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
        float velocidadeLider = controleLider != null ? controleLider.ObterVelocidadeAtualReal() : 0f;

        Vector3 frenteLider = Flatten(alvoSeguido.forward);
        if (frenteLider.sqrMagnitude < 0.01f)
        {
            frenteLider = Flatten(alvoSeguido.position - transform.position);
            if (frenteLider.sqrMagnitude < 0.01f)
            {
                frenteLider = Vector3.forward;
            }
        }
        frenteLider.Normalize();

        float offsetLateral = controle != null && controle.TemHelicopteroExterno ? 18f : 55f;
        if ((GetInstanceID() & 1) != 0)
        {
            offsetLateral *= -1f;
        }

        float altitudeOffset = controle != null && controle.TemHelicopteroExterno ? 18f : 70f;
        Vector3 direitaLider = new Vector3(frenteLider.z, 0f, -frenteLider.x);
        Vector3 destinoEscolta = alvoSeguido.position - (frenteLider * distanciaIdeal) + (direitaLider * offsetLateral);
        destinoEscolta.y = Mathf.Max(alvoSeguido.position.y + altitudeOffset, transform.position.y);

        float distanciaAtual = Vector3.Distance(transform.position, destinoEscolta);
        if (distanciaAtual > 18f)
        {
            controle.EmitirOrdemMover(destinoEscolta, false);
        }

        if (controleLider != null)
        {
            if (velocidadeLider > 0.5f)
            {
                float multiplicador = controle.TemHelicopteroExterno ? 1.05f : 1.1f;
                controle.AplicarLimiteVelocidade(Mathf.Max(velocidadeLider * multiplicador, 3f));
            }
            else if (controle.TemHelicopteroExterno)
            {
                controle.AplicarLimiteVelocidade(3f);
            }
            else
            {
                controle.RestaurarVelocidadeOriginal();
            }
        }
    }

    void AtualizarSeguimentoNaval()
    {
        ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
        float velocidadeLider = controleLider != null ? controleLider.ObterVelocidadeAtualReal() : 0f;

        Vector3 frenteLider = alvoSeguido.forward;
        frenteLider.y = 0f;
        if (frenteLider.sqrMagnitude < 0.01f)
        {
            frenteLider = Flatten(alvoSeguido.position - transform.position);
            if (frenteLider.sqrMagnitude < 0.01f)
            {
                frenteLider = Vector3.forward;
            }
        }
        frenteLider.Normalize();

        Vector3 direitaLider = new Vector3(frenteLider.z, 0f, -frenteLider.x);
        Vector3 destinoEscolta = alvoSeguido.position - (frenteLider * distanciaIdeal) + (direitaLider * offsetLateralNaval);

        float distanciaLider = PlanarDistance(transform.position, alvoSeguido.position);
        float distanciaDestino = PlanarDistance(transform.position, destinoEscolta);
        bool estaNaFrente = Vector3.Dot(Flatten(transform.position - alvoSeguido.position), frenteLider) > 0f;

        if (estaNaFrente || distanciaLider < 75f)
        {
            float velocidadeFreio = velocidadeLider > 0.5f ? Mathf.Max(0.5f, velocidadeLider * 0.65f) : 0.1f;
            controle.AplicarLimiteVelocidade(velocidadeFreio);
            controle.EmitirOrdemMover(destinoEscolta, false);
            return;
        }

        if (distanciaDestino > 18f)
        {
            controle.EmitirOrdemMover(destinoEscolta, false);

            if (distanciaLider > 140f)
            {
                controle.RestaurarVelocidadeOriginal();
                return;
            }

            if (velocidadeLider > 0.5f)
            {
                float velocidadeEscolta = distanciaLider > 95f ? velocidadeLider * 1.05f : velocidadeLider;
                controle.AplicarLimiteVelocidade(velocidadeEscolta);
            }
            else
            {
                controle.AplicarLimiteVelocidade(Mathf.Clamp(distanciaDestino * 0.08f, 1.5f, 6f));
            }
            return;
        }

        if (velocidadeLider > 0.5f)
        {
            controle.AplicarLimiteVelocidade(velocidadeLider);
            controle.EmitirOrdemMover(destinoEscolta, false);
        }
        else
        {
            controle.AplicarLimiteVelocidade(0.1f);
            controle.EmitirOrdemMover(transform.position, false);
        }
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(Flatten(a), Flatten(b));
    }

    static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
