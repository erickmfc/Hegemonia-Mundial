using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class DesenharLinhasOrdem : MonoBehaviour
{
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
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
        lineRenderer.positionCount = 0;
    }

    public void IniciarModoPatrulha()
    {
        modoPatrulhaAtivo = true;
        modoSeguirAtivo = false;
        pontosPatrulha.Clear();
        lineRenderer.positionCount = 0;
        Debug.Log("MODO PATRULHA: clique com o botao esquerdo para iniciar. SHIFT + clique esquerdo adiciona pontos extras. ENTER confirma rota multipla. BOTAO DIREITO ou ESC cancelam.");
    }

    public void IniciarModoSeguir()
    {
        modoSeguirAtivo = true;
        modoPatrulhaAtivo = false;
        lineRenderer.positionCount = 0;
        Debug.Log("MODO SEGUIR: clique com o botao esquerdo em uma unidade aliada. BOTAO DIREITO ou ESC cancelam.");
    }

    void Update()
    {
        if (gerenteSelecao == null || gerenteSelecao.unidadesSelecionadas.Count == 0)
        {
            LimparTudo();
            return;
        }

        if (modoPatrulhaAtivo && Input.GetMouseButtonDown(0))
        {
            Vector3 pontoPatrulha;
            if (TryObterPontoPatrulha(out pontoPatrulha))
            {
                pontosPatrulha.Add(pontoPatrulha);
                AtualizarLinhaVisualPatrulha();
                MostrarMarcadorPatrulha(pontoPatrulha);

                bool adicionandoRotaMultipla = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (!adicionandoRotaMultipla)
                {
                    AplicarOrdemPatrulha();
                    Debug.Log("Patrulha iniciada.");
                    LimparTudo();
                    return;
                }
            }
        }

        if (modoPatrulhaAtivo && Input.GetMouseButtonDown(1))
        {
            Debug.Log("Modo patrulha cancelado.");
            LimparTudo();
            return;
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
            Debug.Log("Modo seguir cancelado.");
            LimparTudo();
            return;
        }

        if (modoSeguirAtivo && Input.GetMouseButtonDown(0))
        {
            if (Camera.main == null)
            {
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                ControleUnidade alvo = hit.collider.GetComponentInParent<ControleUnidade>();
                if (alvo != null)
                {
                    AplicarOrdemSeguir(alvo.transform);
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

    void MostrarMarcadorPatrulha(Vector3 pontoPatrulha)
    {
        if (prefabMarcadorPatrulha == null)
        {
            return;
        }

        GameObject marcador = Instantiate(prefabMarcadorPatrulha, pontoPatrulha + Vector3.up * 0.1f, Quaternion.identity);
        marcador.transform.localScale = new Vector3(11f, 11f, 11f);
        Destroy(marcador, 3.0f);
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
            return;
        }

        Vector3 posInicial = gerenteSelecao.unidadesSelecionadas[0].transform.position;
        lineRenderer.positionCount = pontosPatrulha.Count + 1;
        lineRenderer.SetPosition(0, posInicial + Vector3.up * 2f);

        for (int i = 0; i < pontosPatrulha.Count; i++)
        {
            lineRenderer.SetPosition(i + 1, pontosPatrulha[i] + Vector3.up * 2f);
        }
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
        foreach (ControleUnidade unidade in gerenteSelecao.unidadesSelecionadas)
        {
            if (unidade == null || unidade.transform == alvo)
            {
                continue;
            }

            unidade.EmitirOrdemSeguir(alvo);
        }
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
    private bool ehNaval;

    public void Configurar(List<Vector3> novosPontos)
    {
        pontos = new List<Vector3>(novosPontos);
        indiceAtual = 0;
        indiceDesignado = -1;
        controle = GetComponent<ControleUnidade>();
        agente = GetComponent<NavMeshAgent>();
        ehNaval = controle != null && controle.EhUnidadeNaval();
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
        
        float margem = ehNaval ? 20f : ((agente != null && agente.enabled && !agente.updatePosition) ? 15f : 6f);

        if (distancia < margem)
        {
            indiceAtual++;
            if (indiceAtual >= pontos.Count)
            {
                indiceAtual = 0;
            }
            return; // Espera o próximo frame para comandar ir para o novo ponto
        }

        // Manda ir para o ponto APENAS se o alvo mudou (evita recalculo de rota todo frame e engasgos curtos)
        if (indiceDesignado != indiceAtual || Time.time - tempoUltimoComando > 2f)
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
    private float intervaloAtualizacao = 0.5f;
    private float offsetLateralNaval = 0f;

    public void Configurar(Transform novoAlvo)
    {
        alvoSeguido = novoAlvo;
        controle = GetComponent<ControleUnidade>();
        ehNaval = controle != null && controle.EhUnidadeNaval();
        distanciaIdeal = ehNaval ? 170f : 45f;
        intervaloAtualizacao = ehNaval ? 0.2f : 0.5f;
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
