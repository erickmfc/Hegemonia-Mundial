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
    public float velocidadeMaximaVoo = 180f; 
    public float taxaDeGiroLeme = 120f;    

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
    [HideInInspector] public bool alvoPrioritarioIA = false; 

    public bool estaEmModoVooFisico = false;
    private float giroLateralRoll = 0f; 
    private float empinadaPitch = 0f;   
    private float multiplicadorVelocidadeTurbo = 1f;
    private float tempoSegurandoTab = 0f;

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true; 

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
        }

        foreach (var roda in rodas)
        {
            if (roda != null) rotacoesOriginaisRodas.Add(roda.localRotation);
            else rotacoesOriginaisRodas.Add(Quaternion.identity);
        }
        AbaixarRodas(); 
    }

    void Update()
    {
        if (estaEmModoVooFisico)
        {
            ControleUnidade cu = GetComponent<ControleUnidade>();
            bool selecionado = (cu != null && cu.selecionado);

            float multiplicadorDanos = 1f;
            SistemaDeDanos danos = GetComponent<SistemaDeDanos>();
            if (danos != null && danos.vidaMaxima > 0)
            {
                float pctVida = danos.vidaAtual / danos.vidaMaxima;
                if (pctVida < 0.25f)
                {
                    multiplicadorDanos = 0.5f;
                    if (estadoAtual == EstadoAviao.EmMissao && !ordemParaRetorno)
                    {
                        Debug.Log($"<color=red>[{gameObject.name}] DANOS CRÍTICOS ({Mathf.RoundToInt(pctVida*100)}%)! Retornando base.</color>");
                        ComandoRetornarBase();
                    }
                }
            }

            if (selecionado && Input.GetKey(KeyCode.Tab))
            {
                tempoSegurandoTab += Time.deltaTime;
                if (tempoSegurandoTab >= 11f) multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 6f, Time.deltaTime);
                else if (tempoSegurandoTab >= 5f) multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 4f, Time.deltaTime);
                else if (tempoSegurandoTab >= 2f) multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 2f, Time.deltaTime);
                else multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 1.5f, Time.deltaTime);
            }
            else
            {
                tempoSegurandoTab = 0f; 
                multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 1f, Time.deltaTime * 2f);
            }

            ManobraVooRealista(multiplicadorDanos);
        }
    }

    private void ManobraVooRealista(float multDano = 1f)
    {
        Vector3 retaAteAlvo = alvoGPSVoo - transform.position;
        Quaternion olharMundoDesejado = Quaternion.LookRotation(retaAteAlvo);
        float anguloPressaoLateralY = Vector3.SignedAngle(transform.forward, retaAteAlvo, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, olharMundoDesejado, taxaDeGiroLeme * Time.deltaTime);
        
        float velFinal = (velocidadeMaximaVoo * multiplicadorVelocidadeTurbo) * multDano;
        Vector3 novaPos = transform.position + transform.forward * velFinal * Time.deltaTime;

        if (novaPos.y < 15f)
        {
            novaPos.y = 15f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, transform.eulerAngles.y, 0), 30f * Time.deltaTime);
        }
        
        if (Mathf.Abs(novaPos.x) > 1800f || Mathf.Abs(novaPos.z) > 1800f)
        {
             Vector3 centroDoMap = new Vector3(0, novaPos.y, 0);
             alvoGPSVoo = centroDoMap;
             Quaternion freioDeOuro = Quaternion.LookRotation((centroDoMap - transform.position).normalized);
             transform.rotation = Quaternion.RotateTowards(transform.rotation, freioDeOuro, 100f * Time.deltaTime);
             novaPos = transform.position + transform.forward * velocidadeMaximaVoo * Time.deltaTime;
        }

        transform.position = novaPos;

        if (modeloMecanicoVisual != null)
        {
            float inclinacaoAlvoZ = Mathf.Clamp(anguloPressaoLateralY * -2.5f, -asaBankingMaximo, asaBankingMaximo);
            float inclinacaoAlvoX = Mathf.Clamp(retaAteAlvo.y * -3.0f, -arfagemPitchMaxima, arfagemPitchMaxima);
            giroLateralRoll = Mathf.Lerp(giroLateralRoll, inclinacaoAlvoZ, Time.deltaTime * 5f);
            empinadaPitch = Mathf.Lerp(empinadaPitch, inclinacaoAlvoX, Time.deltaTime * 5f);
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, 0f, giroLateralRoll);
        }
    }

    public IEnumerator MoverInterpolado(Vector3 destinoOriginal, float vel, bool pontoFinal = false)
    {
        float raioDeAceitacao = pontoFinal ? 0.5f : 1.5f;
        while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(destinoOriginal.x, destinoOriginal.z)) > raioDeAceitacao)
        {
            Vector3 vetorAteDestino = destinoOriginal - transform.position;
            if (vetorAteDestino.magnitude < 4f && Vector3.Dot(transform.forward, vetorAteDestino.normalized) < 0f) break;
            Vector3 direcaoHorizon = new Vector3(vetorAteDestino.x, 0, vetorAteDestino.z).normalized;
            if (direcaoHorizon != Vector3.zero)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direcaoHorizon), 50f * Time.deltaTime);
                float fatorVelocidade = Mathf.Clamp01(1.2f - (Quaternion.Angle(transform.rotation, Quaternion.LookRotation(direcaoHorizon)) / 45f));
                if (fatorVelocidade < 0.2f) fatorVelocidade = 0.2f;
                transform.position += vetorAteDestino.normalized * (vel * fatorVelocidade) * Time.deltaTime;
            }
            if (modeloMecanicoVisual != null) modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.identity, Time.deltaTime * 5f);
            yield return null;
        }
        if (pontoFinal && Vector3.Distance(transform.position, destinoOriginal) < 3.5f) transform.position = destinoOriginal;
    }

    public IEnumerator SeguirCaminhoDeWaypoints(List<Transform> caminho, float velInicial, float velFinal, bool aceleracaoGradativa = false)
    {
        for (int i = 0; i < caminho.Count; i++)
        {
            if (caminho[i] == null) continue;
            float velAtual = aceleracaoGradativa && caminho.Count > 1 ? Mathf.Lerp(velInicial, velFinal, (float)i / (caminho.Count - 1)) : velInicial;
            yield return StartCoroutine(MoverInterpolado(caminho[i].position, velAtual, i == caminho.Count - 1));
            if (caminho[i].name.ToLower().Contains("alinhamento")) yield return new WaitForSeconds(2.5f);
        }
    }

    public void IniciarMissaoCompleta(Vector3 alvoFinalGPS)
    {
        if (estadoAtual == EstadoAviao.ProntoNoPatio)
        {
            alvoGPSVoo = alvoFinalGPS;
            StartCoroutine(SequenciaDeVooEPouso());
        }
    }

    public void ComandoRetornarBase()
    {
        if (estadoAtual == EstadoAviao.EmMissao) ordemParaRetorno = true;
    }

    private IEnumerator SequenciaDeVooEPouso()
    {
        ordemParaRetorno = false;
        estadoAtual = EstadoAviao.Decolando;
        vagaRetorno = null; 
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsDecolagem, velocidadeSolo, velocidadeMaximaVoo, true));

        estaEmModoVooFisico = true;
        estadoAtual = EstadoAviao.EmMissao;
        if (alvoGPSVoo.y < 60f) alvoGPSVoo.y = 60f;
        centroDaPatrulha = alvoGPSVoo;
        StartCoroutine(RecolherRodas(3f));

        while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(centroDaPatrulha.x, centroDaPatrulha.z)) > 100f)
        {
            if (ordemParaRetorno) break;
            alvoGPSVoo = centroDaPatrulha; 
            yield return null;
        }

        while (!ordemParaRetorno)
        {
            if (emAtaqueMergulho) alvoGPSVoo = alvoDoMergulho;
            else if (!alvoPrioritarioIA)
            {
                alvoGPSVoo = transform.position + (transform.right * 150f) + (transform.forward * 100f);
                alvoGPSVoo.y = centroDaPatrulha.y; 
                if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(centroDaPatrulha.x, centroDaPatrulha.z)) > 250f) alvoGPSVoo = centroDaPatrulha;
            }
            yield return null;
        }

        ordemParaRetorno = false;
        estadoAtual = EstadoAviao.Pousando;
        Vector3 pontoFreiada = aeroportoOrigem.waypointsDecida[0].position;
        alvoGPSVoo = pontoFreiada;
        if (alvoGPSVoo.y < 40f) alvoGPSVoo.y = 40f; 
        
        while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(alvoGPSVoo.x, alvoGPSVoo.z)) > 70f)
        {
            if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(alvoGPSVoo.x, alvoGPSVoo.z)) < 400f) AbaixarRodas();
            yield return null;
        }

        AbaixarRodas();
        estaEmModoVooFisico = false;
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsDecida, velocidadeMaximaVoo * 0.5f, velocidadeSolo, true));

        estadoAtual = EstadoAviao.RetornandoPraVaga;
        if (aeroportoOrigem.wpAndadar != null) yield return StartCoroutine(MoverInterpolado(aeroportoOrigem.wpAndadar.position, velocidadeSolo, true));
        if (aeroportoOrigem.wpAnalise != null)
        {
            yield return StartCoroutine(MoverInterpolado(aeroportoOrigem.wpAnalise.position, velocidadeSolo, true));
            
            // REPARO AO CHEGAR: Restaura 100% da vida no ponto de análise
            SistemaDeDanos danos = GetComponent<SistemaDeDanos>();
            if (danos != null) danos.Reparar(danos.vidaMaxima);
            
            estadoAtual = EstadoAviao.ProntoNoPatio; 
            yield return new WaitForSeconds(3f);
            if (estadoAtual != EstadoAviao.ProntoNoPatio) yield break; 
        }

        Transform vagaSegura = aeroportoOrigem.ObterPrimeiraVagaLivre();
        if (vagaSegura != null)
        {
             vagaRetorno = vagaSegura;
             yield return StartCoroutine(MoverInterpolado(vagaRetorno.position, velocidadeSolo, true));
             estadoAtual = EstadoAviao.ProntoNoPatio;
             transform.rotation = vagaRetorno.rotation; 
        }
        else
        {
             yield return StartCoroutine(MoverInterpolado(aeroportoOrigem.wpPronto.position, velocidadeSolo, true));
             aeroportoOrigem.GuardarNoHangarAutomatico(this);
        }
    }

    private IEnumerator RecolherRodas(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (rodasRecolhidas) yield break;
        rodasRecolhidas = true;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime;
            for (int i = 0; i < rodas.Count; i++)
            {
                if (rodas[i] != null) rodas[i].localRotation = Quaternion.Slerp(rotacoesOriginaisRodas[i], rotacoesOriginaisRodas[i] * Quaternion.Euler(-50f, 0f, 0f), t);
            }
            yield return null;
        }
        foreach (var roda in rodas) if (roda != null) roda.gameObject.SetActive(false);
    }

    private void AbaixarRodas()
    {
        if (!rodasRecolhidas) return;
        rodasRecolhidas = false;
        for (int i = 0; i < rodas.Count; i++)
        {
            if (rodas[i] != null) { rodas[i].gameObject.SetActive(true); rodas[i].localRotation = rotacoesOriginaisRodas[i]; }
        }
    }

    public void ForcarAtaqueMergulho(Vector3 direcaoRetoAtaque)
    {
        if (!emAtaqueMergulho) StartCoroutine(RotinaMergulho(direcaoRetoAtaque));
    }

    private IEnumerator RotinaMergulho(Vector3 pontoFinal)
    {
        emAtaqueMergulho = true;
        float velOriginal = velocidadeMaximaVoo;
        alvoDoMergulho = transform.position + transform.forward * 120f; alvoDoMergulho.y = 150f;
        velocidadeMaximaVoo = velOriginal * 0.4f;
        yield return new WaitForSeconds(2.0f);
        alvoDoMergulho = pontoFinal;
        velocidadeMaximaVoo = velOriginal * 0.8f;
        yield return new WaitForSeconds(3.5f);
        velocidadeMaximaVoo = velOriginal;
        emAtaqueMergulho = false;
    }
}
