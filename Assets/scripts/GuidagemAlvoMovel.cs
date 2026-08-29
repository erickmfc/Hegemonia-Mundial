using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Calcula um ponto de guiagem antecipado sem alterar o alvo real. O projétil
/// usa este ponto apenas para orientar o nariz; o detonador continua usando a
/// posição atual do Transform alvo para não explodir em uma previsão vencida.
/// </summary>
public static class GuidagemAlvoMovel
{
    public static Vector3 ObterPontoDeMira(Transform alvo, Vector3 origem, float velocidade, float antecipacaoMaxima = 2.5f)
    {
        if (alvo == null) return origem;

        Vector3 posicao = alvo.position;
        Vector3 velocidadeAlvo = ObterVelocidade(alvo);
        if (velocidadeAlvo.sqrMagnitude < 0.01f) return posicao;

        float velocidadeMissil = Mathf.Max(1f, velocidade);
        float tempoEstimado = Vector3.Distance(origem, posicao) / velocidadeMissil;
        tempoEstimado = Mathf.Clamp(tempoEstimado, 0f, Mathf.Max(0f, antecipacaoMaxima));
        return posicao + velocidadeAlvo * tempoEstimado;
    }

    /// <summary>
    /// Verifica o trecho realmente percorrido pelo projétil entre dois
    /// frames. Em velocidades altas, apenas testar a distância no fim do
    /// frame permite que o míssil atravesse um alvo entre duas amostras.
    /// </summary>
    public static bool SegmentoAtingePonto(Vector3 inicio, Vector3 fim, Vector3 ponto, float tolerancia)
    {
        return TentarObterPontoMaisProximoNoSegmento(inicio, fim, ponto, out _, tolerancia);
    }

    /// <summary>
    /// Testa um segmento e devolve o ponto realmente percorrido mais próximo
    /// do alvo. Os mísseis usam esse ponto somente no instante confirmado de
    /// impacto, evitando que o efeito/dano fique atrás ou à frente do alvo
    /// quando a velocidade excede a distância entre duas amostras.
    /// </summary>
    public static bool TentarObterPontoMaisProximoNoSegmento(
        Vector3 inicio,
        Vector3 fim,
        Vector3 ponto,
        out Vector3 pontoImpacto,
        float tolerancia)
    {
        Vector3 segmento = fim - inicio;
        float comprimentoSqr = segmento.sqrMagnitude;
        if (comprimentoSqr < 0.0001f)
        {
            pontoImpacto = inicio;
            return Vector3.Distance(inicio, ponto) <= Mathf.Max(0f, tolerancia);
        }

        float t = Mathf.Clamp01(Vector3.Dot(ponto - inicio, segmento) / comprimentoSqr);
        pontoImpacto = inicio + segmento * t;
        return Vector3.Distance(pontoImpacto, ponto) <= Mathf.Max(0f, tolerancia);
    }

    private static Vector3 ObterVelocidade(Transform alvo)
    {
        Rigidbody corpo = alvo.GetComponent<Rigidbody>();
        if (corpo == null) corpo = alvo.GetComponentInParent<Rigidbody>();
        if (corpo == null) corpo = alvo.GetComponentInChildren<Rigidbody>(true);
        if (corpo != null && corpo.linearVelocity.sqrMagnitude > 0.01f)
            return corpo.linearVelocity;

        NavMeshAgent agente = alvo.GetComponent<NavMeshAgent>();
        if (agente == null) agente = alvo.GetComponentInParent<NavMeshAgent>();
        if (agente == null) agente = alvo.GetComponentInChildren<NavMeshAgent>(true);
        return agente != null ? agente.velocity : Vector3.zero;
    }
}
