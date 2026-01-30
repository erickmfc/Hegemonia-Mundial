using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SistemaDeTiro : MonoBehaviour
{
    [Header("Comportamento IA")]
    public bool modoPassivo = false; // Se true, só ataca se mandarem
    public string etiquetaAlvo = "Inimigo"; 
    public float intervaloEntreTiros = 0.5f;
    private float tempoParaProximoTiro = 0f;
    private Transform alvoAtual;

    [Header("Configuração de Munição")]
    public GameObject prefabProjetil; // A munição
    public int capacidadePente = 30;  // Quantidade de balas
    public float tempoRecarga = 2.0f; // Tempo para trocar o pente
    private int balasAtuais;
    private bool recarregando = false;

    [Header("Configuração de Alcance e Balística")]
    public float alcanceTiro = 50f;   // Distância máxima (Visual e Lógica)
    
    [Tooltip("Controla a velocidade visual da bala (Metros por segundo).")]
    public float velocidadeDoTiro = 60f; // Nova variável pedida
    
    public Transform bocaDoCano;      // O ponto de saída
    
    [Tooltip("Força física aplicada APENAS se a bala tiver Rigidbody (Impacto). Não afeta a velocidade de voo.")]
    public float forcaDoTiro = 1000f; 

    [Header("Áudio")]
    public AudioClip somTiro; 
    public AudioClip somRecarga; 
    public AudioClip somSemMuni; 
    private AudioSource fonteAudio;

    private ControleUnidade selecao; 
    private IdentidadeUnidade minhaIdentidade;

    // OTIMIZAÇÃO: Buffer de colisão para evitar GC
    private Collider[] bufferColisores = new Collider[50];

    void Update()
    {
        // Se estiver recarregando ou em modo passivo, não faz nada
        if (recarregando || modoPassivo) return;

        // Decrementa o cooldown
        if (tempoParaProximoTiro > 0) tempoParaProximoTiro -= Time.deltaTime;

        // Se temos um alvo válido
        if (alvoAtual != null)
        {
            // Validação de segurança: Alvo foi destruído?
            if (alvoAtual == null || !alvoAtual.gameObject.activeInHierarchy)
            {
                alvoAtual = null;
                return;
            }

            // Verifica distância (o Scan faz isso, mas o Update é mais rápido para parar de atirar se o alvo fugir)
            float dist = Vector3.Distance(transform.position, alvoAtual.position);
            if (dist > alcanceTiro)
            {
                alvoAtual = null; // Perde o alvo se sair do alcance
                return;
            }

            // Gira em direção ao alvo (Apenas Y para soldados terrestres)
            Vector3 direcao = (alvoAtual.position - transform.position).normalized;
            direcao.y = 0; // Mantém o soldado em pé
            
            float anguloParaAlvo = 180f; // Valor padrão alto para não atirar se direção for zero
            
            if (direcao != Vector3.zero)
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                
                // VELOCIDADE DE ROTAÇÃO: 45 graus/segundo para alinhamento rápido
                transform.root.rotation = Quaternion.Slerp(transform.root.rotation, rotacaoAlvo, Time.deltaTime * 45f);
                
                // Calcula o ângulo atual entre a frente do tanque e o alvo
                anguloParaAlvo = Vector3.Angle(transform.root.forward, direcao);
            }

            // Lógica de Tiro - SÓ ATIRA SE ESTIVER APONTANDO PRO ALVO
            // Adiciona verificação: o tanque precisa estar virado para o alvo (< 10 graus - mais preciso)
            if (tempoParaProximoTiro <= 0 && anguloParaAlvo < 10f)
            {
                if (balasAtuais > 0)
                {
                    Atirar();
                    tempoParaProximoTiro = intervaloEntreTiros;
                }
                else
                {
                    StartCoroutine(RotinaRecarga());
                }
            }
        }
    }

    void Start()
    {
        balasAtuais = capacidadePente; 
        selecao = GetComponentInParent<ControleUnidade>();
        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();

        // AUTO-CORREÇÃO: Se eu (o atirador) não tenho Identidade, crio uma como Time 1 (Jogador)
        if (minhaIdentidade == null)
        {
            var root = transform.root.gameObject;
            minhaIdentidade = root.AddComponent<IdentidadeUnidade>();
            minhaIdentidade.teamID = 1; // Padrão Jogador
            minhaIdentidade.nomeDoPais = "Minha Nação";
            // Debug.Log($"[SistemaDeTiro] Criei Identidade (Team 1) em {root.name} para poder identificar inimigos.");
        }

        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null) fonteAudio = gameObject.AddComponent<AudioSource>();
        fonteAudio.spatialBlend = 1.0f; 

        // OTIMIZAÇÃO: Scan com intervalo aleatório para evitar picos de CPU
        float inicioAleatorio = Random.Range(0f, 1.0f);
        InvokeRepeating("ProcurarAlvo", inicioAleatorio, 0.5f);
    }
    
    void ProcurarAlvo()
    {
        if (modoPassivo || recarregando) return;

        // OTIMIZAÇÃO: Usa NonAlloc para não gerar lixo
        int qtd = Physics.OverlapSphereNonAlloc(transform.position, alcanceTiro, bufferColisores);
        
        float menorDist = Mathf.Infinity;
        Transform melhorAlvo = null;

        // Lazy Load (Garante que pegamos a identidade mesmo se foi adicionada depois do Start)
        if (minhaIdentidade == null) minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();

        for (int i = 0; i < qtd; i++)
        {
            Collider hit = bufferColisores[i];
            if (hit == null) continue;

            // Ignora a si mesmo (Root vs Root para garantir partes do veículo)
            if (hit.transform.root == transform.root) continue; 

            // 1. TENTA POR IDENTIDADE (Mais seguro)
            IdentidadeUnidade idAlvo = hit.GetComponentInParent<IdentidadeUnidade>();
            
            bool ehInimigoConfirmado = false;
            
            // Pré-calcula a verificação de tag para usar como fallback ou override
            // Atenção: CompareTag é rápido, mas evitar strings literais em loops é melhor. Aqui ok.
            bool tagInimigoDetectada = hit.CompareTag(etiquetaAlvo) || hit.CompareTag("Inimigo"); // Simplificado

            if (idAlvo != null && minhaIdentidade != null)
            {
                // Lógica de Times (Prioridade 1)
                if (idAlvo.teamID != minhaIdentidade.teamID) 
                {
                    ehInimigoConfirmado = true;
                }
                // (FIX) Override por Tag: Se for do mesmo time mas tiver a tag "Inimigo", ataca igual.
                else if (tagInimigoDetectada)
                {
                    ehInimigoConfirmado = true;
                }
            }
            else 
            {
                // Lógica puramente por Tag (Prioridade 2 - Fallback)
                if (tagInimigoDetectada)
                {
                    ehInimigoConfirmado = true;
                }
            }

            if (ehInimigoConfirmado)
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < menorDist)
                {
                    menorDist = d;
                    melhorAlvo = hit.transform;
                }
            }
        }
        
        // Limpar buffer opcional
        for(int i=0; i<qtd; i++) bufferColisores[i] = null;

        if (melhorAlvo != null && alvoAtual != melhorAlvo)
        {
            // Debug.Log($"[SistemaDeTiro] 🎯 ALVO TRAVADO: {melhorAlvo.name} (Dist: {menorDist:F1}m)");
        }
        
        alvoAtual = melhorAlvo;
    }

    void Atirar()
    {
        // Se a boca do cano não estiver definida, usa a posição do próprio objeto
        Transform origem = (bocaDoCano != null) ? bocaDoCano : transform;

        GameObject bala = Instantiate(prefabProjetil, origem.position, origem.rotation);
        
        Projetil scriptBala = bala.GetComponent<Projetil>();
        if (scriptBala != null)
        {
            scriptBala.SetDono(transform.root.gameObject);
            
            // APLICA A VELOCIDADE CONFIGURADA NO SISTEMA
            scriptBala.velocidade = velocidadeDoTiro;

            // Se tiver alvo, podemos ajustar a MIRA (girar o boneco/arma) em vez de girar a BALA magicamente.
            // Mas se quisermos "Auto Aim" leve:
            if (alvoAtual != null)
            {
                 // Calcula a direção para o alvo
                 Vector3 direcaoAlvo = (alvoAtual.position + Vector3.up * 1.0f) - origem.position; // +1 no Y para mirar no peito/centro
                 
                 // Define a direção customizada no projétil para ele ir RETO nessa direção
                 scriptBala.SetDirecao(direcaoAlvo);
            }
            else
            {
                // Se não tem alvo (tiro cego), vai na direção que o cano está apontando
                scriptBala.SetDirecao(origem.forward);
            }
        }

        balasAtuais--;
        
        // SISTEMA DE SOM: Tenta usar o sistema novo de som (SomUnidade) primeiro
        var somUnidade = GetComponentInParent<SomUnidade>();
        if (somUnidade != null)
        {
            somUnidade.TocarSomTiro();
        }
        // Fallback: Sistema antigo de AudioSource
        else if (fonteAudio != null && somTiro != null)
        {
            fonteAudio.PlayOneShot(somTiro);
        }
        
        // Ativa animação se tiver o script de Animação
        var anim = GetComponentInParent<AnimacoesSoldado>();
        if(anim != null) anim.DefinirAtaque(true);
        
        // Reseta a animação depois de um tempo curto (opcional, ou melhor deixar o Update controlar)
        CancelInvoke("PararAnimacaoTiro");
        Invoke("PararAnimacaoTiro", 0.1f);
    }
    
    void PararAnimacaoTiro()
    {
        var anim = GetComponentInParent<AnimacoesSoldado>();
        if(anim != null) anim.DefinirAtaque(false);
    }

    IEnumerator RotinaRecarga()
    {
        recarregando = true;
        if(somSemMuni != null) fonteAudio.PlayOneShot(somSemMuni); // Click seco
        Debug.Log("Recarregando...");
        
        if(somRecarga != null) 
        {
            yield return new WaitForSeconds(0.2f);
            fonteAudio.PlayOneShot(somRecarga);
        }
        
        yield return new WaitForSeconds(tempoRecarga);

        balasAtuais = capacidadePente;
        recarregando = false;
    }

    public void DefinirModoPassivo(bool estado)
    {
        modoPassivo = estado;
        if (modoPassivo)
        {
            alvoAtual = null; // Para de mirar imediatamente
            // Opcional: Cancelar recarga se quiser ser muito estrito, mas deixar recarregar é bom.
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5f); 
        Gizmos.DrawWireSphere(transform.position, alcanceTiro);
    }
}
