namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_Context
    {
        public IA_BrainMaster Brain;
        public IA_WorldState WorldState;
        public IA_MapAnalyzer MapAnalyzer;
        public IA_PlayerProfileMemory PlayerProfileMemory;
        public IA_ThreatAnalyzer ThreatAnalyzer;
        public IA_CommandQueue CommandQueue;
        public IA_BackendBridge Backend;
        public IA_PerformanceScheduler Scheduler;
        public IA_DebugMonitor DebugMonitor;
        public IA_CombatPressure CombatPressure;
        public IA_ForceSnapshot ForceSnapshot;
        public IA_PerformanceStateData PerformanceGovernorState;
        public IA_EngagementBudget EngagementBudget;
        public IA_TransportPlan TransportPlan;
        public IA_BattleGovernorDecision BattleDecision;
        public IA_BuildDirector BuildDirector;
        public IA_SquadDirector SquadDirector;
        public IA_NationalDecisionState NationalDecisionState;
        public IA_GrandStrategy GrandStrategy;
        public IA_EconomyDirector EconomyDirector;
        public IA_LawDirector LawDirector;
        public IA_DiplomacyDirector DiplomacyDirector;
        public IA_MarketDirector MarketDirector;
        public IA_LogisticsDirector LogisticsDirector;
        public IA_WarDirector WarDirector;
        public IA_SyncNetwork SyncNetwork;
        public IA_SemanticMapPlanner SemanticMapPlanner;
        public IA_ZonePlanner ZonePlanner;
        public IA_LotPlanner LotPlanner;
        public IA_UrbanBuildValidator UrbanBuildValidator;
        public IA_ConstructionPlanner ConstructionPlanner;
    }
}
