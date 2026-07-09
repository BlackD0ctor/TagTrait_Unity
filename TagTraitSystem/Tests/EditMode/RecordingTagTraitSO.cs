using System;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Traits;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class RecordingTagTraitSO : TagTraitSO
    {
        public int OnAddCallCount { get; private set; }
        public int OnRemoveCallCount { get; private set; }
        public int OnActivateCallCount { get; private set; }
        public TraitContext LastOnAddContext { get; private set; }
        public TraitContext LastOnRemoveContext { get; private set; }
        public TraitContext LastOnActivateContext { get; private set; }
        public bool ActivateResult { get; set; } = true;
        public Action<TraitContext> OnAddAction { get; set; }
        public Action<TraitContext> OnRemoveAction { get; set; }
        public Action<TraitContext> OnActivateAction { get; set; }
        public bool ThrowOnAdd { get; set; }
        public bool ThrowOnRemove { get; set; }
        public bool ThrowOnActivate { get; set; }

        public override void OnAdd(TraitContext context)
        {
            OnAddCallCount++;
            LastOnAddContext = context;
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("OnAdd failed");
            }

            if (OnAddAction != null)
            {
                OnAddAction(context);
            }
        }

        public override void OnRemove(TraitContext context)
        {
            OnRemoveCallCount++;
            LastOnRemoveContext = context;
            if (ThrowOnRemove)
            {
                throw new InvalidOperationException("OnRemove failed");
            }

            if (OnRemoveAction != null)
            {
                OnRemoveAction(context);
            }
        }

        public override bool OnActivate(TraitContext context)
        {
            OnActivateCallCount++;
            LastOnActivateContext = context;
            if (ThrowOnActivate)
            {
                throw new InvalidOperationException("OnActivate failed");
            }

            if (OnActivateAction != null)
            {
                OnActivateAction(context);
            }

            return ActivateResult;
        }
    }
}
