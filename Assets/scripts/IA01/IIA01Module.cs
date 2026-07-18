using System;

namespace Hegemonia.AI.IA01
{
    public interface IIA01Module
    {
        string ModuleId { get; }
        bool IsDirty { get; }
        bool IsEnabled { get; }

        void Initialize(IA01RuntimeContext context);
        void MarkDirty(IA01DirtyReason reason);
        IA01WorkResult ExecuteSlice(IA01WorkBudget budget);
        void Shutdown();
    }
}
