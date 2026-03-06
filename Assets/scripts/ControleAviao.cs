using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// O CÉREBRO COMPLETO DO AVIÃO.
/// Divide o movimento em 2 modos: Solo (rígido e guiado) e Voo (solto e realista).
/// </summary>
public class ControleAviao : MonoBehaviour
{
    public enum EstadoAviao { ReservaHangar, Taxiando, ProntoNoPatio, Decolando, EmMissao, Pousando, RetornandoPraVaga }
    
    [Header("=== ESTADO ATUAL ===")]
    public EstadoAviao estadoAtual = EstadoAviao.ReservaHangar;
    [HideInInspector] public GerenciadorAeroporto aeroportoOrigem;
    [HideInInspector] public Transform vagaRetorno;
    [HideInInspector] public bool aguardandoCliqueRadar = false;
    [Header("=== MISSÃO ===")]
    public bool ordemParaRetorno = false;

    [Header("=== FÍSICA E VELOCIDADES ===")]
    [Tooltip("Velocidade rígida no chão para não fazer zig-zag")]
    public float velocidadeSolo = 10f; 
    [Tooltip("Velocidade de voo nas nuvens")]
    public float velocidadeMaximaVoo = 150f;
    public float taxaDeGiroLeme = 60f;

    [Header("=== ANIMAÇÃO VISUAL ===")]
    public Transform modeloMecanicoVisual; 
    public float asaBankingMaximo = 75f; 
    public float arfagemPitchMaxima = 30f; 

    [Header("=== TREM DE POUSO ===")]
    public List<Transform> rodas;
    private List<Quaternion> rotacoesOriginaisRodas = new List<Quaternion>();
    private bool rodasRecolhidas = false;

    // Variáveis internas
    public Vector3 alvoGPSVoo;
    public bool estaEmModoVooFisico = false;
    private float giroLateralRoll = 0f; 
    private float empinadaPitch = 0f;   
    private float multiplicadorVelocidadeTurbo = 1f;

    void Start()
    {
        // CORREÇÃO: Desliga a gravidade e colisões físicas para o avião não quicar no asfalto!
        // Quando usamos transform.position (movimento forçado), a física deve ser 'Kinematic'.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; 
        }

        // AUTO-DETECÇÃO DE RODAS (Caso o usuário tenha esquecido de preencher no Inspector)
        if (rodas == null) rodas = new List<Transform>();
        if (rodas.Count == 0)
        {
            foreach (Transform f in GetComponentsInChildren<Transform>(true))
            {
                string n = f.name.ToLower();
                if (n.Contains("wheel") || n.Contains("roda") || n.Contains("gear") || n.Contains("pneu") || n.Contains("tremdepouso"))
                {
                    if (f != transform) rodas.Add(f); 
                }
            }
            if (rodas.Count > 0) Debug.Log($"[{gameObject.name}] Auto-detectou {rodas.Count} pedaços de trem de pouso/rodas!");
            else Debug.LogWarning($"[{gameObject.name}] O script NÃO achou rodas! O avião não poderá recolhê-las.");
        }

