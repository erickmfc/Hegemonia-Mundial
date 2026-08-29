using System;

namespace Hegemonia.AI.IA02
{
    public interface IIA02Module
    {
        string ModuleId { get; }
        bool IsDirty { get; }
        bool IsEnabled { get; }

        void Initialize(IA02RuntimeContext context);
        void MarkDirty(IA02DirtyReason reason);
        IA02WorkResult ExecuteSlice(IA02WorkBudget budget);
        void Shutdown();
    }
}
