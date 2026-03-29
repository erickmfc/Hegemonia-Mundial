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
        public IA_SquadDirector SquadDirector;
    }
}
