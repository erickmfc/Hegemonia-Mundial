using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_DefenseDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private float _nextDecisionTime;

        // --- Antiaérea ARES ---
        private float _nextAresCheckTime;
        private int   _aresBuilt;
        private const int   MaxAresTotal        = 6;   // Limite global de ARES na zona
        private const float AresCheckInterval   = 12f; // Segundos entre avaliações de ARES
        private const float AresMinRadius       = 40f;
        private const float AresMaxRadius       = 280f;

        // --- Reforço Terrestre Reativo ---
        private float _nextGroundReinforceTime;
        private float _lastGroundThreatTime     = -999f;
        private const float GroundAlertRadius   = 220f; // Raio de detecção de ameaça terrestre
        private const float GroundAlertScore    = 35f;  // Score mínimo para disparar produção
        private const float GroundReinforceCD   = 18f;  // Cooldown entre lotes de reforço
        private const int   TanksPerWave        = 2;
        private const int   SoldiersPerWave     = 3;

        // --- Cortina de Ferro (Iron Curtain) ---
        private float _nextIronCurtainCheckTime;
        private float _ironCurtainActiveUntil;
        private float _nextEmergencyBuildTime;
        private const float IronCurtainAlertDuration = 60f;
        private const float IronCurtainCheckInterval = 1f;

        public IA_DefenseDirector(IA_Context context)
        {
            _context = context;
        }

        public string Name    { get { return "IA_DefenseDirector"; } }
        public float  Interval { get { return 1.80f; } }
        public float  BudgetMs { get { return 0.45f; } }

        public void Tick(float now, float deltaTime)
        {
            if (_context.Brain != null && _context.Brain.IsBootstrapActive)
                return;

            // ── Bloco 1: defesas clássicas ──────────────────────────────────────
            if (now >= _nextDecisionTime)
            {
                _nextDecisionTime = now + 1.45f;
                if (_commandQueueNotSaturated())
                {
                    TickClassicDefenses(now);
                }
            }

            // ── Bloco 2: posicionamento de ARES ─────────────────────────────────
            if (now >= _nextAresCheckTime)
            {
                _nextAresCheckTime = now + AresCheckInterval;
                TickAresPlacement(now);
            }

            // ── Bloco 3: reforço terrestre reativo ──────────────────────────────
            if (now >= _nextGroundReinforceTime)
            {
                _nextGroundReinforceTime = now + 3f; // Verifica a cada 3s
                TickGroundReinforcement(now);
            }

            // ── Bloco 4: Cortina de Ferro (Iron Curtain) ─────────────────────────
            if (now >= _nextIronCurtainCheckTime)
            {
                _nextIronCurtainCheckTime = now + IronCurtainCheckInterval;
                TickIronCurtain(now);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BLOCO 4 — Cortina de Ferro (Intercepção Ativa de Mísseis/Bombardeiros)
        // ═══════════════════════════════════════════════════════════════════════

        private void TickIronCurtain(float now)
        {
            Vector3 baseCenter = GetBaseCenter();
            float radius = 1500f; // Alcance enorme de radar antecipado
            
            int teamId = _context.Brain != null ? _context.Brain.TeamId : -1;
            
            // Busca a ameaça balística ou de bombardeio mais próxima cruzando o espaço aéreo
            Transform ameaca = MissileThreatTracker.EncontrarAmeacaMaisProxima(
                baseCenter,
                radius,
                teamId,
                null,
                2f, // Multiplicador de antecipação agressivo
                45f // 45 segundos de janela de antecipação
            );

            if (ameaca != null)
            {
                if (now > _ironCurtainActiveUntil && _context.Brain != null && _context.Brain.EnableVerboseLogs)
                {
                    Debug.Log($"<color=red>[IA_DefenseDirector] CORTINA DE FERRO ATIVADA! Ameaça detectada: {ameaca.name}</color>");
                }
                _ironCurtainActiveUntil = now + IronCurtainAlertDuration;
            }

            bool isIronCurtainActive = now < _ironCurtainActiveUntil;

            if (isIronCurtainActive)
            {
                AtivarSistemasAntimissil();
                
                // Se o modo estiver ativo, acelera a construção de defesas! (com cooldown local)
                if (now >= _nextEmergencyBuildTime && _commandQueueNotSaturated())
                {
                    _nextEmergencyBuildTime = now + 10f; // Tenta construir a cada 10s enquanto em alerta
                    QueueDefensiveBuild("ciws", baseCenter, IA_TerrainType.City, 20f, 180f, 100, 5f);
                    QueueDefensiveBuild("thaad", baseCenter, IA_TerrainType.Open, 40f, 220f, 99, 8f);
                }
            }
        }

        private void AtivarSistemasAntimissil()
        {
            if (_context.WorldState == null) return;
            
            // Varre estruturas próprias para encontrar SistemaAntiMissil e liga eles
            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structObj = _context.WorldState.OwnStructures[i];
                if (structObj == null) continue;
                
                SistemaAntiMissil antimissil = structObj.GetComponent<SistemaAntiMissil>();
                if (antimissil != null)
                {
                    antimissil.DefinirModoAtivo(true);
                }
            }
            
            // Varre também unidades móveis (ex: Cruzadores Aegis)
            for (int i = 0; i < _context.WorldState.OwnCombatUnits.Count; i++)
            {
                GameObject unitObj = _context.WorldState.OwnCombatUnits[i];
                if (unitObj == null) continue;
                
                SistemaAntiMissil antimissil = unitObj.GetComponentInChildren<SistemaAntiMissil>();
                if (antimissil != null)
                {
                    antimissil.DefinirModoAtivo(true);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BLOCO 1 — Defesas estáticas clássicas (lógica original preservada)
        // ═══════════════════════════════════════════════════════════════════════

        private void TickClassicDefenses(float now)
        {
            Vector3 baseCenter = GetBaseCenter();
            IA_CounterPlan counter = _context.PlayerProfileMemory.BuildCounterPlan();
            float localThreat = _context.ThreatAnalyzer.EvaluateThreat(baseCenter, IA_Domain.Land);

            if (localThreat < 45f
                && !counter.ReinforceCoast
                && !counter.ReinforceCenter
                && !counter.AntiRush
                && counter.AirWeight < 0.25f)
            {
                return;
            }

            if (localThreat > 55f)
                QueueDefensiveBuild("torreta", baseCenter, IA_TerrainType.Choke, 35f, 140f, 93, 10f);

            if (counter.AirWeight > 0.32f || localThreat > 70f)
                QueueDefensiveBuild("ciws", baseCenter, IA_TerrainType.City, 30f, 130f, 92, 10f);

            if (counter.ReinforceCoast)
            {
                QueueDefensiveBuild("radar",   baseCenter, IA_TerrainType.Coast, 80f, 260f, 82, 16f);
                QueueDefensiveBuild("torreta", baseCenter, IA_TerrainType.Coast, 90f, 300f, 80, 12f);
            }

            if (counter.ReinforceCenter)
            {
                QueueDefensiveBuild("muro", baseCenter, IA_TerrainType.Choke, 30f, 150f, 76,  7f);
                QueueDefensiveBuild("hack", baseCenter, IA_TerrainType.Open,  60f, 180f, 74, 12f);
            }

            if (counter.AntiRush || localThreat > 85f)
                QueueDefensiveBuild("lancador missil", baseCenter, IA_TerrainType.Open, 90f, 240f, 70, 35f);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BLOCO 2 — Posicionamento de defesa antiaérea ARES
        // ═══════════════════════════════════════════════════════════════════════

        private void TickAresPlacement(float now)
        {
            if (_aresBuilt >= MaxAresTotal)
                return;

            if (_commandQueueNotSaturated() == false)
                return;

            Vector3 baseCenter = GetBaseCenter();
            IA_CounterPlan counter = _context.PlayerProfileMemory.BuildCounterPlan();
            float airThreat  = _context.ThreatAnalyzer.EvaluateThreat(baseCenter, IA_Domain.Air);
            float landThreat = _context.ThreatAnalyzer.EvaluateThreat(baseCenter, IA_Domain.Land);

            // Sempre constrói ao menos 1 ARES perto da base ao iniciar
            bool forcarPrimeiro = (_aresBuilt == 0);

            // Condições para construir ARES neste ciclo
            bool aresNecessario = forcarPrimeiro
                || airThreat > 30f
                || counter.AirWeight > 0.20f
                || landThreat > 60f; // Cobertura também contra helicópteros de apoio terrestre

            if (!aresNecessario)
                return;

            // ── Prioridade 1: Choke points (desfiladeiros, entradas) — mais difícil de contornar
            if (_aresBuilt < 2)
            {
                bool enqueued = QueueAresBuild("ares", baseCenter, IA_TerrainType.Choke,
                    AresMinRadius, 160f, 97, 20f);
                if (enqueued) return;
            }

            // ── Prioridade 2: Costa — protege contra invasão anfíbia com cobertura aérea
            if (_aresBuilt < 4 && (counter.ReinforceCoast || airThreat > 25f))
            {
                bool enqueued = QueueAresBuild("ares", baseCenter, IA_TerrainType.Coast,
                    80f, AresMaxRadius, 95, 22f);
                if (enqueued) return;
            }

            // ── Prioridade 3: Terreno aberto — cobertura de área extensa
            if (_aresBuilt < MaxAresTotal)
            {
                QueueAresBuild("ares", baseCenter, IA_TerrainType.Open,
                    60f, AresMaxRadius, 88, 25f);
            }
        }

        private bool QueueAresBuild(string itemKey, Vector3 anchor, IA_TerrainType terrain,
            float minR, float maxR, int priority, float cooldown)
        {
            Vector3 candidate = _context.MapAnalyzer.FindPointInTerrain(anchor, terrain, minR, maxR, 14);

            // Verifica se o ponto encontrado é minimamente válido (evita construir no mar)
            if (candidate == Vector3.zero || Vector3.Distance(candidate, anchor) < minR * 0.5f)
                return false;

            IA_BuildOrderData payload = new IA_BuildOrderData
            {
                ItemKey  = itemKey,
                Position = candidate,
                Rotation = Quaternion.identity,
                Zone     = IA_ZoneType.Defense
            };

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Build,
                "IA_DefenseDirector",
                "defense",
                "estrutura defensiva de resposta",
                priority,
                "defense",
                "ares_build:" + terrain.ToString().ToLower() + ":" + _aresBuilt,
                cooldown,
                payload);

            string reason;
            bool ok = _context.CommandQueue.Enqueue(request, Time.time, out reason);
            if (ok) _aresBuilt++;
            return ok;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BLOCO 3 — Reforço terrestre reativo a ameaça terrestre próxima
        // ═══════════════════════════════════════════════════════════════════════

        private void TickGroundReinforcement(float now)
        {
            Vector3 baseCenter = GetBaseCenter();

            // Avalia ameaça terrestre no raio de alerta
            float groundThreat = EvaluateGroundThreatInRadius(baseCenter, GroundAlertRadius);

            bool ameacaDetectada = groundThreat >= GroundAlertScore;

            if (ameacaDetectada)
                _lastGroundThreatTime = now;

            // Mantém alerta por 30s após a última detecção (evita ciclos de stop-start)
            bool emAlerta = (now - _lastGroundThreatTime) < 30f;

            if (!emAlerta)
                return;

            if (now < _nextGroundReinforceTime + GroundReinforceCD)
                return; // Ainda no cooldown de produção

            if (_commandQueueNotSaturated() == false)
                return;

            IA_ForceSnapshot snap = _context.ForceSnapshot;
            if (snap == null)
                return;

            // ── Decide o que produzir com base no inventário atual ──
            bool precisaTank    = snap.TankUnits < 6;
            bool precisaSoldado = snap.InfantryUnits < 12;

            // Se já tem tropas suficientes, não precisa de reforço urgente
            if (!precisaTank && !precisaSoldado)
                return;

            int wavesQueued = 0;

            // Produz Tanks quando há fábrica disponível
            if (precisaTank && snap.HasFactory)
            {
                for (int i = 0; i < TanksPerWave; i++)
                {
                    QueueGroundUnit("tank", 96 - i);
                }
                wavesQueued++;
            }

            // Produz Soldados quando há quartel disponível
            if (precisaSoldado && snap.HasBarracks)
            {
                for (int i = 0; i < SoldiersPerWave; i++)
                {
                    QueueGroundUnit("soldado", 94 - i);
                }
                wavesQueued++;
            }

            if (wavesQueued > 0)
            {
                // Próxima leva de reforço só depois do cooldown
                _nextGroundReinforceTime = now + GroundReinforceCD;

                if (_context.Brain != null && _context.Brain.EnableVerboseLogs)
                {
                    Debug.Log(
                        $"[IA_DefenseDirector] Alerta terrestre! Score={groundThreat:F1} " +
                        $"→ Produzindo {TanksPerWave} tanks + {SoldiersPerWave} soldados. " +
                        $"Team={_context.Brain.TeamId}"
                    );
                }
            }
        }

        private float EvaluateGroundThreatInRadius(Vector3 center, float radius)
        {
            var enemies = _context.WorldState.VisibleEnemies;
            float total = 0f;
            float sqrRadius = radius * radius;

            for (int i = 0; i < enemies.Count; i++)
            {
                IA_EnemyObservation obs = enemies[i];
                if (obs == null || obs.Domain != IA_Domain.Land)
                    continue;

                Vector3 delta = obs.Position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude > sqrRadius)
                    continue;

                float distFactor = Mathf.Clamp01(1f - (delta.magnitude / radius));
                total += obs.ThreatScore * distFactor;
            }

            return total;
        }

        private void QueueGroundUnit(string itemKey, int priority)
        {
            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Produce,
                "IA_DefenseDirector",
                "production",
                "reforco terrestre reativo",
                priority,
                "production",
                "ground_reinforce:" + IA_Text.Normalize(itemKey) + ":" + Time.frameCount,
                4f,
                new IA_ProduceOrderData
                {
                    ItemKey = itemKey,
                    Quantity = 1
                });

            string reason;
            _context.CommandQueue.Enqueue(request, Time.time, out reason);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers comuns
        // ═══════════════════════════════════════════════════════════════════════

        private bool _commandQueueNotSaturated()
        {
            return _context.CommandQueue.PendingCount <= 6;
        }

        private Vector3 GetBaseCenter()
        {
            Vector3 center = _context.WorldState.BaseCenter;
            if (center == Vector3.zero && _context.Brain != null)
                center = _context.Brain.transform.position;
            return center;
        }

        private void QueueDefensiveBuild(
            string itemKey,
            Vector3 anchor,
            IA_TerrainType terrain,
            float minRadius,
            float maxRadius,
            int priority,
            float cooldown)
        {
            Vector3 candidate = _context.MapAnalyzer.FindPointInTerrain(anchor, terrain, minRadius, maxRadius, 12);
            IA_BuildOrderData payload = new IA_BuildOrderData
            {
                ItemKey  = itemKey,
                Position = candidate,
                Rotation = Quaternion.identity,
                Zone     = IA_ZoneType.Defense
            };

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Build,
                "IA_DefenseDirector",
                "defense",
                "construcao defensiva de contingencia",
                priority,
                "defense",
                "defense_build:" + IA_Text.Normalize(itemKey),
                cooldown,
                payload);

            string reason;
            _context.CommandQueue.Enqueue(request, Time.time, out reason);
        }
    }
}
