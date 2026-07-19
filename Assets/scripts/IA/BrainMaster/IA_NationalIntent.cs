using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public enum IA_IntentKind { Build, Produce, Trade, Defend, Attack, Recon, Recover }

    [Serializable]
    public sealed class IA_NationalIntent
    {
        public string Id;
        public string DedupKey;
        public string Origin;
        public string Reason;
        public IA_IntentKind Kind;
        public int Priority;
        public float Urgency;
        public float Risk;
        public float EstimatedCost;
        public float ExpectedBenefit;
        public float ValidUntil;
        public bool Feasible = true;
        public bool Approved;
        public bool QueuedToCommand;
        public string RejectionReason;
        [NonSerialized] public IA_CommandRequest Command;

        public float Score(float availableBudget, float nationalRisk, float styleBias)
        {
            float affordability = availableBudget <= 0f ? -EstimatedCost : Mathf.Clamp01(availableBudget / Mathf.Max(1f, EstimatedCost));
            return Priority + Urgency * 0.8f + ExpectedBenefit * 0.7f + styleBias + affordability * 15f - Risk * 0.8f - nationalRisk * 0.5f;
        }
    }

    public sealed class IA_NationalIntentBoard
    {
        private readonly List<IA_NationalIntent> _pending = new List<IA_NationalIntent>();
        private readonly Dictionary<string, IA_NationalIntent> _byKey = new Dictionary<string, IA_NationalIntent>();
        public int PendingCount { get { return _pending.Count; } }

        public bool Publish(IA_NationalIntent intent, float now, out string reason)
        {
            reason = string.Empty;
            if (intent == null) { reason = "intencao nula"; return false; }
            intent.Id = string.IsNullOrEmpty(intent.Id) ? Guid.NewGuid().ToString("N") : intent.Id;
            intent.Origin = string.IsNullOrWhiteSpace(intent.Origin) ? "desconhecido" : intent.Origin.Trim();
            intent.DedupKey = string.IsNullOrWhiteSpace(intent.DedupKey) ? intent.Kind + ":" + intent.Origin : intent.DedupKey.Trim();
            intent.ValidUntil = intent.ValidUntil <= now ? now + 30f : intent.ValidUntil;
            if (_byKey.ContainsKey(intent.DedupKey)) { reason = "intencao duplicada"; return false; }
            _pending.Add(intent);
            _byKey.Add(intent.DedupKey, intent);
            return true;
        }

        public void Remove(IA_NationalIntent intent)
        {
            if (intent == null) return;
            _pending.Remove(intent);
            if (!string.IsNullOrEmpty(intent.DedupKey)) _byKey.Remove(intent.DedupKey);
        }

        public List<IA_NationalIntent> Snapshot(float now)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
                if (_pending[i] == null || _pending[i].ValidUntil < now) Remove(_pending[i]);
            return new List<IA_NationalIntent>(_pending);
        }
    }

    public sealed class IA_StrategyArbiter
    {
        public IA_NationalIntent Select(IA_NationalIntentBoard board, float now, float availableBudget, float nationalRisk, float styleBias)
        {
            if (board == null) return null;
            List<IA_NationalIntent> candidates = board.Snapshot(now);
            IA_NationalIntent best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                IA_NationalIntent candidate = candidates[i];
                if (candidate == null || !candidate.Feasible || candidate.EstimatedCost > availableBudget) continue;
                float score = candidate.Score(availableBudget, nationalRisk, styleBias);
                if (score > bestScore) { best = candidate; bestScore = score; }
            }
            if (best != null) best.Approved = true;
            return best;
        }
    }

    public sealed class IA_IntentCommandRouter : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private float _nextRouteTime;
        public IA_IntentCommandRouter(IA_Context context) { _context = context; }
        public string Name { get { return "IA_IntentCommandRouter"; } }
        public float Interval { get { return 0.20f; } }
        public float BudgetMs { get { return 0.08f; } }

        public void Tick(float now, float deltaTime)
        {
            if (_context == null || _context.IntentBoard == null || _context.StrategyArbiter == null || _context.CommandQueue == null || _context.Brain == null) return;
            if (now < _nextRouteTime) return;
            _nextRouteTime = now + Interval;
            float risk = _context.CombatPressure != null && _context.CombatPressure.EnemyVisible ? 30f : 0f;
            float styleBias = (_context.Brain.IndustryWeight - _context.Brain.MilitarismWeight) * 10f;
            IA_NationalIntent intent = _context.StrategyArbiter.Select(_context.IntentBoard, now, _context.Brain.Credits, risk, styleBias);
            if (intent == null || intent.Command == null) return;

            string reason;
            if (_context.CommandQueue.Enqueue(intent.Command, now, out reason))
            {
                intent.QueuedToCommand = true;
                _context.IntentBoard.Remove(intent);
                IA_RuntimeTextTrace.LogCommand(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_IntentRouter", "INTENT_TO_QUEUE", intent.Command, "intencao aceita e roteada");
                return;
            }

            // A fila ja possui a mesma acao ou esta em cooldown: nao insistir numa intencao obsoleta.
            if (reason == "duplicada em fila" || reason == "em cooldown")
            {
                intent.RejectionReason = reason;
                _context.IntentBoard.Remove(intent);
                IA_RuntimeTextTrace.LogCommand(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_IntentRouter", "INTENT_DROP", intent.Command, reason);
            }
            else
            {
                intent.Approved = false;
                intent.RejectionReason = reason;
                IA_RuntimeTextTrace.LogCommand(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_IntentRouter", "INTENT_WAIT", intent.Command, reason);
            }
        }
    }
}
