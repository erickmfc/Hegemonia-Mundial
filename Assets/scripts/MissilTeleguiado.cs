using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

public class MissilTeleguiado : MonoBehaviour
{
    private static readonly List<ControleAviao> bufferAvioes = new List<ControleAviao>(64);

    private Transform alvo;
    public float velocidade = 80f;
    public int dano = 20;
    public float distanciaDeImpacto = 2f;
    public float tempoDeVida = 15f;
    private float tempoExpirar;
    private Vector3 ultimaPosicaoGuiagem;
    private bool possuiUltimaPosicaoGuiagem;
    private Vector3 pontoAlvoFixo;
    private bool alvoFixoPorCoordenada;

    void OnEnable()
    {
        IA_CombatTelemetry.RegisterMissile();
        alvo = null;
        pontoAlvoFixo = transform.position;
        alvoFixoPorCoordenada = false;
        tempoExpirar = Time.time + tempoDeVida;
        ultimaPosicaoGuiagem = transform.position;
        possuiUltimaPosicaoGuiagem = true;
    }

    void OnDisable()
    {
        IA_CombatTelemetry.UnregisterMissile();
        alvo = null;
    }

    public void DefinirAlvo(Transform novoAlvo)
    {
        alvoFixoPorCoordenada = false;
        alvo = ControleSubmarino.PodeSerAlvoConvencional(novoAlvo) ? novoAlvo : null;
        AtualizarPrazoDeVoo(novoAlvo != null ? novoAlvo.position : transform.position);
        ultimaPosicaoGuiagem = transform.position;
        possuiUltimaPosicaoGuiagem = true;
    }

    /// <summary>
    /// Define um ponto manual sem deixar o míssil procurar automaticamente
    /// outra aeronave. Esse caminho é usado por lançamentos coordenados por
    /// posição quando o prefab legado ainda contém apenas este componente.
    /// </summary>
    public void DefinirAlvo(Vector3 ponto)
    {
        alvo = null;
        pontoAlvoFixo = ponto;
        alvoFixoPorCoordenada = true;
        AtualizarPrazoDeVoo(ponto);
        ultimaPosicaoGuiagem = transform.position;
        possuiUltimaPosicaoGuiagem = true;
    }

    // O prazo legado de 15 s era suficiente apenas para alvos próximos. Em
    // lançamentos por coordenada o mesmo míssil era liberado no meio do mar
    // antes de alcançar o destino. O prazo agora respeita a distância real,
    // sem alterar velocidade, dano ou guiagem.
    private void AtualizarPrazoDeVoo(Vector3 destino)
    {
        float distancia = Vector3.Distance(transform.position, destino);
        float segundosNecessarios = distancia / Mathf.Max(velocidade, 1f) + 6f;
        tempoExpirar = Time.time + Mathf.Max(tempoDeVida, segundosNecessarios);
    }

    void Update()
    {
        if (Time.time >= tempoExpirar)
        {
            Liberar();
            return;
        }

        if (!alvoFixoPorCoordenada && (alvo == null || !ControleSubmarino.PodeSerAlvoConvencional(alvo)))
        {
            alvo = null;
            BuscarNovoAlvo();

            if (alvo == null)
            {
                Liberar();
                return;
            }
        }

        Vector3 posicaoAnterior = transform.position;
        Vector3 pontoAtual = alvoFixoPorCoordenada ? pontoAlvoFixo : alvo.position;
        Vector3 pontoDeMira = alvoFixoPorCoordenada
            ? pontoAlvoFixo
            : GuidagemAlvoMovel.ObterPontoDeMira(
                alvo,
                transform.position,
                Mathf.Max(velocidade, 1f),
                1.5f);
        Vector3 direcao = pontoDeMira - transform.position;
        float distancia = Vector3.Distance(pontoAtual, transform.position);
        float distanciaFrame = velocidade * Time.deltaTime;
        Vector3 direcaoNormalizada = direcao.sqrMagnitude > 0.0001f
            ? direcao.normalized
            : transform.forward;
        Vector3 posicaoSeguinte = posicaoAnterior + direcaoNormalizada * distanciaFrame;
        bool cruzouAlvo = GuidagemAlvoMovel.SegmentoAtingePonto(
            possuiUltimaPosicaoGuiagem ? ultimaPosicaoGuiagem : posicaoAnterior,
            posicaoSeguinte,
            pontoAtual,
            Mathf.Max(distanciaDeImpacto, distanciaFrame * 1.1f));

        if (distancia <= distanciaDeImpacto || cruzouAlvo)
        {
            AplicarDano();
            Liberar();
            return;
        }

        transform.position = posicaoSeguinte;
        if (direcao.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direcaoNormalizada, Vector3.up);
        }
        ultimaPosicaoGuiagem = transform.position;
        possuiUltimaPosicaoGuiagem = true;
    }

    void BuscarNovoAlvo()
    {
        bufferAvioes.Clear();
        RegistroEntidadesJogo.FillAvioes(bufferAvioes);

        float menorDistancia = 100f;
        Transform novoAlvo = null;

        for (int i = 0; i < bufferAvioes.Count; i++)
        {
            ControleAviao aviao = bufferAvioes[i];
            if (aviao == null || !aviao.gameObject.activeInHierarchy)
            {
                continue;
            }

            Transform candidato = aviao.transform;
            if (!ControleSubmarino.PodeSerAlvoConvencional(candidato))
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, candidato.position);
            if (dist < menorDistancia)
            {
                menorDistancia = dist;
                novoAlvo = candidato;
            }
        }

        alvo = novoAlvo;
    }

    void AplicarDano()
    {
        if (alvo == null) return;

        SistemaDeDanos sistemaDanos = alvo.GetComponent<SistemaDeDanos>()
            ?? alvo.GetComponentInParent<SistemaDeDanos>()
            ?? alvo.GetComponentInChildren<SistemaDeDanos>(true);
        if (sistemaDanos != null)
        {
            sistemaDanos.ReceberDano(dano);
            return;
        }

        AtributosPredio predioAtrib = alvo.GetComponent<AtributosPredio>()
            ?? alvo.GetComponentInParent<AtributosPredio>()
            ?? alvo.GetComponentInChildren<AtributosPredio>(true);
        if (predioAtrib != null)
        {
            predioAtrib.vidaAtual -= dano;
            if (predioAtrib.vidaAtual <= 0)
            {
                Destroy(alvo.gameObject);
            }
            return;
        }

        if (alvo.CompareTag("Aereo") || alvo.CompareTag("Inimigo"))
        {
            Destroy(alvo.gameObject);
        }
    }

    private void Liberar()
    {
        PoolDeObjetosCombate.Release(gameObject);
    }
}
