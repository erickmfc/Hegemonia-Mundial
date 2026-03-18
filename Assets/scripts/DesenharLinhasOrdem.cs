using UnityEngine;
using UnityEngine.AI; // Necessário para acessar o NavMesh
using System.Collections.Generic;

public class DesenharLinhasOrdem : MonoBehaviour
{
    public LineRenderer lineRenderer;
    private GerenteSelecao gerenteSelecao;
    
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
        Debug.Log("🎯 MODO PATRULHA: Clique com DIREITO no chão para criar a rota.");
    }

    public void IniciarModoSeguir()
    {
        modoSeguirAtivo = true;
        modoPatrulhaAtivo = false;
        lineRenderer.positionCount = 0;
        Debug.Log("🎯 MODO SEGUIR: Clique com DIREITO em uma unidade aliada.");
    }

    void Update()
    {
        if (gerenteSelecao == null || gerenteSelecao.unidadesSelecionadas.Count == 0)
        {
            LimparTudo();
            return;
        }

        // --- MODO PATRULHA ---
        if (modoPatrulhaAtivo)
        {
            if (Input.GetMouseButtonDown(1)) // Botão Direito
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    pontosPatrulha.Add(hit.point);
                    AtualizarLinhaVisualPatrulha();
                    AplicarOrdemPatrulha();
                }
            }
        }

        // --- MODO SEGUIR ---
        if (modoSeguirAtivo)
        {
            if (Input.GetMouseButtonDown(1)) // Botão Direito
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // Verifica se clicamos em alguma unidade
                    ControleUnidade alvo = hit.collider.GetComponentInParent<ControleUnidade>();
                    if (alvo != null)
                    {
                        AplicarOrdemSeguir(alvo.transform);
                        Debug.Log("🎯 Ordem de SEGUIR confirmada para o alvo!");
                        modoSeguirAtivo = false; // Desliga o modo após clicar
                    }
                    else
                    {
                        Debug.LogWarning("Você não clicou em uma unidade para seguir!");
                    }
                }
            }
        }

        // ESC para cancelar
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

    void AtualizarLinhaVisualPatrulha()
    {
        if (pontosPatrulha.Count == 0 || gerenteSelecao.unidadesSelecionadas.Count == 0) return;

        Vector3 posInicial = gerenteSelecao.unidadesSelecionadas[0].transform.position;
        lineRenderer.positionCount = pontosPatrulha.Count + 1;
        
        lineRenderer.SetPosition(0, posInicial + Vector3.up * 2f);

        for (int i = 0; i < pontosPatrulha.Count; i++)
        {
            lineRenderer.SetPosition(i + 1, pontosPatrulha[i] + Vector3.up * 2f);
        }
    }

    // --- ENVIANDO ORDENS PARA AS UNIDADES ---

    void AplicarOrdemPatrulha()
    {
        foreach (var unidade in gerenteSelecao.unidadesSelecionadas)
        {
            if (unidade == null) continue;
            
            // Remove comportamento de seguir se houver
            if (unidade.GetComponent<ComportamentoSeguirUniversal>()) Destroy(unidade.GetComponent<ComportamentoSeguirUniversal>());

            ComportamentoPatrulhaUniversal pat = unidade.GetComponent<ComportamentoPatrulhaUniversal>();
            if (pat == null) pat = unidade.gameObject.AddComponent<ComportamentoPatrulhaUniversal>();
            
            pat.Configurar(pontosPatrulha);
        }
    }

    void AplicarOrdemSeguir(Transform alvo)
    {
        foreach (var unidade in gerenteSelecao.unidadesSelecionadas)
        {
            if (unidade == null || unidade.transform == alvo) continue; // Não pode seguir a si mesmo

            // Remove comportamento de patrulha se houver
            if (unidade.GetComponent<ComportamentoPatrulhaUniversal>()) Destroy(unidade.GetComponent<ComportamentoPatrulhaUniversal>());

            ComportamentoSeguirUniversal seg = unidade.GetComponent<ComportamentoSeguirUniversal>();
            if (seg == null) seg = unidade.gameObject.AddComponent<ComportamentoSeguirUniversal>();
            
            seg.Configurar(alvo);
        }
    }
}

// =========================================================================
// CÉREBROS UNIVERSAIS (Ficam no mesmo arquivo para facilitar sua vida)
// =========================================================================

public class ComportamentoPatrulhaUniversal : MonoBehaviour
{
    private List<Vector3> pontos;
    private int indiceAtual = 0;
    private ControleUnidade controle;
    private NavMeshAgent agente;

    public void Configurar(List<Vector3> novosPontos)
    {
        pontos = new List<Vector3>(novosPontos);
        indiceAtual = 0;
        controle = GetComponent<ControleUnidade>();
        agente = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (pontos == null || pontos.Count == 0 || controle == null) return;

        Vector3 alvo = pontos[indiceAtual];
        
        // Verifica a distância até o ponto atual
        float distancia = Vector3.Distance(transform.position, alvo);
        
        // Folga maior para aviões e navios (que fazem curvas largas)
        float margem = (agente != null && agente.enabled && !agente.updatePosition) ? 15f : 5f;

        if (distancia < margem) 
        {
            indiceAtual++;
            if (indiceAtual >= pontos.Count) indiceAtual = 0; // Volta para o primeiro (Loop)
            return;
        }

        // Usa o sistema de movimento inteligente da unidade (Voo, Naval ou Terrestre)
        controle.MoverParaPonto(alvo, false); // false = não cancelar comportamentos
    }
}

public class ComportamentoSeguirUniversal : MonoBehaviour
{
    private Transform alvoSeguido;
    private ControleUnidade controle;
    private float distanciaIdeal = 15f; 
    private float tempoProximaAtualizacao = 0f;

    public void Configurar(Transform novoAlvo)
    {
        alvoSeguido = novoAlvo;
        controle = GetComponent<ControleUnidade>();
    }

    void Update()
    {
        if (alvoSeguido == null || controle == null)
        {
            Destroy(this); 
            return;
        }

        // Atualiza o destino a cada 0.5 segundos para não sobrecarregar
        if (Time.time > tempoProximaAtualizacao)
        {
            float distancia = Vector3.Distance(transform.position, alvoSeguido.position);

            if (distancia > distanciaIdeal)
            {
                // Calcula posição de escolta (um pouco atrás do alvo)
                Vector3 posicaoEscolta = alvoSeguido.position - (alvoSeguido.forward * (distanciaIdeal * 0.5f));
                controle.MoverParaPonto(posicaoEscolta, false);
                
                // --- IGUALAR VELOCIDADE AO LÍDER (Se estiver perto) ---
                if (distancia < distanciaIdeal + 30f)
                {
                    ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
                    if (controleLider != null)
                    {
                        float velLider = controleLider.ObterVelocidadeAtualReal();
                        if (velLider > 0.5f) controle.AplicarLimiteVelocidade(velLider * 1.15f); // 15% mais rápido pra alcançar
                        else controle.RestaurarVelocidadeOriginal(); 
                    }
                }
                else
                {
                    // Se estiver muito longe, restaura a velocidade máxima para "correr atrás"
                    controle.RestaurarVelocidadeOriginal();
                }
            }
            else
            {
                 // Se já está na posição ideal, tenta não ultrapassar
                 ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
                 if (controleLider != null) controle.AplicarLimiteVelocidade(controleLider.ObterVelocidadeAtualReal());
            }
            
            tempoProximaAtualizacao = Time.time + 0.5f;
        }
    }
}