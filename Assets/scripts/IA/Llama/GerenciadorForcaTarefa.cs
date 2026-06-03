using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GerenciadorForcaTarefa : MonoBehaviour
{
    private IA_Comandante chefe;
    
    public void IniciarInvasao(int alvoTeamId)
    {
        chefe = GetComponent<IA_Comandante>();
        if (chefe == null) return;
        
        Transform alvo = null;
        var bases = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach(var b in bases)
        {
            if (b.teamID == alvoTeamId && b.tipoUnidade == TipoUnidade.Estrutura)
            {
                alvo = b.transform;
                break;
            }
        }
        
        if (alvo == null)
        {
            // Fallback: busca HQ ou qualquer unidade
            foreach(var u in bases)
            {
                if (u.teamID == alvoTeamId)
                {
                    alvo = u.transform;
                    break;
                }
            }
        }

        if (alvo != null)
        {
            StartCoroutine(RotinaForcaTarefa(alvo));
        }
        else
        {
            Debug.Log("[Força-Tarefa] Alvo não encontrado. Abortando invasão anfíbia.");
        }
    }
    
    IEnumerator RotinaForcaTarefa(Transform alvoFinal)
    {
        Debug.Log("[Força-Tarefa] Fase 1: Recrutamento e Logística.");
        
        List<GameObject> forcaTerrestre = new List<GameObject>();
        List<GameObject> transportes = new List<GameObject>();
        List<GameObject> escoltaNaval = new List<GameObject>();
        
        // Separa as tropas para a operação (Pega até 10 soldados/tanques)
        int terrestreMax = 10;
        foreach (var u in chefe.minhasUnidades.ToList()) // Copia a lista para evitar erro
        {
            if (u == null) continue;
            
            // Verifica tipo de unidade
            if (u.name.Contains("Transporte") || u.name.Contains("Hovercraft") || u.GetComponent<Helicoptero>() != null)
            {
                transportes.Add(u);
            }
            else if (u.GetComponent<ControleSubmarino>() != null || u.name.Contains("Navio") || u.name.Contains("Corveta") || u.name.Contains("Destroyer"))
            {
                escoltaNaval.Add(u);
            }
            else if (forcaTerrestre.Count < terrestreMax && (u.GetComponent<NavMeshAgent>() != null))
            {
                forcaTerrestre.Add(u);
            }
        }
        
        if (forcaTerrestre.Count == 0 || (transportes.Count == 0 && escoltaNaval.Count == 0))
        {
            Debug.Log("[Força-Tarefa] Falta de veículos logísticos ou tropas. Abortando, delegando para ataque terrestre comum.");
            chefe.estadoAtual = IA_Comandante.EstadoEstrategico.Ataque_Total;
            Destroy(this);
            yield break;
        }
        
        // Fase 2: Ponto de Encontro Logístico
        Debug.Log("[Força-Tarefa] Fase 2: Preparando Embarque simulado.");
        Transform veiculoMestre = transportes.Count > 0 ? transportes[0].transform : escoltaNaval[0].transform;
        
        // Manda infantaria andar até o veículo
        foreach (var tropa in forcaTerrestre)
        {
            if (tropa != null)
            {
                ControleUnidade ctrl = tropa.GetComponent<ControleUnidade>();
                if (ctrl) ctrl.EmitirOrdemMover(veiculoMestre.position);
            }
        }
        
        // Espera a tropa chegar perto ou timeout
        float timeout = 25f;
        while (timeout > 0)
        {
            bool todosEmbarcados = true;
            foreach (var tropa in forcaTerrestre)
            {
                if (tropa != null && tropa.activeInHierarchy)
                {
                    if (Vector3.Distance(tropa.transform.position, veiculoMestre.position) < 15f)
                    {
                        // Embarca (Some do mapa)
                        tropa.SetActive(false);
                    }
                    else
                    {
                        todosEmbarcados = false;
                    }
                }
            }
            if (todosEmbarcados) break;
            yield return new WaitForSeconds(1f);
            timeout -= 1f;
        }
        
        // Força embarque de quem atrasou
        foreach (var tropa in forcaTerrestre)
        {
            if (tropa != null && tropa.activeInHierarchy)
            {
                tropa.SetActive(false);
                tropa.transform.position = veiculoMestre.position; // Teleporta para dentro do veículo
            }
        }
        
        // Fase 3: Viagem Conjunta
        Debug.Log("[Força-Tarefa] Fase 3: Movimentando frota em direção à praia inimiga.");
        
        // Acha um ponto costeiro perto do alvo (ou vai direto)
        Vector3 destinoPraia = alvoFinal.position;
        
        // Tenta achar um ponto na água/terra mais afastado usando Raycast se necessário (simplificado)
        destinoPraia += (veiculoMestre.position - alvoFinal.position).normalized * 40f; 
        
        foreach (var transp in transportes)
        {
            if (transp != null)
            {
                ControleUnidade ctrl = transp.GetComponent<ControleUnidade>();
                if (ctrl) ctrl.EmitirOrdemMover(destinoPraia);
            }
        }
        foreach (var escolta in escoltaNaval)
        {
            if (escolta != null)
            {
                // Escolta vai um pouco na frente
                ControleUnidade ctrl = escolta.GetComponent<ControleUnidade>();
                if (ctrl) ctrl.EmitirOrdemMover(alvoFinal.position);
            }
        }
        
        // Espera transporte chegar
        float tempoViagem = 120f;
        while (tempoViagem > 0)
        {
            if (veiculoMestre == null) break; // Veiculo explodiu!
            if (Vector3.Distance(veiculoMestre.position, destinoPraia) < 25f)
            {
                break; // Chegou
            }
            yield return new WaitForSeconds(2f);
            tempoViagem -= 2f;
        }
        
        // Fase 4: Desembarque e Choque
        Debug.Log("[Força-Tarefa] Fase 4: Desembarque e Doutrina de Choque Ativados!");
        
        // Descarrega
        foreach (var tropa in forcaTerrestre)
        {
            if (tropa != null)
            {
                // Verifica se o veículo mestre morreu na viagem
                if (veiculoMestre == null)
                {
                    Destroy(tropa); // Tropa afunda com o navio
                    continue;
                }
                
                tropa.transform.position = veiculoMestre.position + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
                tropa.SetActive(true);
                
                // Manda atacar o alvo
                ControleUnidade ctrl = tropa.GetComponent<ControleUnidade>();
                if (ctrl) ctrl.EmitirOrdemMover(alvoFinal.position);
            }
        }
        
        // Transportes recuam
        foreach (var transp in transportes)
        {
            if (transp != null)
            {
                ControleUnidade ctrl = transp.GetComponent<ControleUnidade>();
                if (ctrl) ctrl.EmitirOrdemMover(chefe.pontoDeSpawnPadrao().position);
            }
        }
        
        // Suicídio deste script, trabalho finalizado.
        Destroy(this);
    }
}
