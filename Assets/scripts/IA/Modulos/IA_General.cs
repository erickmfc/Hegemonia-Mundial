using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class IA_General : MonoBehaviour
{
    private IA_Comandante chefe;

    public enum EstadoGuerra { Reagrupando, AtaqueTotal, DefesaCritica }
    public EstadoGuerra estadoAtual = EstadoGuerra.Reagrupando;

    [Header("Estratégia de Ondas")]
    public int tamanhoMinimoOnda = 5; // Só ataca com 5 unidades
    public Transform pontoDeEncontro; // Rally Point antes do ataque
    public Transform alvoPrioritario; // Geralmente a base inimiga

    // Lista de unidades prontas para a guerra
    private List<GameObject> exercitoDisponivel = new List<GameObject>();
    private List<Fabrica> minhasFabricas = new List<Fabrica>(); // Fábricas da IA

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    public void RegistrarSoldado(GameObject unidade)
    {
        if (!exercitoDisponivel.Contains(unidade))
        {
            exercitoDisponivel.Add(unidade);
            // Manda para o ponto de encontro inicialmente
            if (pontoDeEncontro != null)
            {
                // MoverUnidade(unidade, pontoDeEncontro.position);
            }
        }
    }

    public void RegistrarFabrica(Fabrica fab)
    {
        if (fab != null && !minhasFabricas.Contains(fab))
        {
            minhasFabricas.Add(fab);
            Debug.Log($"[IA General] Fábrica registrada: {fab.name}");
        }
    }

    public void ProcessarEstrategia()
    {
        // 0. Manutenção (remover mortos)
        exercitoDisponivel.RemoveAll(u => u == null);

        // 1. Recrutamento (Se precisar de mais força)
        if (exercitoDisponivel.Count < tamanhoMinimoOnda + 2) // Mantém sempre um extra
        {
            TentarRecrutar();
        }

        // 2. Máquina de Estados de Guerra
        switch (estadoAtual)
        {
            case EstadoGuerra.Reagrupando:
                // Se juntou gente suficiente...
                if (exercitoDisponivel.Count >= tamanhoMinimoOnda)
                {
                    Debug.Log($"[IA General] Exército de {exercitoDisponivel.Count} unidades pronto! Iniciando Ataque!");
                    
                    // Define alvo (Base do jogador ou primeira unidade inimiga que achar)
                    alvoPrioritario = BuscarAlvoGlobal();
                    
                    estadoAtual = EstadoGuerra.AtaqueTotal;
                    LancarAtaque(alvoPrioritario);
                }
                else
                {
                    // Mantém as unidades perto da base defendendo
                    ManterPosicaoDefensiva();
                }
                break;

            case EstadoGuerra.AtaqueTotal:
                // Se o exército foi dizimado, volta a recuar
                if (exercitoDisponivel.Count < 2)
                {
                    Debug.Log("[IA General] Ataque falhou. Recuando para reagrupar.");
                    estadoAtual = EstadoGuerra.Reagrupando;
                }
                else
                {
                    // Continua pressionando o ataque (atualiza a posição do alvo se ele se moveu)
                    if(alvoPrioritario != null)
                    {
                        // Opcional: Reenviar comando de ataque a cada X segundos para unidades novas ou perdidas
                    }
                    else
                    {
                        // Alvo morreu? Busca outro
                        alvoPrioritario = BuscarAlvoGlobal();
                        if(alvoPrioritario != null) LancarAtaque(alvoPrioritario);
                    }
                }
                break;
        }
    }

    void TentarRecrutar()
    {
        // Só tenta a cada X frames
        if (Time.frameCount % 60 != 0) return;
        if (chefe.dinheiro < 500) return; // Segurança fnanceira

        var catalogo = MenuConstrucao.catalogoGlobal;
        if (catalogo == null) return;

        // 1. Filtra opções (SEM TANQUES POR ENQUANTO)
        var militares = catalogo.FindAll(i => 
            (i.categoria == DadosConstrucao.CategoriaItem.Exercito || 
             i.categoria == DadosConstrucao.CategoriaItem.Marinha || 
             i.categoria == DadosConstrucao.CategoriaItem.Aeronautica)
            && !i.nomeItem.ToLower().Contains("tank") // Bloqueia tanques
            && !i.nomeItem.ToLower().Contains("tanque")
        );

        if (militares.Count > 0)
        {
            var escolha = militares[Random.Range(0, militares.Count)];
            bool ehSoldado = (escolha.categoria == DadosConstrucao.CategoriaItem.Exercito);

            // 2. Procura uma fábrica adequada na lista da IA
            Fabrica fabricaDisponivel = null;
            
            foreach(var fab in minhasFabricas)
            {
                if(fab == null) continue;
                
                // Se quero soldado, preciso de Quartel. Se quero tanque/heli, preciso de Hangar.
                if (ehSoldado && fab.ehQuartel) fabricaDisponivel = fab;
                else if (!ehSoldado && !fab.ehQuartel) fabricaDisponivel = fab;

                if (fabricaDisponivel != null) break;
            }

            // 3. Produz
            if (fabricaDisponivel != null)
            {
                if (chefe.GastarDinheiro(escolha.preco))
                {
                    GameObject novaUnidade = fabricaDisponivel.ProduzirUnidade(escolha.prefabDaUnidade);
                    
                    if (novaUnidade != null)
                    {
                        RegistrarSoldado(novaUnidade);
                        Debug.Log($"[IA General] Recrutou {escolha.nomeItem} na fábrica {fabricaDisponivel.name}");
                    }
                }
            }
            else
            {
                // Se a IA tiver dinheiro mas não tiver fábrica, talvez devesse pedir pro Arquiteto construir uma...
                // Mas por enquanto só ignora.
            }
        }
    }

    Transform BuscarAlvoGlobal()
    {
        // Tenta achar a base do jogador (Tag "Player" ou ID 1)
        var inimigos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach(var ini in inimigos)
        {
            if (ini.teamID != 1) continue; // Só quer ID 1 (Jogador)
            return ini.transform;
        }
        return null; // Vitória da IA?
    }

    void ManterPosicaoDefensiva()
    {
        if (pontoDeEncontro == null) pontoDeEncontro = chefe.transform;
        
        foreach (var u in exercitoDisponivel)
        {
            if (u == null) continue;
            var nav = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
            
            // Se estiver muito longe do ponto de encontro, volta
            if (nav != null && Vector3.Distance(u.transform.position, pontoDeEncontro.position) > 30f)
            {
                nav.SetDestination(pontoDeEncontro.position);
            }
        }
    }

    void LancarAtaque(Transform alvo)
    {
        if (alvo == null) return;

        Debug.Log($"[IA General] ORDEM DE ATAQUE EM MASSA CONTRA {alvo.name}");

        foreach (var u in exercitoDisponivel)
        {
            if (u != null)
            {
                // Tenta usar o controle unificado (funciona para Helicópteros, Tanques e Soldados se implementado)
                var controle = u.GetComponent<ControleUnidade>();
                if (controle != null)
                {
                    // Se tiver método de ataque direto, usa. Se não, move para perto.
                    controle.MoverParaPonto(alvo.position); 
                }
                else
                {
                    // Fallback para unidades simples com NavMesh
                    var agente = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agente != null)
                    {
                        agente.SetDestination(alvo.position);
                        agente.isStopped = false;
                    }
                }

                // Se for Helicóptero, talvez precise de altura? O ControleUnidade já deve cuidar disso.
            }
        }
    }
}
