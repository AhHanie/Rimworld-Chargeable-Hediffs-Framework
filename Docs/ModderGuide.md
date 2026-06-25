# Chargeable Hediffs Framework - Modder Guide

**Package ID:** `sk.chargehediff`
**RimWorld Version:** 1.6
**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3671242214

---

## Table of Contents

- [Overview](#overview)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Hediff Comp XML Reference](#hediff-comp-xml-reference)
  - [HediffCompProperties_Rechargeable fields](#hediffcompproperties_rechargeable-fields)
  - [ChargeConsequenceProperties fields](#chargeconsequenceproperties-fields)
  - [Understanding units](#understanding-units)
- [Charge Station XML Reference](#charge-station-xml-reference)
  - [ChargeStationExtension fields](#chargestationextension-fields)
  - [Station requirements](#station-requirements)
- [XML Examples](#xml-examples)
  - [Rechargeable bionic limb - no consequences](#rechargeable-bionic-limb--no-consequences)
  - [Rechargeable bionic limb - depleted stat penalties](#rechargeable-bionic-limb--depleted-stat-penalties)
  - [Rechargeable artificial organ - part efficiency penalty](#rechargeable-artificial-organ--part-efficiency-penalty)
  - [Charge station - open (accepts all hediffs)](#charge-station--open-accepts-all-hediffs)
  - [Charge station - hediff whitelist](#charge-station--hediff-whitelist)
- [Public API](#public-api)
  - [HediffComp_Rechargeable](#hediffcomp_rechargeable)
  - [HediffChargeUtility](#hediffchargeutility)
- [Compatibility Notes](#compatibility-notes)

---

## Overview

Chargeable Hediffs Framework adds a reusable `HediffComp` that gives any hediff an independent electrical charge store. Pawns can recharge at any powered building you tag with a `DefModExtension`. Everything is opt-in and purely XML-driven - no required C# base classes, no new Defs to extend.

**What the framework provides:**

- `HediffComp_Rechargeable` - tracks current/max charge, decays over time, stores active depleted consequences in a pawn cache comp; stats use vanilla `ThingComp` stat hooks, part efficiency uses one targeted capacity patch.
- `ChargeStationExtension` - marks any powered `ThingDef` as a charge station.
- Automatic work assignment - colonists/slaves/player mechs seek charge stations when any eligible hediff falls below 20 %.
- Right-click manual recharge - select a pawn, right-click a powered station, choose _Recharge_.
- Low-charge alert - grouped alert when any eligible pawn's lowest hediff charge falls below the threshold.

---

## Requirements

Your mod must list this framework as a dependency:

```xml
<modDependencies>
    <li>
        <packageId>sk.chargehediff</packageId>
        <displayName>Chargeable Hediffs Framework</displayName>
    </li>
</modDependencies>
```

The framework itself requires **Harmony** (`brrainz.harmony`), which is already listed in its own `About.xml`. You do not need to redeclare it.

---

## Quick Start

Three XML changes are all that is needed:

**1. Add the comp to a hediff:**

```xml
<comps>
    <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_Rechargeable">
        <maxCharge>100</maxCharge>
        <chargeDecayPerTick>0.00167</chargeDecayPerTick>
    </li>
</comps>
```

**2. Tag a powered building as a charge station:**

```xml
<modExtensions>
    <li Class="Chargeable_Hediffs_Framework.ChargeStationExtension" />
</modExtensions>
```

**3. Ensure the building has `hasInteractionCell = true` and a `CompPowerTrader`** - that's it.

The pawn will now show charge percentage in the Health tab (`Bionic Arm (74%)`), seek the station automatically when charge drops below 20 %, and accept manual recharge orders via right-click.

---

## Hediff Comp XML Reference

### HediffCompProperties_Rechargeable fields

| Field                  | Type    | Default | Description                                                                                                                                                 |
| ---------------------- | ------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `maxCharge`            | `float` | `1`     | Maximum stored charge. Must be positive. Choose any scale; see [Understanding units](#understanding-units).                                                 |
| `startingCharge`       | `float` | `-1`    | Charge when the hediff is first created. **Negative means start at `maxCharge`** (full). Set to `0` to start depleted.                                      |
| `chargeDecayPerTick`   | `float` | `0`     | Charge lost per game tick while the pawn is alive and active. `0` means no decay (charge is permanent until drained externally).                            |
| `depletedConsequences` | element | `null`  | Optional `ChargeConsequenceProperties` block. Penalties are cached in the pawn's `CompChargeConsequencesCache` and applied only while `IsDepleted` is true. |

**Example with all fields:**

```xml
<li Class="Chargeable_Hediffs_Framework.HediffCompProperties_Rechargeable">
    <maxCharge>100</maxCharge>
    <startingCharge>50</startingCharge>
    <chargeDecayPerTick>0.00167</chargeDecayPerTick>
    <depletedConsequences>
        <statOffsets>
            <MeleeDPS>-3</MeleeDPS>
        </statOffsets>
        <partEfficiencyOffset>-0.5</partEfficiencyOffset>
    </depletedConsequences>
</li>
```

### ChargeConsequenceProperties fields

These penalties are active **only while the hediff's charge is zero** (`IsDepleted == true`). They are cached in the pawn's `CompChargeConsequencesCache` and applied through vanilla `ThingComp.GetStatOffset`/`GetStatFactor` hooks (stats) and a single Harmony postfix on `PawnCapacityUtility.CalculatePartEfficiency` (part efficiency). They do not create hediff stages or modify `CurStage`.

| Field                  | Type                 | Default | Description                                                                                                                                                                   |
| ---------------------- | -------------------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `statOffsets`          | `List<StatModifier>` | `null`  | Flat additions to global pawn stats when depleted. Use negative values for penalties. Same syntax as `HediffStage.statOffsets`.                                               |
| `statFactors`          | `List<StatModifier>` | `null`  | Multiplicative factors applied to global pawn stats when depleted. `0.5` means 50 % of base value. Same syntax as `HediffStage.statFactors`.                                  |
| `partEfficiencyOffset` | `float`              | `0`     | Added to the efficiency of the body part this hediff is installed on when depleted. `-0.5` halves the part's contribution to capacities. Only affects the anchored body part. |

> **Note:** `statOffsets` and `statFactors` are **global** - they apply to all pawns regardless of which body part the hediff is on. `partEfficiencyOffset` is **local** - it only affects the hediff's anchored part.

### Understanding units

`maxCharge`, `startingCharge`, `chargeDecayPerTick`, and `chargeRate` (on the station) all use the **same arbitrary unit**. You choose the scale; the framework does not enforce one. The only constraint is internal consistency between decay and recharge.

**Useful reference values:**

| Quantity      | Ticks    |
| ------------- | -------- |
| 1 game day    | 60,000   |
| 1 game hour   | 2,500    |
| 1 game season | ~900,000 |

**Example mental model - "charge points equal ticks of life":**

```
maxCharge         = 600000    → 10-day battery at full charge
chargeDecayPerTick = 1        → loses 1 charge per tick → 600,000 ticks = 10 days
```

**Smaller-number mental model - "charge is a 0–100 percentage":**

```
maxCharge         = 100
chargeDecayPerTick = 0.00167  → 100 / 0.00167 ≈ 60,000 ticks ≈ 1 day to deplete
chargeRate (station) = 0.00333 → recharges from 0 to 100 in ~30,000 ticks ≈ 0.5 days
```

**Power-linked recharge (station `chargeRate = 0`):**

When `chargeRate` is omitted or `0` on a station, the framework reads the station's `CompProperties_Power.PowerConsumption` and computes:

```
effectiveChargeRate = PowerConsumption / 60000
```

This creates a natural balance: if your station draws 200 W and your hediff has `maxCharge = 200`, one day of power fully recharges one hediff.

---

## Charge Station XML Reference

### ChargeStationExtension fields

| Field              | Type              | Default | Description                                                                                                                                                 |
| ------------------ | ----------------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `chargeRate`       | `float`           | `0`     | Charge added per tick to each eligible hediff while a pawn occupies this station. When `0`, falls back to `PowerConsumption / 60000`. Must not be negative. |
| `supportedHediffs` | `List<HediffDef>` | `null`  | Whitelist of `HediffDef` names this station can service. Empty or `null` means **all rechargeable hediffs** are accepted.                                   |

### Station requirements

The framework validates these at game load and logs config errors if they are missing:

1. The `ThingDef` must have **`hasInteractionCell = true`**.
2. The `ThingDef` must have a **`CompPowerTrader`** comp (`CompProperties_Power`).
3. `chargeRate` must be `>= 0` (zero uses the power-consumption fallback).
4. All entries in `supportedHediffs` must resolve to valid `HediffDef`s.

The building does **not** need to extend any special class. Any `ThingDef` that meets the requirements above works as a charge station.

---

## XML Examples

### Rechargeable bionic limb - no consequences

A powered bionic arm that shows charge in the Health tab and triggers the auto-recharge work giver. No stat penalties when depleted.

```xml
<HediffDef>
    <defName>MyMod_BionicArm_Rechargeable</defName>
    <label>powered bionic arm</label>
    <description>A bionic arm that requires periodic recharging. Provides full strength while charged.</description>
    <hediffClass>HediffWithComps</hediffClass>
    <defaultLabelColor>(0.5, 0.5, 1.0)</defaultLabelColor>
    <isBad>false</isBad>
    <addedPartProps>
        <solid>true</solid>
        <partEfficiencyOffset>0.5</partEfficiencyOffset>
    </addedPartProps>
    <comps>
        <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_Rechargeable">
            <maxCharge>100</maxCharge>
            <!-- startingCharge omitted (negative) → starts at maxCharge -->
            <chargeDecayPerTick>0.00167</chargeDecayPerTick>
            <!-- ~60,000 ticks (~1 day) to deplete from full -->
        </li>
    </comps>
</HediffDef>
```

Health tab label: **Powered bionic arm (74%)**

---

### Rechargeable bionic limb - depleted stat penalties

Same arm, but losing melee damage and work speed when the battery runs out.

```xml
<HediffDef>
    <defName>MyMod_BionicArm_Rechargeable</defName>
    <label>powered bionic arm</label>
    <description>A bionic arm that requires periodic recharging. Suffers performance loss when depleted.</description>
    <hediffClass>HediffWithComps</hediffClass>
    <isBad>false</isBad>
    <addedPartProps>
        <solid>true</solid>
        <partEfficiencyOffset>0.5</partEfficiencyOffset>
    </addedPartProps>
    <comps>
        <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_Rechargeable">
            <maxCharge>100</maxCharge>
            <chargeDecayPerTick>0.00167</chargeDecayPerTick>
            <depletedConsequences>
                <!-- Flat stat penalties while IsDepleted == true -->
                <statOffsets>
                    <MeleeDPS>-5</MeleeDPS>
                    <WorkSpeedGlobal>-0.2</WorkSpeedGlobal>
                </statOffsets>
                <!-- Multiplicative penalty on global stats while IsDepleted == true -->
                <statFactors>
                    <MeleeHitChance>0.7</MeleeHitChance>
                </statFactors>
            </depletedConsequences>
        </li>
    </comps>
</HediffDef>
```

---

### Rechargeable artificial organ - part efficiency penalty

A heart that reduces pumping capacity when its charge is exhausted.

```xml
<HediffDef>
    <defName>MyMod_PoweredHeart</defName>
    <label>powered artificial heart</label>
    <description>An artificial heart that requires regular recharging to maintain full cardiac output.</description>
    <hediffClass>HediffWithComps</hediffClass>
    <isBad>false</isBad>
    <addedPartProps>
        <solid>true</solid>
        <partEfficiencyOffset>0.3</partEfficiencyOffset>
    </addedPartProps>
    <comps>
        <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_Rechargeable">
            <maxCharge>100</maxCharge>
            <chargeDecayPerTick>0.00083</chargeDecayPerTick>
            <!-- ~120,000 ticks (~2 days) to deplete from full -->
            <depletedConsequences>
                <!-- Reduces the Heart's part efficiency by 60 % when depleted.
                     Only affects the body part this hediff is anchored to. -->
                <partEfficiencyOffset>-0.6</partEfficiencyOffset>
            </depletedConsequences>
        </li>
    </comps>
</HediffDef>
```

> `partEfficiencyOffset` is applied only to the body part this hediff is installed on (matched by `hediff.Part`). A heart efficiency penalty will reduce `BloodPumping`; a spine penalty will reduce `Moving`.

---

### Charge station - open (accepts all hediffs)

A general-purpose charger that services any rechargeable hediff. `chargeRate` is omitted, so it defaults to `PowerConsumption / 60000`.

```xml
<ThingDef ParentName="BuildingBase">
    <defName>MyMod_HediffCharger</defName>
    <label>hediff charger</label>
    <description>A powered station that recharges any powered bionics or implants.</description>
    <graphicData>
        <texPath>Things/Building/MyMod_HediffCharger</texPath>
        <graphicClass>Graphic_Single</graphicClass>
    </graphicData>
    <size>(2,2)</size>

    <!-- Required: interaction cell for the charging pawn to stand at -->
    <hasInteractionCell>true</hasInteractionCell>
    <interactionCellOffset>(0,0,-1)</interactionCellOffset>

    <statBases>
        <MaxHitPoints>200</MaxHitPoints>
        <WorkToBuild>3000</WorkToBuild>
        <Flammability>0.5</Flammability>
    </statBases>

    <comps>
        <!-- Required: the station must be a power consumer -->
        <li Class="CompProperties_Power">
            <compClass>CompPowerTrader</compClass>
            <basePowerConsumption>200</basePowerConsumption>
        </li>
        <li Class="CompProperties_Flickable" />
    </comps>

    <modExtensions>
        <!-- chargeRate defaults to 200 / 60000 ≈ 0.00333 per tick -->
        <!-- supportedHediffs omitted → accepts all rechargeable hediffs -->
        <li Class="Chargeable_Hediffs_Framework.ChargeStationExtension" />
    </modExtensions>
</ThingDef>
```

---

### Charge station - hediff whitelist

A specialized charger that only services specific hediffs, with an explicit charge rate.

```xml
<ThingDef ParentName="BuildingBase">
    <defName>MyMod_ArmCharger</defName>
    <label>arm charger</label>
    <description>A high-powered charger tuned for powered bionic arms only.</description>
    <hasInteractionCell>true</hasInteractionCell>
    <interactionCellOffset>(0,0,-1)</interactionCellOffset>

    <comps>
        <li Class="CompProperties_Power">
            <compClass>CompPowerTrader</compClass>
            <basePowerConsumption>400</basePowerConsumption>
        </li>
    </comps>

    <modExtensions>
        <li Class="Chargeable_Hediffs_Framework.ChargeStationExtension">
            <!-- Explicit rate: charges 0.01 units per tick regardless of power draw -->
            <chargeRate>0.01</chargeRate>
            <!-- Only services these two hediffs; others are ignored -->
            <supportedHediffs>
                <li>MyMod_BionicArm_Rechargeable</li>
                <li>MyMod_BionicArm_Advanced</li>
            </supportedHediffs>
        </li>
    </modExtensions>
</ThingDef>
```

---

## Public API

All public types live in the `Chargeable_Hediffs_Framework` namespace. Reference the compiled `Chargeable Hediffs Framework.dll` in your project if you need C# integration.

### HediffComp_Rechargeable

```csharp
public class HediffComp_Rechargeable : HediffComp
```

Retrieve from any `HediffWithComps`:

```csharp
HediffComp_Rechargeable comp = myHediff.TryGetComp<HediffComp_Rechargeable>();
```

**Properties**

| Member          | Type                                | Description                                                                                               |
| --------------- | ----------------------------------- | --------------------------------------------------------------------------------------------------------- |
| `Props`         | `HediffCompProperties_Rechargeable` | Typed access to the comp's XML properties.                                                                |
| `MaxCharge`     | `float`                             | The configured `maxCharge` value.                                                                         |
| `CurrentCharge` | `float`                             | Charge currently stored.                                                                                  |
| `ChargePercent` | `float`                             | `CurrentCharge / MaxCharge`, clamped 0–1.                                                                 |
| `IsDepleted`    | `bool`                              | `true` when `CurrentCharge <= 0`. Depleted consequences are active when this is `true`.                   |
| `NeedsCharge`   | `bool`                              | `true` when `CurrentCharge < MaxCharge`.                                                                  |
| `CanDecayNow`   | `bool`                              | `true` when the pawn is alive and either spawned on a map or in a caravan. Decay is suppressed otherwise. |

**Methods**

| Method                      | Description                                                                                                                                         |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AddCharge(float amount)`   | Adds charge, clamped to `[0, MaxCharge]`. Notifies the pawn's health system if depletion state changes.                                             |
| `DrainCharge(float amount)` | Removes charge, clamped to `[0, MaxCharge]`. Notifies the pawn's health system if depletion state changes.                                          |
| `SetCharge(float amount)`   | Sets charge to an absolute value, clamped to `[0, MaxCharge]`. Notifies on state change.                                                            |
| `GetChargeInspectString()`  | Returns a formatted multi-line string with current/max charge, decay rate, depletion status, and active consequences. Used by `CompTipStringExtra`. |

**Displayed strings (Health tab)**

| Hook                       | Output example                                                |
| -------------------------- | ------------------------------------------------------------- |
| `CompLabelInBracketsExtra` | `74%` - shown in the hediff list label as `Bionic Arm (74%)`. |
| `CompTipStringExtra`       | Charge: 74.0 / 100.0 (74%)\nDecay rate: 100.2 / day           |

---

### HediffChargeUtility

```csharp
public static class HediffChargeUtility
```

A static utility with no shared state that mutates between calls. Safe to call from any thread that holds a game simulation lock.

#### Constants

| Member                  | Value         | Description                                                                                       |
| ----------------------- | ------------- | ------------------------------------------------------------------------------------------------- |
| `AutoRechargeThreshold` | `0.2f` (20 %) | The lowest-charge threshold used by both `WorkGiver_RechargeHediffs` and `Alert_LowHediffCharge`. |

#### Station cache

| Method / Property                   | Description                                                                                                                                               |
| ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BuildStationCache()`               | Pre-warms the `ThingDef → ChargeStationExtension` cache. Called automatically on first use; call explicitly from a startup hook for eager initialization. |
| `GetStationExtension(ThingDef def)` | Returns the `ChargeStationExtension` for the given def, or `null` if not registered. Triggers cache build on first call.                                  |
| `StationDefs`                       | `IReadOnlyList<ThingDef>` - all defs with a `ChargeStationExtension`. Allocation-free iteration.                                                          |

#### Eligibility helpers

| Method                                                          | Description                                                                                                                         |
| --------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `IsPawnEligible(Pawn pawn)`                                     | Basic null / dead / hediff-set guard. Returns `false` when pawn cannot be queried.                                                  |
| `IsAutoRechargeEligible(Pawn pawn)`                             | Returns `true` for player-owned colonists, slaves, and mechanoids. Returns `false` for prisoners, animals, enemies, and dead pawns. |
| `IsChargeStation(Thing thing)`                                  | Returns `true` when the thing has a `ChargeStationExtension`. Does not check power state.                                           |
| `IsStationPowered(Thing thing)`                                 | Returns `true` when the thing's `CompPowerTrader` reports power is on.                                                              |
| `IsValidStation(Thing station)`                                 | Returns `true` when the station is both a registered charge station and currently powered.                                          |
| `CanStationCharge(Thing station, HediffComp_Rechargeable comp)` | Returns `true` when the station's whitelist (if any) includes the comp's `HediffDef`. Does not check power.                         |
| `GetChargeRatePerTick(Thing station)`                           | Returns the station's effective charge rate per tick (explicit `chargeRate` or `PowerConsumption / 60000`).                         |

#### Pawn query helpers

| Method                                                                    | Description                                                                                                                                      |
| ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| `GetRechargeableComps(Pawn pawn, List<HediffComp_Rechargeable> outComps)` | Fills `outComps` with all rechargeable comps on the pawn. Returns the count. Zero-allocation when you supply a reusable list.                    |
| `GetRechargeableComps(Pawn pawn)`                                         | Allocating overload returning `IEnumerable<HediffComp_Rechargeable>`. Use for one-off checks or UI code.                                         |
| `HasRechargeableHediffs(Pawn pawn)`                                       | Returns `true` if the pawn has at least one rechargeable hediff.                                                                                 |
| `TryGetLowestChargePercent(Pawn pawn, out float percent)`                 | Sets `percent` to the lowest `ChargePercent` across all rechargeable hediffs. Returns `false` when the pawn has none.                            |
| `NeedsRecharge(Pawn pawn, float threshold = 0.2f)`                        | Returns `true` if the pawn's lowest charge percent is below `threshold`.                                                                         |
| `HasChargeableNeedingCharge(Pawn pawn, Thing station)`                    | Returns `true` if the pawn has at least one rechargeable hediff the station supports that still needs charge. Does not check powered state.      |
| `HasChargeableByStation(Pawn pawn, Thing station)`                        | Returns `true` if the pawn has at least one rechargeable hediff the station supports (regardless of current charge level). Checks powered state. |
| `ChargeAllFromStation(Pawn pawn, Thing station, int delta)`               | Charges every eligible hediff on the pawn for `delta` ticks at the station's rate. Returns count of comps charged.                               |
| `AggregateChargePercent(Pawn pawn, Thing station = null)`                 | Mean charge percent across rechargeable hediffs. Pass `null` for `station` to include all hediffs regardless of station support.                 |

**Usage example (C#):**

```csharp
// Check if a pawn needs to recharge
if (HediffChargeUtility.NeedsRecharge(pawn))
    Log.Message($"{pawn.LabelShort} needs recharging.");

// Manually charge all hediffs from a station (e.g. in a custom job driver tick):
int charged = HediffChargeUtility.ChargeAllFromStation(pawn, chargerBuilding, delta);
if (charged == 0)
    EndJobWith(JobCondition.Succeeded);

// Iterate comps without allocating:
var tempList = new List<HediffComp_Rechargeable>();
HediffChargeUtility.GetRechargeableComps(pawn, tempList);
foreach (var comp in tempList)
    comp.DrainCharge(10f);
```

---

## Compatibility Notes

### No required base classes

The `HediffComp_Rechargeable` comp can be added to **any** `HediffWithComps` hediff. Your `hediffClass` can remain `HediffWithComps` (the default) or any subclass. You do not need to extend a special base class.

```xml
<!-- Both of these work -->
<hediffClass>HediffWithComps</hediffClass>
<hediffClass>MyMod.MySpecialHediff</hediffClass>
```

### No required building class

The `ChargeStationExtension` can be added to **any** `ThingDef` that has `hasInteractionCell = true` and `CompPowerTrader`. Your building class can remain `Building` or any subclass.

### Harmony patch scope

The framework applies one narrow Harmony postfix patch:

| Patch target                                  | Purpose                                                                                                                     |
| --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `PawnCapacityUtility.CalculatePartEfficiency` | Reads the pawn's `CompChargeConsequencesCache` for a cached part efficiency offset and adds it when the hediff is depleted. |

Stat offsets and stat factors are applied through vanilla `ThingComp.GetStatOffset` and `ThingComp.GetStatFactor`, which `StatWorker.GetValueUnfinalized` already calls on every pawn comp - no Harmony patch is needed for stats.

The patch is **read-only** (modifies the return value only) and does a single dictionary lookup per queried body part with no allocations.

No other vanilla methods are patched.

### Race def injection

Every pawn race `ThingDef` automatically receives a `CompChargeConsequencesCache` comp at startup. You do not need to add it manually to your pawn defs. The injection guards against duplicates and is safe across hot-reloads.

### Zero stat factors

`depletedConsequences.statFactors` values must **not** be `0`. The cache stores a running product per stat and divides the factor back out on unregister; a zero factor cannot be divided out. Use a very small nonzero value (e.g. `0.001`) if you intend a near-total penalty. A config error is logged at startup if a zero factor is detected.

### Single resource type

The current version supports **electricity only**. There is no multi-resource or multi-type abstraction. All charge values are dimensionless floats on a modder-defined scale.

### Caravan and off-map behavior

Decay runs while `CanDecayNow` is true:

- Pawn is **alive** AND
- Pawn is **spawned on a map** OR **in a caravan** (`pawn.GetCaravan() != null`).

Decay is **suspended** when the pawn is dead, is in a cryptosleep casket (no caravan), or is otherwise off-map in a context that is not a caravan. Transport pods, orbital trade ships, and generated-world contexts do not count as caravans.
