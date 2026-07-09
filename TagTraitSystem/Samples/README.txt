TagTraitSystem Samples

Purpose
- This sample shows how to call the public TagContainer API from a MonoBehaviour.
- It provides a small controller, a logging trait, and manual setup steps.
- It does not create scenes, ScriptableObject assets, UI, input bindings, or editor buttons.

Assembly Structure
- TagTraitSystem.Samples references TagTraitSystem.Runtime.
- Runtime and Editor assemblies do not reference Samples.
- The EditMode test assembly references Samples only for sample verification.

Sample TagID Values
- SampleKeyword = 100
- SamplePermanent = 101
- SampleRefresh = 102
- SampleMaxDuration = 103
- SampleStackCount = 104

These values are sample-only IDs. Replace or extend them with project-specific IDs as needed.

Manual Scene Setup
1. Create an empty GameObject.
2. Add TagContainer.
3. Add TagTraitSampleController.
4. Create a SampleLogTraitSO asset from Assets > Create > TagTraitSystem > Samples > Logging Trait.
5. Create TagDefinition assets for the sample policies listed below.
6. Assign one TagDefinition to selectedDefinition on the controller.
7. Set duration and autoTick on the controller.
8. Use the component context menu to call AddPermanent, AddPerishable, Remove, Activate, TickOneSecond, or PrintCurrentTags.
9. Check the Console for SampleLogTrait and sample event logs.

Sample Definition Settings
- Keyword:
  TagID = SampleKeyword
  Category = Keyword
  Trait = None
  StackPolicy = None
  MaxStackCount = 1
- Permanent:
  TagID = SamplePermanent
  Category = Status
  Trait = SampleLogTraitSO
  StackPolicy = None
  MaxStackCount = 1
- Refresh:
  TagID = SampleRefresh
  Category = Status
  Trait = SampleLogTraitSO
  StackPolicy = Refresh
  MaxStackCount = 1
- MaxDuration:
  TagID = SampleMaxDuration
  Category = Status
  Trait = SampleLogTraitSO
  StackPolicy = MaxDuration
  MaxStackCount = 1
- StackCount:
  TagID = SampleStackCount
  Category = Status
  Trait = SampleLogTraitSO
  StackPolicy = StackCount
  MaxStackCount = 3

Context Menu Commands
- Add Permanent Tag calls TagContainer.TagAdd(selectedDefinition, gameObject).
- Add Perishable Tag calls TagContainer.PerishableTagAdd(selectedDefinition, duration, gameObject).
- Remove Selected Tag calls TagContainer.TagSub(selectedDefinition, gameObject).
- Activate Selected Tag calls TagContainer.TagActivate(selectedDefinition, gameObject).
- Tick One Second calls TagContainer.Tick(1f).
- Print Current Tags calls TagContainer.TagScan() and logs the snapshot.

autoTick
- When autoTick is true, Update calls TagContainer.Tick(Time.deltaTime).
- When autoTick is false, the controller does not advance time automatically.
- In Play Mode, confirm that RemainingTime decreases when autoTick is true.
- In Play Mode, confirm that RemainingTime does not decrease automatically when autoTick is false.

Manual Test Scenarios
- Keyword: Add, Activate, Remove.
- Permanent: Add, Activate, Remove.
- Refresh: Add perishable, Tick, Add perishable again, let it expire.
- MaxDuration: Add perishable, Tick, extend with a higher duration, try a lower no-op duration, let it expire.
- StackCount: Add perishable, Tick, add another stack, remove or let stacks expire.
- Perishable to Permanent: Add perishable, then call Add Permanent with the same definition.
- Multiple controllers: Use different definitions on different GameObjects.
- Validator: Run Tools/TagTraitSystem/Validate Tag Definitions after creating assets.

Notes and Limitations
- The sample logging trait does not store runtime target, source, instance, or call count state.
- Do not assign a trait to a Keyword definition.
- Non-keyword definitions need a trait to activate.
- StackCount samples should use MaxStackCount = 3.
- If another tag changes StackCount during a Tick callback, the final StackCount can differ depending on processing order. Design StackCount Tick changes separately before implementing deferred or replay logic.
- This sample intentionally excludes UI, Input System, custom inspectors, save/load, networking, Addressables, automatic ID generation, ExclusiveGroup, Priority, and IsSaveable behavior.