        foreach (var roda in rodas)
        {
            if (roda != null) rotacoesOriginaisRodas.Add(roda.localRotation);
            else rotacoesOriginaisRodas.Add(Quaternion.identity);
        }
        AbaixarRodas(); // Garante que começam ativas e no lugar certo
    }

    void Update()
    {
        // Só usa a matemática louca de avião se estiver no MODO VOO.
        // Se estiver no chão (taxiando), o MoverInterpolado cuida de tudo perfeitamente.
        if (estaEmModoVooFisico)
        {
            ControleUnidade cu = GetComponent<ControleUnidade>();
            bool selecionado = (cu != null && cu.selecionado);

            // TURBO: Segurar o TAB acelera o dobro (atinge a vel. máxima em 5 seg)
            if (selecionado && Input.GetKey(KeyCode.Tab))
            {
                multiplicadorVelocidadeTurbo = Mathf.MoveTowards(multiplicadorVelocidadeTurbo, 2f, (1f / 5f) * Time.deltaTime);
            }
            else
            {
                multiplicadorVelocidadeTurbo = Mathf.MoveTowards(multiplicadorVelocidadeTurbo, 1f, (1f / 5f) * Time.deltaTime);
            }

            ManobraVooRealista();
        }
    }

    // ==========================================
    // MODO SOLO: TAXIANDO SEM ZIG ZAG
    // ==========================================
    
    /// <summary>
    /// Movimento estrito e preciso para vias urbanas/aeroportos. MoveTo garante parada exata.
    /// </summary>
    public IEnumerator MoverInterpolado(Vector3 destinoOriginal, float vel, bool pontoFinal = false)
    {
        Vector3 destinoPlano = new Vector3(destinoOriginal.x, transform.position.y, destinoOriginal.z);
        float raioDeAceitacao = pontoFinal ? 0.5f : 1.5f; // Aumentar levemente o raio mínimo de 0.2f para 0.5f

        // Enquanto a distância no plano 2D for maior que raioDeAceitacao, continua andando
        while (Vector3.Distance(transform.position, destinoPlano) > raioDeAceitacao)
        {
            // Atualiza o Y caso o avião esteja descendo uma rampa
            destinoPlano = new Vector3(destinoOriginal.x, transform.position.y, destinoOriginal.z);
            Vector3 vetorAteDestino = destinoPlano - transform.position;
            
            // PROTEÇÃO ANTI-360º: Se o avião sobrevoou o ponto alvo ou o cruzou lateralmente muito perto, 
            // e o alvo agora ficou para "trás", interrompe o trajeto para que ele não tente dar a volta.
            if (vetorAteDestino.magnitude < 4f && Vector3.Dot(transform.forward, vetorAteDestino.normalized) < 0f)
            {
                break;
            }

            // 1. Olha para o ponto
            Vector3 direcao = vetorAteDestino.normalized;
            
            if (direcao != Vector3.zero)
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                float angulo = Quaternion.Angle(transform.rotation, rotacaoAlvo);

                // Gira mais suave, como rodas virando
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, 50f * Time.deltaTime);

                // Se o avião está muito desalinhado na curva, reduz um pouco a velocidade para não sair do trajeto
                float fatorVelocidade = Mathf.Clamp01(1.2f - (angulo / 45f));
                if (fatorVelocidade < 0.2f) fatorVelocidade = 0.2f;

                // Move puxando para a direção que mira, garantindo que não vai orbitar infinitamente
                Vector3 direcaoMovimento = Vector3.Lerp(transform.forward, direcao, 0.4f).normalized;

                transform.position += direcaoMovimento * (vel * fatorVelocidade) * Time.deltaTime;
            }

            // Garante que as asas estão retas (Tira o banking visual)
            if (modeloMecanicoVisual != null)
            {
                modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.identity, Time.deltaTime * 5f);
            }

            yield return null;
        }

        // Se for o último ponto, crava a posição para não ter erro
        // Só crava se realmente estiver muito perto (prevenindo o teletransporte se ele sofreu "overshot" um pouco longe)
        if (pontoFinal)
        {
            Vector3 finalPlanoPos = new Vector3(destinoOriginal.x, transform.position.y, destinoOriginal.z);
            if (Vector3.Distance(transform.position, finalPlanoPos) < 2.5f)
            {
                transform.position = finalPlanoPos;
            }
        }
    }

    public IEnumerator SeguirCaminhoDeWaypoints(List<Transform> caminho, float velInicial, float velFinal, bool aceleracaoGradativa = false)
    {
        for (int i = 0; i < caminho.Count; i++)
        {
            if (caminho[i] != null)
            {
                bool eHUultimoPonto = (i == caminho.Count - 1);
                
                float velAtual = velInicial;
                
                // Se for pra acelerar/frear gradativamente, calculamos baseado no progresso dos waypoints
                if (aceleracaoGradativa && caminho.Count > 1)
                {
                    float progresso = (float)i / (caminho.Count - 1);
                    velAtual = Mathf.Lerp(velInicial, velFinal, progresso);
                }

                yield return StartCoroutine(MoverInterpolado(caminho[i].position, velAtual, eHUultimoPonto));
            }
        }
    }

    // ==========================================
    // MODO VOO: ROTINA DE MISSÃO COMPLETA
    // ==========================================

    public void IniciarMissaoCompleta(Vector3 alvoFinalGPS)
    {
        if (estadoAtual == EstadoAviao.ProntoNoPatio)
        {
            alvoGPSVoo = alvoFinalGPS;
            StartCoroutine(SequenciaDeVooEPouso());
        }
    }

    // Função pública para ser chamada por Botões/UI/Comandos
    public void ComandoRetornarBase()
    {
        if (estadoAtual == EstadoAviao.EmMissao)
        {
            ordemParaRetorno = true;
            Debug.Log($"[{gameObject.name}] Recebeu ordem de RETORNO para a base!");
        }
    }

    private IEnumerator SequenciaDeVooEPouso()
    {
        ordemParaRetorno = false; // Garante que começa falso
        
        // 1. DECOLAGEM (Chão)
        estadoAtual = EstadoAviao.Decolando;
        Debug.Log($"[{gameObject.name}] Iniciando taxiamento e decolagem!");
        
        // Segue os waypoints de decolagem do aeroporto rigorosamente (Aumentando do Zero até a Velocidade do Voo)
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsDecolagem, velocidadeSolo, velocidadeMaximaVoo, true));

        // 2. VOO FÍSICO (Céu)
        estadoAtual = EstadoAviao.EmMissao;
        estaEmModoVooFisico = true;
        
        // Garante que a missão ocorra no alto (Sempre acima de 60 metros)
        if (alvoGPSVoo.y < 60f) alvoGPSVoo.y = 60f;
        Vector3 pontoCentralMissao = alvoGPSVoo;

        Debug.Log($"[{gameObject.name}] Saiu do chão. Voando para o Alvo!");

        // Inicia o processo de recolher o trem de pouso após 3 segundos
        StartCoroutine(RecolherRodas(3f));

        // Espera chegar no alvo (Distância horizontal menor que 100 metros) - Chegada suave
        while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(pontoCentralMissao.x, pontoCentralMissao.z)) > 100f)
        {
            if (ordemParaRetorno) break; // Se mandou voltar no meio do caminho
            yield return null;
        }
        Debug.Log($"[{gameObject.name}] Chegou na zona de missão! Iniciando órbita/patrulha. Ficará aqui até receber ordem de retorno.");

        // Loop de órbita na área de missão (Vigilância/Patrulha/Ataque)
        while (!ordemParaRetorno)
        {
            // O avião move o alvo GPS sempre em curva para formar um círculo ao redor do ponto de missão
            alvoGPSVoo = transform.position + (transform.right * 150f) + (transform.forward * 100f);
            
            // Trava a altura para ele não espiralar para baixo e mergulhar em solo! (Causa do bug de perder altura)
            alvoGPSVoo.y = pontoCentralMissao.y; 

            // Se distanciar demais lateralmente, puxa de volta suavemente para o centro da ronda
            if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(pontoCentralMissao.x, pontoCentralMissao.z)) > 250f)
            {
                 // Puxa o alvo para o centro da missão de novo para não escapar
                 alvoGPSVoo = pontoCentralMissao;
            }

            yield return null;
        }

        ordemParaRetorno = false; // Limpa estado para a proxima missao

        // 3. RETORNO (Céu -> Ponto 'Freiada' do Aeroporto)
        estadoAtual = EstadoAviao.Pousando;
        Transform pontoFreiada = aeroportoOrigem.waypointsDecida[0]; // O waypoint lá no alto
        alvoGPSVoo = pontoFreiada.position;
        // Se a freiada inicial for muito baixa (erro do construtor), força entrar por cima antes de descer
        if (alvoGPSVoo.y < 40f) alvoGPSVoo.y = 40f; 
        
        Debug.Log($"[{gameObject.name}] Retornando para a base...");

        // Espera chegar perto do aeroporto
        while (Vector3.Distance(transform.position, alvoGPSVoo) > 15f)
        {
            // Quando chegarem em um raio de 200 metros do pouso as rodas aparecem novamente direto
            if (Vector3.Distance(transform.position, alvoGPSVoo) <= 200f)
            {
                AbaixarRodas();
            }

            yield return null;
        }

        // Força abaixar caso tenha passado muito rapido e evitado
        AbaixarRodas();

        // 4. POUSO (Desliga física e volta a seguir as linhas do chão)
        estaEmModoVooFisico = false;
        
        // Segue a lista obrigatória de pouso reduzindo velocidade para atersar e depois para taxiar
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsDecida, velocidadeMaximaVoo * 0.5f, velocidadeSolo, true));

        // 5. TAXIANDO PRA VAGA
        estadoAtual = EstadoAviao.RetornandoPraVaga;
        yield return StartCoroutine(MoverInterpolado(vagaRetorno.position, velocidadeSolo, true));

        // 6. PRONTO DE NOVO
        estadoAtual = EstadoAviao.ProntoNoPatio;
        Debug.Log($"[{gameObject.name}] Motor Desligado. Aeronave pronta para nova missão.");
        
        // Zera o modelo para alinhar certinho na vaga
        transform.rotation = vagaRetorno.rotation;
    }

    // ==========================================
    // MATEMÁTICA DO VOO REALISTA NO CÉU
    // ==========================================

    private void ManobraVooRealista()
    {
        Vector3 retaAteAlvo = alvoGPSVoo - transform.position;

        // O avião olha suavemente para a direção
        Quaternion olharMundoDesejado = Quaternion.LookRotation(retaAteAlvo);
        float anguloPressaoLateralY = Vector3.SignedAngle(transform.forward, retaAteAlvo, Vector3.up);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, olharMundoDesejado, taxaDeGiroLeme * Time.deltaTime);
        
        // Calculo da velocidade turbo
        Vector3 novaPos = transform.position + transform.forward * (velocidadeMaximaVoo * multiplicadorVelocidadeTurbo) * Time.deltaTime;

        // PROTEÇÃO DE ALTITUDE DE VOO: No modo de voo livre, é proibido voar abaixo de 15m.
        if (novaPos.y < 15f)
        {
            novaPos.y = 15f;
            // Levanta suavemente o nariz (gira o Transform para Y=0 plano) para não continuar caindo de bico
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, transform.eulerAngles.y, 0), 30f * Time.deltaTime);
        }
        
        transform.position = novaPos;

        // Banking Visual (Inclinar asas)
        if (modeloMecanicoVisual != null)
        {
            float inclinacaoAlvoZ = Mathf.Clamp(anguloPressaoLateralY * -1.8f, -asaBankingMaximo, asaBankingMaximo);
            float inclinacaoAlvoX = Mathf.Clamp(retaAteAlvo.y * -2.0f, -arfagemPitchMaxima, arfagemPitchMaxima);

            giroLateralRoll = Mathf.Lerp(giroLateralRoll, inclinacaoAlvoZ, Time.deltaTime * 3.5f);
            empinadaPitch = Mathf.Lerp(empinadaPitch, inclinacaoAlvoX, Time.deltaTime * (3.5f * 0.8f));

            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, 0f, giroLateralRoll);
        }
    }

    // ==========================================
    // SISTEMA DE TREM DE POUSO (RODAS)
    // ==========================================

    private IEnumerator RecolherRodas(float delay)
    {
        // Aguarda x segundos de voo para começar a animação
        yield return new WaitForSeconds(delay);
        
        if (rodasRecolhidas) yield break;
        rodasRecolhidas = true;

        float tempoTransicao = 1.0f;
        float tempoAtual = 0f;

        while (tempoAtual < tempoTransicao)
        {
            tempoAtual += Time.deltaTime;
            float t = tempoAtual / tempoTransicao;

            for (int i = 0; i < rodas.Count; i++)
            {
                if (rodas[i] != null)
                {
                    // Rotação faz um -50
                    Quaternion rotFinal = rotacoesOriginaisRodas[i] * Quaternion.Euler(-50f, 0f, 0f);
                    rodas[i].localRotation = Quaternion.Slerp(rotacoesOriginaisRodas[i], rotFinal, t);
                }
            }
            yield return null;
        }

        // Somem
        foreach (var roda in rodas)
        {
            if (roda != null) roda.gameObject.SetActive(false);
        }
    }

    private void AbaixarRodas()
    {
        if (!rodasRecolhidas) return;
        rodasRecolhidas = false;

        for (int i = 0; i < rodas.Count; i++)
        {
            if (rodas[i] != null)
            {
                rodas[i].gameObject.SetActive(true);
                rodas[i].localRotation = rotacoesOriginaisRodas[i]; // Aparecem novamente direto com a rotação original
            }
        }
    }
}
