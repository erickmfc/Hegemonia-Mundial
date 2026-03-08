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
    public Vector3 centroDaPatrulha; 
    [HideInInspector] public bool emAtaqueMergulho = false;
    [HideInInspector] public Vector3 alvoDoMergulho;

    public bool estaEmModoVooFisico = false;
    private float giroLateralRoll = 0f; 
    private float empinadaPitch = 0f;   
    private float multiplicadorVelocidadeTurbo = 1f;
    private float tempoSegurandoTab = 0f;

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

            // TURBO: Segurar o TAB acelera de forma progressiva
            if (selecionado && Input.GetKey(KeyCode.Tab))
            {
                tempoSegurandoTab += Time.deltaTime;
                
                if (tempoSegurandoTab >= 11f)
                    multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 6f, Time.deltaTime);
                else if (tempoSegurandoTab >= 5f)
                    multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 4f, Time.deltaTime);
                else if (tempoSegurandoTab >= 2f)
                    multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 2f, Time.deltaTime);
                else
                    multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 1.5f, Time.deltaTime);
            }
            else
            {
                tempoSegurandoTab = 0f; // Reseta o cronômetro
                multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 1f, Time.deltaTime * 2f);
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
        float raioDeAceitacao = pontoFinal ? 0.5f : 1.5f;

        // Modificado para usar X e Z na distância de aceitação, mas atualizando a Altura (Y) fielmente (Crucial para Pousos!)
        while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(destinoOriginal.x, destinoOriginal.z)) > raioDeAceitacao)
        {
            Vector3 vetorAteDestino = destinoOriginal - transform.position;
            
            // PROTEÇÃO ANTI-360º
            if (vetorAteDestino.magnitude < 4f && Vector3.Dot(transform.forward, vetorAteDestino.normalized) < 0f)
            {
                break;
            }

            // Olha para o ponto (Apenas XZ para manobras horizontais)
            Vector3 direcaoHorizon = new Vector3(vetorAteDestino.x, 0, vetorAteDestino.z).normalized;
            
            if (direcaoHorizon != Vector3.zero)
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoHorizon);
                float angulo = Quaternion.Angle(transform.rotation, rotacaoAlvo);

                // Gira mais suave, como rodas virando
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, 50f * Time.deltaTime);

                // Se o avião está muito desalinhado na curva, reduz um pouco a velocidade para não sair do trajeto
                float fatorVelocidade = Mathf.Clamp01(1.2f - (angulo / 45f));
                if (fatorVelocidade < 0.2f) fatorVelocidade = 0.2f;

                // Movimento 3D Perfeito (Glide Slope): Move exatamente apontando pro alvo no ar
                Vector3 direcaoMovimento3D = vetorAteDestino.normalized;
                transform.position += direcaoMovimento3D * (vel * fatorVelocidade) * Time.deltaTime;
            }

            // Garante que as asas estão retas (Tira o banking visual)
            if (modeloMecanicoVisual != null)
            {
                modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.identity, Time.deltaTime * 5f);
            }

            yield return null;
        }

        // Se for o último ponto, crava a posição para não ter erro
        if (pontoFinal)
        {
            if (Vector3.Distance(transform.position, destinoOriginal) < 3.5f)
            {
                transform.position = destinoOriginal;
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
                
                // --- NOVO: ESPERAR NO ALINHAMENTO DA PISTA ANTES DE DECOLAR ---
                if (caminho[i].name.ToLower().Contains("alinhamento"))
                {
                    // Alinha o nariz fisicamente com o próximo waypoint (O fim da pista) antes de acelerar
                    if (i + 1 < caminho.Count && caminho[i+1] != null)
                    {
                        Vector3 direcaoPista = (caminho[i+1].position - transform.position).normalized;
                        direcaoPista.y = 0; // Rotacao apenas horizontal no chao
                        if (direcaoPista != Vector3.zero)
                        {
                            Quaternion rotDesejada = Quaternion.LookRotation(direcaoPista);
                            while (Quaternion.Angle(transform.rotation, rotDesejada) > 1f)
                            {
                                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotDesejada, 50f * Time.deltaTime);
                                yield return null;
                            }
                        }
                    }
                    // Aguardando autorização da torre por 2.5s
                    Debug.Log($"[{gameObject.name}] Aguardando autorização da torre na cabeceira da pista...");
                    yield return new WaitForSeconds(2.5f);
                }
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
        vagaRetorno = null; // --- NOVO: LIBERA A VAGA PARA QUE O HANGAR POSSA USAR OUTROS CAÇAS ---
        Debug.Log($"[{gameObject.name}] Iniciando taxiamento e decolagem!");
        
        // Segue os waypoints de decolagem do aeroporto rigorosamente (Aumentando do Zero até a Velocidade do Voo)
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsDecolagem, velocidadeSolo, velocidadeMaximaVoo, true));

        // 2. VOO FÍSICO (Céu)
        estadoAtual = EstadoAviao.EmMissao;
        estaEmModoVooFisico = true;
        
        // Garante que a missão ocorra no alto (Sempre acima de 60 metros)
        if (alvoGPSVoo.y < 60f) alvoGPSVoo.y = 60f;
        centroDaPatrulha = alvoGPSVoo;

        Debug.Log($"[{gameObject.name}] Saiu do chão. Voando para o Alvo!");

        // Inicia o processo de recolher o trem de pouso após 3 segundos
        StartCoroutine(RecolherRodas(3f));

        // Espera chegar no alvo (Distância horizontal menor que 100 metros) - Chegada suave
        while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(centroDaPatrulha.x, centroDaPatrulha.z)) > 100f)
        {
            if (ordemParaRetorno) break; // Se mandou voltar no meio do caminho
            
            // Permite mudar o centro de patrulha ANTES MESMO DE CHEGAR LÁ
            alvoGPSVoo = centroDaPatrulha; 
            
            yield return null;
        }
        Debug.Log($"[{gameObject.name}] Chegou na zona de missão! Iniciando órbita/patrulha. Ficará aqui até receber ordem de retorno.");

        // Loop de órbita na área de missão (Vigilância/Patrulha/Ataque)
        while (!ordemParaRetorno)
        {
            if (emAtaqueMergulho)
            {
                // MODO THUNDERBOLT (Mergulho de Ataque Reto!)
                alvoGPSVoo = alvoDoMergulho;
            }
            else
            {
                // O avião move o alvo GPS sempre em curva para formar um círculo ao redor do ponto de missão
                alvoGPSVoo = transform.position + (transform.right * 150f) + (transform.forward * 100f);
                
                // Trava a altura para ele não espiralar para baixo e mergulhar em solo!
                alvoGPSVoo.y = centroDaPatrulha.y; 

                // Se distanciar demais lateralmente, puxa de volta suavemente para o centro da ronda
                if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(centroDaPatrulha.x, centroDaPatrulha.z)) > 250f)
                {
                     // Puxa o alvo para o centro da missão de novo para não escapar
                     alvoGPSVoo = centroDaPatrulha;
                }
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

        // Espera chegar na bolha do aeroporto com uma margem maior para não criar o bug de ficar "girando" infinito
        while (true)
        {
            float distHorizontalPouso = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(alvoGPSVoo.x, alvoGPSVoo.z));
            
            // Quando chegarem em um raio de 400 metros do pouso as rodas aparecem
            if (distHorizontalPouso <= 400f)
            {
                AbaixarRodas();
            }

            // Uma margem grande (70m horizontal) garante que jatos a 150km/h acertem o gatilho sem orbitar
            if (distHorizontalPouso <= 70f)
            {
                break;
            }

            yield return null;
        }

        // Força abaixar caso tenha passado muito rapido e evitado
        AbaixarRodas();

        // 4. POUSO (Desliga física e volta a seguir as linhas do chão)
        estaEmModoVooFisico = false;
        
        // Segue a lista obrigatória de pouso reduzindo velocidade para atersar e depois para taxiar
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsDecida, velocidadeMaximaVoo * 0.5f, velocidadeSolo, true));

        // 5. REABASTECIMENTO AUTOMÁTICO (ANDADAR -> ANÁLISE)
        estadoAtual = EstadoAviao.RetornandoPraVaga;
        
        if (aeroportoOrigem.wpAndadar != null)
        {
            yield return StartCoroutine(MoverInterpolado(aeroportoOrigem.wpAndadar.position, velocidadeSolo, true));
        }

        if (aeroportoOrigem.wpAnalise != null)
        {
            yield return StartCoroutine(MoverInterpolado(aeroportoOrigem.wpAnalise.position, velocidadeSolo, true));
            
            // Pausa no ponto de Análise para rearmar os mísseis (3 segundos parados)
            Debug.Log($"[{gameObject.name}] Chegou no ponto de Análise. Parado por 3 segundos para reabastecer...");
            
            // Esse script avisa que chegou num local seguro para os sistemas de tiro recarregarem
            estadoAtual = EstadoAviao.ProntoNoPatio; 
            
            yield return new WaitForSeconds(3f);
            
            // Depois de 3s, confere se ele recebeu ordens de decolar pra patrulha
            // Se o LancadorMissel mandar ele patrulhar, o estado passa a ser Decolando/EmMissao imediatamente
            if (estadoAtual != EstadoAviao.ProntoNoPatio)
            {
                 // Ele decolou de novo direto do ponto de Análise!
                 yield break; 
            }
        }

        // 6. VAI PARA A VAGA PÁTIO/HANGAR SE NÃO SAIU EM MISSÃO
        estadoAtual = EstadoAviao.RetornandoPraVaga;
        
        Transform vagaSegura = aeroportoOrigem.ObterPrimeiraVagaLivre();
        if (vagaSegura != null)
        {
             // O Pátio tem vaga para estacionar!
             vagaRetorno = vagaSegura;
             yield return StartCoroutine(MoverInterpolado(vagaRetorno.position, velocidadeSolo, true));
             estadoAtual = EstadoAviao.ProntoNoPatio;
             Debug.Log($"[{gameObject.name}] Motor Desligado. Aeronave estacionou pronta para nova missão no pátio.");
             transform.rotation = vagaRetorno.rotation; // Alinhamento exato na vaga
        }
        else
        {
             // Pátio lotado! Vai taxiar reto para o wpPronto (portão do hangar) e ser consumido por ele
             yield return StartCoroutine(MoverInterpolado(aeroportoOrigem.wpPronto.position, velocidadeSolo, true));
             aeroportoOrigem.GuardarNoHangarAutomatico(this);
             Debug.Log($"[{gameObject.name}] Recapitulado direto pro Hangar: Pátio Físico estava lotado.");
        }
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
        
        // PROTEÇÃO INVISÍVEL MUNDIAL (BUG DO VAZIO): Evita que o caça vá muito longe sem querer e se perca no infinito
        if (Mathf.Abs(novaPos.x) > 1800f || Mathf.Abs(novaPos.z) > 1800f)
        {
             // Força rotação dura de volta para 0,0 do mapa sem perdoar curva! (180 Graus)
             Vector3 centroDoMap = new Vector3(0, novaPos.y, 0);
             alvoGPSVoo = centroDoMap;
             Quaternion freioDeOuro = Quaternion.LookRotation((centroDoMap - transform.position).normalized);
             transform.rotation = Quaternion.RotateTowards(transform.rotation, freioDeOuro, 100f * Time.deltaTime);
             novaPos = transform.position + transform.forward * velocidadeMaximaVoo * Time.deltaTime;
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

    // --- NOVA FUNÇÃO PARA ATAQUE DE MERGULHO ---
    public void ForcarAtaqueMergulho(Vector3 direcaoRetoAtaque)
    {
        if (emAtaqueMergulho) return; // Se já está no mergulho, deixa terminar a manobra completa!
        StartCoroutine(RotinaMergulho(direcaoRetoAtaque));
    }

    private IEnumerator RotinaMergulho(Vector3 pontoFinal)
    {
        emAtaqueMergulho = true;
        float velOriginal = velocidadeMaximaVoo;
        
        // 1. SOBE PARA O ARTO (80m) E PERDE 50% DA VELOCIDADE PARA MIRAR BEM 
        Vector3 pontoAlto = transform.position + transform.forward * 80f;
        pontoAlto.y = 80f;
        alvoDoMergulho = pontoAlto;
        velocidadeMaximaVoo = velOriginal * 0.5f;

        yield return new WaitForSeconds(1.2f); // Espera um pouco alevantando o nariz em baixa velocidade

        // 2. DESCE RASGANDO (20m) DESPEJANDO FOGO! (Volta a acelerar)
        alvoDoMergulho = pontoFinal; // O pontoFinal que o armamento enviou já tem y=20
        velocidadeMaximaVoo = velOriginal * 0.8f; // Desce a 80% do máximo para ter mais tempo de atirar!

        // Em linha reta até o fim do ataque rasante!
        yield return new WaitForSeconds(3.5f);

        // 3. FIM DO MERGULHO. Volta a patrulhar normal o ponto central e reacelera infinito.
        velocidadeMaximaVoo = velOriginal;
        emAtaqueMergulho = false;
    }
}
