using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

public class GerenteDeTerritorio : MonoBehaviour
{
    public static GerenteDeTerritorio Instancia;

    private List<MarcadorTerritorio> marcadores = new List<MarcadorTerritorio>();

    void Awake()
    {
        if (Instancia == null) 
        {
            Instancia = this;
            
            // Se o Gerente foi criado depois do jogo iniciar (pelo Construtor),
            // busca e registra todos os marcadores que já nasceram na fase.
            MarcadorTerritorio[] todosMarcadores = Object.FindObjectsByType<MarcadorTerritorio>(FindObjectsSortMode.None);
            foreach (var m in todosMarcadores)
            {
                RegistrarMarcador(m);
            }
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    public void RegistrarMarcador(MarcadorTerritorio marcador)
    {
        if (!marcadores.Contains(marcador)) marcadores.Add(marcador);
    }

    public void RemoverMarcador(MarcadorTerritorio marcador)
    {
        if (marcadores.Contains(marcador)) marcadores.Remove(marcador);
    }

    /// <summary>
    /// Retorna o TeamID de quem é dono do ponto. Retorna 0 se for neutro.
    /// Resolve distributivamente distâncias geométricas em formato de QUADRADO.
    /// </summary>
    public int ObterDonoDoPonto(Vector3 ponto)
    {
        int donoVencedor = 0;
        // Float para achar quem vence a sobreposição num conflito do formato quadrado.
        float menorDistanciaQuadrada = float.MaxValue; 
        
        // --- Corredor Nulo ---
        int donoSecundario = 0; // Se houver empate/sobreposição, guardamos o segundo dono 

        foreach (var m in marcadores)
        {
            if (m == null || !m.gameObject.activeInHierarchy) continue;

            // Transforma o cálculo redondo de Euler para Box/Quadrado Absoluto:
            // A distância "quadrada" para borda é a diferença máxima nos seus dois eixos X e Z
            float distX = Mathf.Abs(ponto.x - m.transform.position.x);
            float distZ = Mathf.Abs(ponto.z - m.transform.position.z);
            float distanciaQuadradaLocal = Mathf.Max(distX, distZ); // Corte em linha reta p/ divisas perfeitamente retas

            // Vê se a pessoa ta dentro da expansão da nossa base (Bandeira=100m, Prefeitura=300m)
            if (distanciaQuadradaLocal <= m.raioDeDominio)
            {
                if (distanciaQuadradaLocal < menorDistanciaQuadrada)
                {
                    // O vencedor anterior vira secundário (Corredor compartilhado se a diferença for pouca)
                    if (donoVencedor != 0 && donoVencedor != m.teamID)
                    {
                        // Se a diferença entre a distância para a fronteira A e fronteira B for menos de 5 metros, é Corredor Nulo!
                        // Mas aqui apenas guardamos. A matemática principal ocorre fora ou mantemos o mais forte.
                        donoSecundario = donoVencedor;
                    }

                    menorDistanciaQuadrada = distanciaQuadradaLocal;
                    donoVencedor = m.teamID;
                }
                else if (Mathf.Abs(distanciaQuadradaLocal - menorDistanciaQuadrada) < 5.0f && m.teamID != donoVencedor)
                {
                    // Margem de 5 metros onde é exatamene o meio da divisa: CORREDOR NULO MATEMÁTICO.
                    // Ambos têm jurisprudência quase igual.
                    donoSecundario = m.teamID;
                }
            }
        }

        return donoVencedor;
    }



    /// <summary>
    /// Regra: "Não se pode por na mesma faixa de terra duas prefeituras."
    /// Usamos NavMesh para testar se há conexão terrestre contínua entre o ponto desejado e as prefeituras existentes.
    /// </summary>
    public bool PodeConstruirPrefeitura(Vector3 ponto)
    {
        foreach (var m in marcadores)
        {
            // O loop verifica se existe OUTRO governo central registrado (Independente de quem)
            if (m != null && m.ehPrefeitura)
            {
                // Verifica distância bruta primeiro. Se estiver incrivelmente colado, bloqueia.
                if (Vector3.Distance(ponto, m.transform.position) < 50f) return false;

                // Tenta calcular um caminho de NavMesh entre a tentativa do mouse e a Prefeitura anterior...
                NavMeshPath caminho = new NavMeshPath();
                if (NavMesh.CalculatePath(ponto, m.transform.position, 1, caminho))
                {
                    if (caminho.status == NavMeshPathStatus.PathComplete)
                    {
                        // Estões conectados na mesma faixa de terra/ilha do mapa e tem passagem de andada pra eles
                        return false;
                    }
                }
            }
        }
        
        return true;
    }
}
