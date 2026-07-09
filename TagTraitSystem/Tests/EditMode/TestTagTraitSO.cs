using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Traits;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TestTagTraitSO : TagTraitSO
    {
        public override bool OnActivate(TraitContext context)
        {
            return true;
        }
    }
}
