using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_TaskForceCoordinator : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private float _nextDecisionTime;
        private int _invasionTargetTeamId = -1;
        private bool _invasionActive = false;

        public IA_TaskForceCoordinator(IA_Context context)
        {
            _context = context;
        }

        public string Name => "IA_TaskForceCoordinator";
        public float Interval => 1.5f;
        public float BudgetMs => 1.0f;

        public void SetInvasionTarget(int teamId)
        {
            _invasionTargetTeamId = teamId;
            _invasionActive = true;
            Debug.Log($"[IA_TaskForceCoordinator] Alvo da Força-Tarefa definido para Nação {teamId}");
        }

        public void Tick(float now, float deltaTime)
        {
            if (now < _nextDecisionTime || !_invasionActive || _context.Brain == null)
            {
                return;
            }
            
            _nextDecisionTime = now + Interval;

            if (_context.Brain.ActiveImperialPlan != "invasao_anfibia_combinada")
            {
                _invasionActive = false;
                return;
            }

            // O Coordenador lê os Esquadrões de Diferentes "Domínios" (Ar e Mar)
            IA_SquadData transportSquad = _context.SquadDirector.GetSquad(IA_SquadRole.AirTacticalTransport);
            IA_SquadData escortSquad = _context.SquadDirector.GetSquad(IA_SquadRole.NavalEscort);

            // Verifica se as forças mínimas foram atingidas
            if (transportSquad == null || transportSquad.Units.Count < 2) return;
            if (escortSquad == null || escortSquad.Units.Count < 3) return;

            // Resolve o alvo
            Vector3 targetPosition = Vector3.zero;
            bool targetFound = false;
            
            if (_invasionTargetTeamId > 0 && _context.WorldState != null)
            {
                // Tenta achar a prefeitura do alvo
                if (_context.WorldState.TryGetEnemyStrategicAnchor(_context.WorldState.BaseCenter, out targetPosition))
                {
                    targetFound = true;
                }
            }

            if (!targetFound)
            {
                return; // Aguarda encontrar o alvo no radar/world state
            }

            Vector3 GetSquadCenter(IA_SquadData squad)
            {
                if (squad == null || squad.Units.Count == 0) return Vector3.zero;
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < squad.Units.Count; i++)
                {
                    if (squad.Units[i] != null)
                    {
                        sum += squad.Units[i].transform.position;
                        count++;
                    }
                }
                return count > 0 ? sum / count : Vector3.zero;
            }

            Vector3 transportCenter = GetSquadCenter(transportSquad);
            Vector3 escortCenter = GetSquadCenter(escortSquad);

            // Cria um Ponto de Encontro no Mar (Assembly)
            Vector3 assemblyCenter = _context.WorldState.BaseCenter + (_context.WorldState.BaseCenter - targetPosition).normalized * -500f;

            // Emite ordens combinadas! 
            // 1. Escolta Naval avança até o Assembly Center
            _context.Brain.TryIssueMovePackage(_context.Brain.TeamId, "taskforce_escort_assembly", assemblyCenter, 100);
            
            // 2. Transportes Aéreos (Helicópteros com tropas) seguem a escolta
            _context.Brain.TryIssueMovePackage(_context.Brain.TeamId, "taskforce_air_assembly", assemblyCenter, 100);

            // Quando o Transporte chegar no Assembly Center (mar aberto), monta a Força-Tarefa e parte pra invasão!
            if (Vector3.Distance(transportCenter, assemblyCenter) < 300f && Vector3.Distance(escortCenter, assemblyCenter) < 400f)
            {
                Debug.Log($"[IA_TaskForceCoordinator] Frotas agrupadas no mar! Criando Comboio de Invasão para Nação {_invasionTargetTeamId}!");
                
                GameObject go = new GameObject("Gerenciador_Forca_Tarefa_" + _context.Brain.TeamId);
                IA_ForcaTarefa comboio = go.AddComponent<IA_ForcaTarefa>();
                
                // Escolhe a âncora (o transporte naval) - Por enquanto pega o primeiro helicóptero/navio de transporte
                Transform ancora = null;
                foreach(var id in transportSquad.Units)
                {
                    if (id != null) 
                    {
                        ancora = id.transform;
                        // Preferência para navios de transporte como âncora
                        var ident = id.GetComponent<IdentidadeUnidade>();
                        if (ident != null && ident.tipoUnidade == TipoUnidade.Naval) break; 
                    }
                }
                
                if (ancora != null)
                {
                    comboio.unidadeAncora = ancora;
                    
                    // Adiciona escoltas navais
                    foreach(var id in escortSquad.Units)
                    {
                        if (id != null && id.transform != ancora)
                        {
                            comboio.escoltasNavais.Add(id.transform);
                        }
                    }
                    
                    // Adiciona escoltas aereas (helicópteros que restaram)
                    foreach(var id in transportSquad.Units)
                    {
                        if (id != null && id.transform != ancora)
                        {
                            var ident = id.GetComponent<IdentidadeUnidade>();
                            if (ident != null && ident.tipoUnidade == TipoUnidade.Aereo)
                            {
                                comboio.escoltasAereas.Add(id.transform);
                            }
                        }
                    }
                    
                    comboio.IniciarDeslocamento(targetPosition);
                }
                
                // Desativa a invasão no BrainMaster para dar cooldown e resetar fase
                _invasionActive = false;
                _context.Brain.StrategicPhase = IA_StrategicPhase.LogisticaPetroleo; 
                _context.Brain.ActiveImperialPlan = "reagrupar";
            }
        }
    }
}
