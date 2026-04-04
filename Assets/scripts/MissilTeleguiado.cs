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

    void OnEnable()
    {
        IA_CombatTelemetry.RegisterMissile();
        alvo = null;
        tempoExpirar = Time.time + tempoDeVida;
    }

    void OnDisable()
    {
        IA_CombatTelemetry.UnregisterMissile();
        alvo = null;
    }

    public void DefinirAlvo(Transform novoAlvo)
    {
        alvo = ControleSubmarino.PodeSerAlvoConvencional(novoAlvo) ? novoAlvo : null;
        tempoExpirar = Time.time + tempoDeVida;
    }

    void Update()
    {
        if (Time.time >= tempoExpirar)
        {
            Liberar();
            return;
        }

        if (alvo == null || !ControleSubmarino.PodeSerAlvoConvencional(alvo))
        {
            alvo = null;
            BuscarNovoAlvo();

            if (alvo == null)
            {
                Liberar();
                return;
            }
        }

        Vector3 direcao = alvo.position - transform.position;
        float distancia = direcao.magnitude;
        float distanciaFrame = velocidade * Time.deltaTime;

        if (distancia <= distanciaDeImpacto)
        {
            AplicarDano();
            Liberar();
            return;
        }

        transform.Translate(direcao.normalized * distanciaFrame, Space.World);
        transform.LookAt(alvo);
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

        SistemaDeDanos sistemaDanos = alvo.GetComponent<SistemaDeDanos>();
        if (sistemaDanos != null)
        {
            sistemaDanos.ReceberDano(dano);
            return;
        }

        AtributosPredio predioAtrib = alvo.GetComponent<AtributosPredio>();
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
