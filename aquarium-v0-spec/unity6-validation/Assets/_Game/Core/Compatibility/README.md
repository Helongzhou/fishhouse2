# Compatibility assessment core

Independent, pure C# 7.3-compatible domain logic in `Aquarium.Compatibility`. It does not reference Unity, the simulation core, categories that imply automatic predation, or external packages.

## Contract

Call `CompatibilityEvaluator.Evaluate(tank, completeComposition)`, not just the candidate fish versus a favorite existing species. Each `StockGroup` is a homogeneous life-stage/sex/size cohort. Groups of the same species reference one shared, reviewed `SpeciesProfile` instance and use unique group IDs. Mixed or unknown sex/stage must remain explicit; never default an unsexed betta to female.

The evaluator collects **all** applicable reasons; its headline status does not hide lower-priority findings. Severity is `HardBlock`, `HighRisk`, `Conditional`, or `PendingVerification`. A clean modeled result is `Monitor` and explicitly still requires quarantine, staged introduction, observation, and an isolation plan. `IsEligibleForIntroduction` is false for every unresolved severity; it is not a survival guarantee when true.

`ReasonDomain.ContentPolicy` separates V0 purchasing restrictions from ecology. `Planned` and `ReferenceOnly` are hard purchase blocks; they do not claim the animal is ecologically unsuitable. Unknown policy remains unverified.

Water type, temperature, pH, and GH are evaluated across the entire composition. `CommunityRange.KnownIntersection` only describes known profiles; check `AllSpeciesVerified` before treating it as a complete range. Actual tank measurements are separately checked. One shared boundary is allowed; a nonempty interval is still no guarantee of long-term compatibility.

Adult length, adult height, usable swimming envelope, and usable swimming volume must be supplied separately. Juveniles do not bypass adult-space planning. These are per-species space checks; stocking/bioload/filtration capacity and nitrogen-cycle readiness remain owned by their corresponding systems, not silently approved here.

The `bottom_sifter` profile tag requires soft sand and suitable sinking food. It never makes a fish a feces cleaner. `fin_nipper` to `long_fin` produces a **directional high risk**, with a named source and target; sufficient group size does not make long fins automatically safe. `betta_male_conspecific_isolation` blocks multiple same-species adult males and leaves unknown sex/stage unverified. Other combinations still need conditional review; it does not assert all bettas attack all fish.

Predation uses a reviewed `CanPreyOnSmallerAnimals` flag plus current gape and the target's current body cross-section. Adult dimensions may only fill missing current dimensions for an explicitly adult cohort. Unknown gape or body cross-section is unresolved; no fish/shrimp category causes an automatic death judgment. A swallowing risk is a warning, not a death roll. Gape below cross-section only means this narrow model did not flag swallowing; it does not rule out injury or other conflicts.

## Localization

Reasons return `Code`, `MessageKey`, `RepairKey`, `Parameters`, stable source species ID, target ID, target species ID, and affected species IDs. UI resolves keys through the selected locale and its registered fallback chain, with English as the final fallback. Locale coverage comes from `data/localization/locales.json`; no particular translation is hardcoded into this evaluator. Separately resolve species IDs and parameter labels for display. `Summary` is an English debugging fallback; use `SummaryKey` for player text.

Keys are `compatibility.reason.<CODE>.message` / `.repair`. Common parameters are `source`, `target`, `source_count`, `required_count`, `parameter`, `source_species_id`, `target_id`, and `target_species_id`. Adult-space failures add `required` / `available`; empty intersections add `lower_bound` / `upper_bound`. IDs are stable technical identifiers; the UI may map them to localized labels before substitution. Affected species IDs preserve whole-community context, including the two boundary-setting profiles.

`compatibility-checks/reason-catalog.json` provides 58 initial keys each for English and Simplified Chinese, for the owning generator to merge into shared catalogs. These two initial catalogs do not limit the product to two languages. New locales have independent packages and registry status; planned or QA packages are not release-support claims. Do not use this proposal as an excuse to bypass the shared-catalog coverage check.

## Checks

From project root:

```sh
./unity/.tools/dotnet/dotnet run --project compatibility-checks/CompatibilityChecks.csproj
```

The .NET 8 console project links these production C# files while enforcing `LangVersion=7.3` and warnings as errors. It uses no test framework or package download. It covers whole-community range traps, unknown data, adult and usable space, same-species counts, sex/stage, directional conflicts, substrate/food needs, size-based predation, purchasing policy, aggregation, and registry-driven catalog key/placeholder coverage. Profiles in tests are synthetic fixtures, not production husbandry recommendations.
