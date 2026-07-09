TagTraitSystem README

Version
- 0.1.0
- Status: Initial functional preview.

Purpose
- TagTraitSystem lets a Unity GameObject hold tags through TagContainer.
- A tag can be queried, activated, expired, stacked, refreshed, or converted from perishable to permanent.
- Non-keyword tags call an attached Trait to execute behavior.
- Keyword tags are data-only tags and do not execute traits.

Target Environment
- Unity 6 LTS 6000.0.68f1.
- API Compatibility: .NET Standard 2.1.
- Unity Test Framework 1.6.0.
- Windows is the primary verification target.
- Windows IL2CPP Development Build is a manual verification target.

Folder Structure
- Runtime: runtime API, core data types, diagnostics, traits, and TagContainer.
- Editor: TagDefinition validator menu.
- Samples: sample controller, sample logging trait, and sample setup README.
- Tests/EditMode: unit, integration, validator, diagnostics, and sample tests.
- Documents: 0.1.0 user and verification documents.

Assembly Structure
- TagTraitSystem.Runtime references no project assemblies.
- TagTraitSystem.Editor references TagTraitSystem.Runtime.
- TagTraitSystem.Samples references TagTraitSystem.Runtime.
- TagTraitSystem.Tests.EditMode references Runtime, Editor, and Samples.

Creating TagDefinition Assets
- Use Assets > Create > TagTraitSystem > Tag Definition.
- Required serialized fields are tagID, tagName, category, trait, stackPolicy, maxStackCount, exclusiveGroup, priority, and isSaveable.
- Keyword tags use Category = Keyword and Trait = None.
- Non-keyword tags use a Trait, usually a TagTraitSO subclass.
- StackPolicy.StackCount requires MaxStackCount >= 1.

Using TagContainer
- Add TagContainer to the target GameObject.
- Use TagAdd(TagDefinition, GameObject source = null) for permanent tags.
- Use PerishableTagAdd(TagDefinition, float duration, GameObject source = null) for perishable tags.
- Use TagSub(TagDefinition, GameObject source = null) to remove or reduce a held tag.
- Use TagActivate(TagDefinition, GameObject source = null) to call trait activation.
- Call Tick(float deltaTime) from your own time owner. TagContainer does not call Tick automatically.

Queries and Scans
- TagCheck(TagDefinition) and TagCheck(TagID) check held tags by ID.
- TagANDCheck(params TagDefinition[]) requires all valid definitions to be held.
- TagORCheck(params TagDefinition[]) requires at least one valid definition to be held.
- TryGetTag(TagDefinition, out TagInstance) returns the stored instance by TagID.
- TagScan() returns a snapshot collection. The collection composition is fixed, but TagInstance references are shared.

Policies
- StackPolicy.None rejects duplicate same-definition adds unless a perishable tag is converted to permanent.
- StackPolicy.Refresh replaces Duration and RemainingTime for an existing perishable tag.
- StackPolicy.MaxDuration extends only when the new normalized duration is higher than the current comparison value.
- StackPolicy.StackCount increases or decreases StackCount up to MaxStackCount.
- Perishable StackCount uses the same normalized Duration for every stack.

Perishable to Permanent Conversion
- Calling TagAdd with the same definition on a held perishable tag converts the existing TagInstance to permanent.
- The same TagInstance reference is kept.
- Duration and RemainingTime become 0.
- StackCount is preserved.
- OnTagUpdated is raised with TagChangeReason.ChangedToPermanent.

Traits
- Implement ITagTrait or derive from TagTraitSO.
- TagTraitSO provides virtual OnAdd and OnRemove and requires OnActivate.
- TraitContext provides Container, Target, Source, Definition, and Instance.
- Trait callbacks run for non-keyword tags with a non-null Trait.
- OnAdd runs before OnTagAdded.
- OnRemove runs before OnTagRemoved.
- OnActivate returns the boolean result from the trait.

Events
- OnTagAdded fires after a new tag instance is added.
- OnTagRemoved fires after a tag is removed or expires.
- OnTagUpdated fires after refresh, max-duration extension, stack change, or perishable to permanent conversion.
- TagChangeEventData includes Container, Definition, Instance, Source, Reason, PreviousDuration, PreviousRemainingTime, and PreviousStackCount.

Diagnostics
- Runtime logging goes through TagDiagnostics.
- TagDiagnostics.Log and LogWarning output only in the Editor or Development Build.
- TagDiagnostics.LogError always logs an error.
- Normal no-op operations return false without logging unless the operation is invalid.

Editor Validator
- Use Tools/TagTraitSystem/Validate Tag Definitions.
- The validator reports errors and warnings for TagDefinition assets.
- It does not auto-fix assets.

Samples
- See Assets/TagTraitSystem/Samples/README.txt.
- SampleLogTraitSO menu: TagTraitSystem/Samples/Logging Trait.
- TagTraitSampleController exposes context menu commands for add, remove, activate, tick, and scan.

Known Limitations
- See TagTraitSystem_KNOWN_LIMITATIONS.txt.
- 0.1.0 is a preview. Public API changes can occur before 1.0.

Final Verification
- Run all EditMode tests in Unity TestRunner.
- Run the Validator manually.
- Configure and run the Samples manually.
- Build and run a Windows IL2CPP Development Build.
- Review the final verification checklist before marking 0.1.0 complete.
