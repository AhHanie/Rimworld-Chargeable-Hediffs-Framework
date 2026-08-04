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
  - [HediffCompProperties_ChargeBattery fields](#hediffcompproperties_chargebattery-fields)
  - [ChargeConsequenceProperties fields](#chargeconsequenceproperties-fields)
  - [Understanding units](#understanding-units)
- [Charge Station XML Reference](#charge-station-xml-reference)
  - [ChargeStationExtension fields](#chargestationextension-fields)
  - [Station requirements](#station-requirements)
- [Wireless Charge Component XML Reference](#wireless-charge-component-xml-reference)
  - [CompProperties_WirelessCharge fields](#compproperties_wirelesscharge-fields)
  - [Wireless charger requirements](#wireless-charger-requirements)
- [Animal Self-Charging](#animal-self-charging)
- [Ghoul Recharging](#ghoul-recharging)
- [XML Examples](#xml-examples)
  - [Rechargeable bionic limb - no consequences](#rechargeable-bionic-limb--no-consequences)
  - [Rechargeable bionic limb - depleted stat penalties](#rechargeable-bionic-limb--depleted-stat-penalties)
  - [Rechargeable artificial organ - part efficiency penalty](#rechargeable-artificial-organ--part-efficiency-penalty)
  - [Charge station - open (accepts all hediffs)](#charge-station--open-accepts-all-hediffs)
  - [Charge station - hediff whitelist](#charge-station--hediff-whitelist)
  - [Wireless charger - standalone building](#wireless-charger--standalone-building)
  - [Wireless charging - patching an existing powered building](#wireless-charging--patching-an-existing-powered-building)
  - [Rechargeable battery implant](#rechargeable-battery-implant)
  - [Battery with target whitelist/blacklist](#battery-with-target-whitelistblacklist)
- [Public API](#public-api)
  - [HediffComp_Rechargeable](#hediffcomp_rechargeable)
  - [HediffComp_ChargeBattery](#hediffcomp_chargebattery)
  - [HediffChargeUtility](#hediffchargeutility)
  - [CompWirelessCharge](#compwirelesscharge)
- [Compatibility Notes](#compatibility-notes)

---

## Overview

Chargeable Hediffs Framework adds a reusable `HediffComp` that gives any hediff an independent electrical charge store. Pawns can recharge at any powered building you tag with a `DefModExtension`. Everything is opt-in and purely XML-driven - no required C# base classes, no new Defs to extend.

**What the framework provides:**

- `HediffComp_Rechargeable` - tracks current/max charge, decays over time, stores active depleted consequences in a pawn cache comp; stats use vanilla `ThingComp` stat hooks, part efficiency uses one targeted capacity patch.
- `HediffComp_ChargeBattery` - turns a rechargeable hediff into a pawn-carried battery that drains itself to charge other rechargeable hediffs on the same pawn, with configurable rate, efficiency, and target filtering.
- `ChargeStationExtension` - marks any powered `ThingDef` as a charge station.
- Automatic work assignment - colonists/slaves/player mechs seek charge stations when any eligible hediff falls below 20 %.
- Right-click manual recharge - select a pawn, right-click a powered station, choose _Recharge_. Works for colonists, slaves, colony mechs, colony animals, and player-owned ghouls.
- Low-charge alert - grouped alert when any eligible pawn's lowest hediff charge falls below the threshold.
- Passive wireless charging - `CompWirelessCharge` scans a radius around a powered building at an interval and charges every eligible pawn found there, with no job or interaction cell required.
- Animal self-charging - a colony animal with the Odyssey `SentienceCatalyst` hediff or the framework's `CHF_SelfCharge` training autonomously seeks out a job-based charge station on its own, the same way a colonist would.
- Ghoul self-charging - a player-owned Anomaly ghoul autonomously seeks out a job-based charge station on its own, without needing any training or authorization. See [Ghoul Recharging](#ghoul-recharging).

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

**4. (Optional) Make the building charge wirelessly instead of - or in addition to - job-based charging:**

```xml
<comps>
    <li Class="Chargeable_Hediffs_Framework.CompProperties_WirelessCharge">
        <radius>8.9</radius>
        <scanIntervalTicks>250</scanIntervalTicks>
    </li>
</comps>
```

A wireless charger does **not** need `hasInteractionCell`. It still needs `CompPowerTrader` and `ChargeStationExtension`. See [Wireless Charge Component XML Reference](#wireless-charge-component-xml-reference).

**5. (Optional) Make a hediff act as a pawn-carried battery for other rechargeable hediffs:**

```xml
<comps>
    <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_Rechargeable">
        <maxCharge>500</maxCharge>
    </li>
    <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_ChargeBattery">
        <chargeRate>0.01</chargeRate>
        <transferEfficiency>0.75</transferEfficiency>
    </li>
</comps>
```

The battery comp requires `HediffCompProperties_Rechargeable` on the same hediff. It drains its own charge and transfers it into the pawn's other rechargeable hediffs every tick, regardless of the pawn's spawned/awake/faction/caravan state. See [HediffCompProperties_ChargeBattery fields](#hediffcompproperties_chargebattery-fields).

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

### HediffCompProperties_ChargeBattery fields

`HediffCompProperties_ChargeBattery` turns a rechargeable hediff into a pawn-carried battery. It **requires** the same hediff to also have `HediffCompProperties_Rechargeable` - the battery drains that comp's store and transfers charge into the pawn's other rechargeable hediffs. A config error is logged if the rechargeable comp is missing.

| Field                | Type              | Default | Description                                                                                                                              |
| -------------------- | ----------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `chargeRate`         | `float`           | `0`     | Maximum charge drained from the battery's own store per tick, **before** efficiency is applied. Must be positive.                        |
| `transferEfficiency` | `float`           | `1`     | Fraction of drained source charge delivered to the target. `0.75` means draining `1` from the battery adds `0.75` to the target. Must be `> 0` and `<= 1`. |
| `supportedHediffs`   | `List<HediffDef>` | `null`  | Optional whitelist of target `HediffDef`s. Empty or `null` means any non-battery rechargeable hediff on the pawn is a valid target.       |
| `excludedHediffs`    | `List<HediffDef>` | `null`  | Optional blacklist of target `HediffDef`s. Takes priority over `supportedHediffs`.                                                        |
| `showInspectString`  | `bool`            | `true`  | Adds battery transfer rate, efficiency, and target info to the hediff's tooltip.                                                          |

The battery uses **source-drain semantics**: `chargeRate` describes how much is drained from the battery, not how much the target gains.

```
source drain (per tick) = up to chargeRate
target gain             = source drain * transferEfficiency
```

**Runtime behavior:**

- Runs every tick the hediff ticks - it does **not** require the pawn to be spawned, awake, in the player faction, or in a caravan. This is an internal hediff-to-hediff transfer, not a station interaction.
- Always targets the lowest-`ChargePercent` eligible rechargeable hediff on the pawn first. Ties break on the larger missing absolute charge, then on hediff list order.
- Can drain itself all the way to `0`. There is no reserve.
- **Battery hediffs never charge other battery hediffs**, even if XML filters would otherwise allow it - any hediff with a `HediffComp_ChargeBattery` is skipped unconditionally as a target.
- Charge stations and wireless chargers can still recharge a battery hediff normally - the battery comp only changes how the hediff *spends* charge, not how it *receives* it.

**Example with all fields:**

```xml
<li Class="Chargeable_Hediffs_Framework.HediffCompProperties_ChargeBattery">
    <chargeRate>0.01</chargeRate>
    <transferEfficiency>0.75</transferEfficiency>
    <supportedHediffs>
        <li>MyMod_BionicArm_Rechargeable</li>
        <li>MyMod_BionicLeg_Rechargeable</li>
    </supportedHediffs>
    <excludedHediffs>
        <li>MyMod_EmergencyImplant_DoNotAutoCharge</li>
    </excludedHediffs>
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

`maxCharge`, `startingCharge`, `chargeDecayPerTick`, `chargeRate` (on the station), and `chargeRate` (on `HediffCompProperties_ChargeBattery`) all use the **same arbitrary unit**. You choose the scale; the framework does not enforce one. The only constraint is internal consistency between decay and recharge.

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

**Battery `chargeRate` is a source-drain rate, not a target-gain rate:**

```
battery chargeRate    = 0.02
transferEfficiency    = 0.75
source drain per day  = 0.02 * 60000 = 1200
target gain per day   = 1200 * 0.75 = 900
```

---

## Charge Station XML Reference

`ChargeStationExtension` is shared by both charging modes described in this guide: job-based, interaction-cell stations (Quick Start steps 1-3) and passive wireless chargers (Quick Start step 4, and see [Wireless Charge Component XML Reference](#wireless-charge-component-xml-reference)). It always controls charge rate and hediff filtering, regardless of which mode delivers the charge.

### ChargeStationExtension fields

| Field              | Type              | Default | Description                                                                                                                                                 |
| ------------------ | ----------------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `chargeRate`       | `float`           | `0`     | Charge added per tick to each eligible hediff while a pawn occupies this station or is in range of a wireless charger. When `0`, falls back to `PowerConsumption / 60000`. Must not be negative. |
| `supportedHediffs` | `List<HediffDef>` | `null`  | Whitelist of `HediffDef` names this station can service. Empty or `null` means **all rechargeable hediffs** are accepted.                                   |

### Station requirements

The framework validates these at game load and logs config errors if they are missing. Requirements differ by charging mode:

**Both modes require:**

1. A **`CompPowerTrader`** comp (`CompProperties_Power`).
2. A **`ChargeStationExtension`** mod extension.
3. `chargeRate` must be `>= 0` (zero uses the power-consumption fallback).
4. All entries in `supportedHediffs` must resolve to valid `HediffDef`s.

**Interaction-cell (job-based) stations additionally require:**

- `hasInteractionCell = true`, so a pawn has somewhere to stand while working the recharge job.

**Wireless chargers do not need `hasInteractionCell`** - see [Wireless charger requirements](#wireless-charger-requirements) for the additional fields `CompProperties_WirelessCharge` validates.

The building does **not** need to extend any special class. Any `ThingDef` that meets the requirements above works as a charge station.

---

## Wireless Charge Component XML Reference

`CompProperties_WirelessCharge` adds passive, jobless charging to any powered building. Instead of a pawn walking to an interaction cell and running a job, the building itself scans a radius around it on an interval and charges every eligible pawn found there directly.

### CompProperties_WirelessCharge fields

| Field                | Type    | Default | Description                                                                                                   |
| -------------------- | ------- | ------- | --------------------------------------------------------------------------------------------------------------- |
| `radius`             | `float` | `6.9`   | Radius, in cells, scanned around the building. Must be positive and below `GenRadial.MaxRadialPatternRadius`. |
| `scanIntervalTicks`  | `int`   | `250`   | How often the building scans for pawns to charge, in ticks. `250` ticks is 1 in-game hour. Must be positive.  |
| `drawRadius`         | `bool`  | `true`  | Draws a radius ring around the building while it is selected.                                                 |
| `showInspectString`  | `bool`  | `true`  | Adds radius, interval, and last-scan-result lines to the building's inspect string.                           |

### Wireless charger requirements

In addition to the shared [station requirements](#station-requirements) (`CompPowerTrader` and `ChargeStationExtension`), the framework validates:

- `radius > 0` and `radius < GenRadial.MaxRadialPatternRadius`.
- `scanIntervalTicks > 0`.

`hasInteractionCell` is **not** required - wireless charging does not use pathing or jobs.

To show the radius ring while placing the blueprint (not just while selected), add the place worker:

```xml
<placeWorkers>
    <li>Chargeable_Hediffs_Framework.PlaceWorker_ShowWirelessChargeRadius</li>
</placeWorkers>
```

The selected-state radius ring is drawn automatically by the comp and does not need this place worker.

---

## Animal Self-Charging

Colony animals do not automatically use job-based charge stations the way colonists, slaves, and colony mechs do. An animal must first be **authorized**, through either of two independent routes:

1. It has the Odyssey `HediffDefOf.SentienceCatalyst` hediff (checked only when `ModsConfig.OdysseyActive`).
2. It has learned the framework's `CHF_SelfCharge` `TrainableDef` - a normal Training-tab trainable (`Advanced` trainability, prerequisite `Obedience`, 3 steps). Merely queuing the training as "wanted" is not enough; it must be fully learned.

Once authorized, an animal behaves exactly like a colonist: when its lowest rechargeable hediff's charge falls below the normal 20 % (`HediffChargeUtility.AutoRechargeThreshold`), it reserves and walks to the closest powered, compatible, reachable station and charges via the same `CHF_RechargeHediffs` job. Station requirements, `supportedHediffs` filtering, and charge-rate math are all unchanged - see [Charge Station XML Reference](#charge-station-xml-reference).

**This only affects job-based stations.** Passive [wireless charging](#wireless-charge-component-xml-reference) is untouched: any eligible pawn (including an unauthorized animal) inside a wireless charger's radius is still charged automatically, because there is no job or seek-out behavior involved. The [low-charge alert](#compwirelesscharge) also still reports every eligible pawn below the threshold regardless of self-charge authorization, since the player may still need to act.

### How it's implemented

- `HediffChargeUtility.IsAnimalAuthorizedToSelfCharge(Pawn)` centralizes the authorization check described above. `HediffChargeUtility.IsEligibleForJobBasedRecharge(Pawn)` builds on it: non-animals keep the existing `IsAutoRechargeEligible` rule, while colony animals additionally require authorization. `WorkGiver_RechargeHediffs` and `FloatMenuOptionProvider_RechargeHediff` both use this helper, so an unauthorized animal never receives a manual right-click recharge order either.
- The autonomous behavior is injected via RimWorld's `Animal_PreWander` `ThinkTreeDef` insertion hook (see `Core/Defs/ThinkTreeDefs/Animal.xml`), not an XPath patch to `Animal.xml`. Both the core `Animal` and `Insect` think trees expose this hook.
- **If your race uses a completely custom main think tree** (i.e. it does not inherit from or subtree into the core `Animal`/`Insect` trees), it will not receive self-charging AI unless it also exposes an `Animal_PreWander`-tagged insertion point, or you ship a compatibility patch that adds the framework's `CHF_AnimalSelfCharge` subtree directly.
- `JobDriver_RechargeHediffs` fails an in-progress job if a colony animal loses its authorization mid-job (training decay, catalyst removal). This has no effect on humanlike pawns or mechs.

---

## Ghoul Recharging

A player-owned Anomaly ghoul with a rechargeable hediff recharges autonomously and can receive a manual right-click recharge order, the same way a colonist can - but ghouls reach this behavior differently than colonists, mechs, and animals do.

Ghouls have `AllWork` disabled and never run `JobGiver_Work`, so they cannot be assigned work by any `WorkGiver`, including `WorkGiver_RechargeHediffs`. Instead, the framework injects a low-priority think node directly into Anomaly's `Ghoul` think tree at its `Ghoul_PreWander` insertion hook (see `Data/Anomaly/Defs/ThinkTreeDefs/Ghoul.xml`), immediately before the tree falls back to wandering. Vanilla ghoul behavior with higher priority - downed/burning handling, mental states, immediate threat response, medical rest, food, drafted orders, and lord duty - all still runs first; self-charging only preempts idle wandering when a compatible powered station is actually needed.

Unlike animal self-charging, **ghouls need no training or authorization** - every player-owned ghoul is eligible as soon as it has a rechargeable hediff. They keep the same 20 % threshold (`HediffChargeUtility.AutoRechargeThreshold`) and the same powered/reachable/compatible station requirements as any other pawn. The manual right-click **Recharge at ...** order also works on a selected ghoul, both drafted and undrafted.

**This only affects job-based stations.** Passive [wireless charging](#wireless-charge-component-xml-reference) and the [low-charge alert](#compwirelesscharge) already cover eligible player ghouls through the ordinary `IsAutoRechargeEligible` route, with no ghoul-specific code involved.

### How it's implemented

- `CHF_GhoulSelfCharge`, a second `ThinkTreeDef` inserted at `Ghoul_PreWander`, uses the same `JobGiver_AutonomousRechargeHediffs` node used for authorized animals directly as its `thinkRoot` - it contains no ghoul-specific logic itself and needs no authorization-gating condition or tag, unlike the animal subtree. The `Ghoul_PreWander` hook is owned by Anomaly's `Ghoul` think tree alone, so this cannot affect any other subhuman mutant.
- Anomaly separately restricts which `FloatMenuOptionProvider`s a selected mutant can use, via `MutantDef.whitelistedFloatMenuProviders` (see `Data/Anomaly/Defs/Misc/Mutants.xml`); `Ghoul`'s list does not include this framework's provider by default. `FloatMenuOptionProvider_RechargeHediff` overrides `SelectedPawnValid` to return `true` for any real ghoul (`pawn.IsGhoul`) before falling back to the base whitelist check for every other pawn type, so only this framework's manual recharge option is exempted - no other Anomaly float-menu option is affected.
- `WorkGiver_RechargeHediffs` and ordinary work assignment are untouched - ghouls remain excluded from the work system entirely. This is self-maintenance behavior slotted before wandering, not a change to what work a ghoul can perform.

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

### Wireless charger - standalone building

A powered pylon that passively charges any rechargeable hediff on nearby colony pawns. No `hasInteractionCell`, no job, no pathing.

```xml
<ThingDef ParentName="BuildingBase">
    <defName>MyMod_WirelessCharger</defName>
    <label>wireless charge pylon</label>
    <description>A powered pylon that wirelessly recharges nearby colonists', slaves', animals', and mechs' powered bionics and implants.</description>
    <graphicData>
        <texPath>Things/Building/MyMod_WirelessCharger</texPath>
        <graphicClass>Graphic_Single</graphicClass>
    </graphicData>
    <size>(1,1)</size>

    <statBases>
        <MaxHitPoints>150</MaxHitPoints>
        <WorkToBuild>2000</WorkToBuild>
        <Flammability>0.5</Flammability>
    </statBases>

    <comps>
        <!-- Required: the charger must be a power consumer -->
        <li Class="CompProperties_Power">
            <compClass>CompPowerTrader</compClass>
            <basePowerConsumption>200</basePowerConsumption>
        </li>
        <li Class="CompProperties_Flickable" />
        <!-- Required: makes this building scan and charge wirelessly -->
        <li Class="Chargeable_Hediffs_Framework.CompProperties_WirelessCharge">
            <radius>8.9</radius>
            <scanIntervalTicks>250</scanIntervalTicks>
        </li>
    </comps>

    <modExtensions>
        <!-- chargeRate defaults to 200 / 60000 ≈ 0.00333 per tick per eligible hediff -->
        <li Class="Chargeable_Hediffs_Framework.ChargeStationExtension" />
    </modExtensions>

    <!-- Optional: shows the charge radius while placing the blueprint -->
    <placeWorkers>
        <li>Chargeable_Hediffs_Framework.PlaceWorker_ShowWirelessChargeRadius</li>
    </placeWorkers>
</ThingDef>
```

---

### Wireless charging - patching an existing powered building

Add wireless charging to a building your mod (or another mod) already defines, as long as it has `CompPowerTrader`. If the building doesn't have `ChargeStationExtension` yet, add both operations; if it already has one, only add the comp.

```xml
<Operation Class="PatchOperationAdd">
    <xpath>/Defs/ThingDef[defName="MyMod_WirelessCharger"]/comps</xpath>
    <value>
        <li Class="Chargeable_Hediffs_Framework.CompProperties_WirelessCharge">
            <radius>8.9</radius>
            <scanIntervalTicks>250</scanIntervalTicks>
        </li>
    </value>
</Operation>

<Operation Class="PatchOperationAddModExtension">
    <xpath>/Defs/ThingDef[defName="MyMod_WirelessCharger"]</xpath>
    <value>
        <li Class="Chargeable_Hediffs_Framework.ChargeStationExtension">
            <chargeRate>0.00333</chargeRate>
        </li>
    </value>
</Operation>
```

If a modder wants the fallback charge rate derived from power consumption, omit `chargeRate` or set it to `0`.

---

### Rechargeable battery implant

An implanted battery that stores charge and transfers it to other powered implants on the same pawn. Requires both comps on the same hediff.

```xml
<HediffDef>
    <defName>MyMod_InternalBattery</defName>
    <label>internal charge battery</label>
    <description>An implanted battery that stores charge and transfers it to other powered implants.</description>
    <hediffClass>HediffWithComps</hediffClass>
    <isBad>false</isBad>
    <comps>
        <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_Rechargeable">
            <maxCharge>500</maxCharge>
            <chargeDecayPerTick>0</chargeDecayPerTick>
        </li>
        <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_ChargeBattery">
            <chargeRate>0.01</chargeRate>
            <transferEfficiency>0.75</transferEfficiency>
        </li>
    </comps>
</HediffDef>
```

Because `supportedHediffs` and `excludedHediffs` are omitted, this battery charges any non-battery rechargeable hediff on the pawn, prioritizing whichever has the lowest charge percent. A charge station or wireless charger can still recharge the battery itself normally.

---

### Battery with target whitelist/blacklist

The same battery, restricted to specific target hediffs and with one explicit exception.

```xml
<HediffDef>
    <defName>MyMod_InternalBattery</defName>
    <label>internal charge battery</label>
    <description>An implanted battery that stores charge and transfers it to other powered implants.</description>
    <hediffClass>HediffWithComps</hediffClass>
    <isBad>false</isBad>
    <comps>
        <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_Rechargeable">
            <maxCharge>500</maxCharge>
            <chargeDecayPerTick>0</chargeDecayPerTick>
        </li>
        <li Class="Chargeable_Hediffs_Framework.HediffCompProperties_ChargeBattery">
            <chargeRate>0.01</chargeRate>
            <transferEfficiency>0.75</transferEfficiency>
            <supportedHediffs>
                <li>MyMod_BionicArm_Rechargeable</li>
                <li>MyMod_BionicLeg_Rechargeable</li>
            </supportedHediffs>
            <excludedHediffs>
                <li>MyMod_EmergencyImplant_DoNotAutoCharge</li>
            </excludedHediffs>
        </li>
    </comps>
</HediffDef>
```

`excludedHediffs` wins over `supportedHediffs` if a def somehow appears in both. Any hediff not in `supportedHediffs` is never charged by this battery, even if it would otherwise be a valid rechargeable target.

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

### HediffComp_ChargeBattery

```csharp
public class HediffComp_ChargeBattery : HediffComp
```

Retrieve from any `HediffWithComps` that has the comp:

```csharp
HediffComp_ChargeBattery battery = myHediff.TryGetComp<HediffComp_ChargeBattery>();
```

Requires a `HediffComp_Rechargeable` on the same hediff - the battery drains that comp's `CurrentCharge` and calls `AddCharge`/`DrainCharge` on the affected comps, so notifications and depletion bookkeeping stay correct.

**Properties**

| Member  | Type                                | Description                                  |
| ------- | ----------------------------------- | --------------------------------------------- |
| `Props` | `HediffCompProperties_ChargeBattery` | Typed access to the comp's XML properties.   |

**Runtime behavior**

- `CompPostTickInterval` computes a drain budget of `Min(source.CurrentCharge, chargeRate * delta)`, then repeatedly finds the lowest-`ChargePercent` eligible target and fills it (or spends the remaining budget on it), draining the source and adding to the target via the existing `AddCharge`/`DrainCharge` methods.
- Eligible targets exclude the source itself, any hediff with its own `HediffComp_ChargeBattery`, hediffs that don't `NeedsCharge`, and hediffs filtered out by `supportedHediffs`/`excludedHediffs`.
- Does not call `IsPawnEligible` or `IsAutoRechargeEligible` - it only checks that the pawn and hediff set are non-null. Dead pawns, prisoners, and off-map pawns are not excluded.
- `CompDebugString()` reports source charge, effective rate, efficiency, and the last transfer's stats (unsaved).

Station and wireless-charge APIs (`HediffChargeUtility.ChargeAllFromStation`, `IsAutoRechargeEligible`, etc.) are unchanged and treat battery hediffs as ordinary rechargeable hediffs - they can still recharge a battery's own store.

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
| `IsAutoRechargeEligible(Pawn pawn)`                             | Returns `true` for player-controlled colonists, slaves, colony animals, colony mechs, and player-owned ghouls. Returns `false` for prisoners, enemies, wild animals, and dead pawns. Used by `Alert_LowHediffCharge` and `CompWirelessCharge`. |
| `IsColonyAnimalCandidate(Pawn pawn)`                            | Returns `true` for an alive, non-prisoner colony animal. Basic guard used by the self-charge authorization helpers.                                                                |
| `IsAnimalAuthorizedToSelfCharge(Pawn pawn)`                     | Returns `true` for a colony animal that has the Odyssey `SentienceCatalyst` hediff, or has learned `CHF_SelfCharge`. See [Animal Self-Charging](#animal-self-charging).             |
| `IsEligibleForJobBasedRecharge(Pawn pawn)`                      | Job-based (`WorkGiver`/float-menu/autonomous AI) eligibility. Same as `IsAutoRechargeEligible` for non-animals - this already includes player-owned ghouls; colony animals additionally require `IsAnimalAuthorizedToSelfCharge`. Used by `WorkGiver_RechargeHediffs`, `FloatMenuOptionProvider_RechargeHediff`, and `JobGiver_AutonomousRechargeHediffs`. |
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

### CompWirelessCharge

```csharp
public class CompWirelessCharge : ThingComp
```

Retrieve from the parent building's comps:

```csharp
CompWirelessCharge comp = building.GetComp<CompWirelessCharge>();
```

Added to a `ThingDef` via `CompProperties_WirelessCharge`. See [Wireless Charge Component XML Reference](#wireless-charge-component-xml-reference) for the XML fields.

**Behavior**

- Caches the parent's `CompPowerTrader` on spawn. Does nothing while unpowered.
- Scans `parent.Map.mapPawns.AllPawnsSpawned` every `scanIntervalTicks`, using `Position.InHorDistOf` against `radius`. No per-tick work and no `GenRadial` cell walk.
- For every pawn in range that passes `HediffChargeUtility.IsAutoRechargeEligible`, calls `HediffChargeUtility.ChargeAllFromStation(pawn, parent, elapsed)`, where `elapsed` is the actual number of ticks since the last scan (not just `scanIntervalTicks`), so charge stays correct even if a scan is delayed.
- Does not reserve, path to, or otherwise interact with pawns - there is no job, so overlapping wireless chargers simply stack: every powered charger covering a pawn charges it independently on its own schedule.
- Draws its radius ring when the parent is selected (`PostDrawExtraSelectionOverlays`), gated by `drawRadius`.

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

The `ChargeStationExtension` can be added to **any** `ThingDef` that has a `CompPowerTrader`, and `hasInteractionCell = true` if it is used for job-based charging. Your building class can remain `Building` or any subclass.

### Wireless charging behavior

- **Overlaps stack.** Wireless chargers do not coordinate with each other. If a pawn is in range of two powered chargers, both charge it independently every scan, so charge gain roughly doubles.
- **Scans are interval-based, not per-tick.** Each `CompWirelessCharge` only scans for pawns every `scanIntervalTicks`, and charges for the actual elapsed ticks since the previous scan - there is no per-tick cost proportional to map pawn count.
- **Eligibility matches `IsAutoRechargeEligible`.** Wireless charging and the low-charge alert affect player-controlled colonists, slaves, colony animals, colony mechs, and player-owned ghouls. They do **not** affect prisoners, neutral guests, hostile pawns, or wild/hostile animals and ghouls. The job-based auto-recharge behavior additionally requires colony animals to be self-charge authorized (see [Animal Self-Charging](#animal-self-charging)) and reaches ghouls through a separate think-tree injection rather than the work system (see [Ghoul Recharging](#ghoul-recharging)).

### Harmony patch scope

The framework applies two narrow Harmony postfix patches:

| Patch target                                  | Purpose                                                                                                                     |
| --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `PawnCapacityUtility.CalculatePartEfficiency` | Reads the pawn's `CompChargeConsequencesCache` for a cached part efficiency offset and adds it when the hediff is depleted. |
| `Pawn.GetGizmos`                              | Dev-mode only: appends `Pawn_HealthTracker.GetGizmos()` (every `HediffComp`'s gizmos, including this framework's debug charge tools) for colony animals and player-owned ghouls. Vanilla only calls it for `IsColonistPlayerControlled`, `IsColonyMech`, or `IsPrisonerOfColony` pawns, so without this patch a rechargeable hediff's debug gizmos are unreachable on an animal or ghoul even with dev mode on. |

Stat offsets and stat factors are applied through vanilla `ThingComp.GetStatOffset` and `ThingComp.GetStatFactor`, which `StatWorker.GetValueUnfinalized` already calls on every pawn comp - no Harmony patch is needed for stats.

The `CalculatePartEfficiency` patch is **read-only** (modifies the return value only) and does a single dictionary lookup per queried body part with no allocations. The `Pawn.GetGizmos` patch only does any work when `DebugSettings.ShowDevGizmos` is true, and only for colony animals and player-owned ghouls - it is a no-op in normal play for every other pawn type.

No other vanilla methods are patched.

### Race def injection

Every pawn race `ThingDef` automatically receives a `CompChargeConsequencesCache` comp at startup. You do not need to add it manually to your pawn defs. The injection guards against duplicates and is safe across hot-reloads.

### Zero stat factors

`depletedConsequences.statFactors` values must **not** be `0`. The cache stores a running product per stat and divides the factor back out on unregister; a zero factor cannot be divided out. Use a very small nonzero value (e.g. `0.001`) if you intend a near-total penalty. A config error is logged at startup if a zero factor is detected.

### Battery-to-battery transfer

`HediffComp_ChargeBattery` never charges another battery hediff. This is enforced by checking for the **presence of the `HediffComp_ChargeBattery` comp** on a candidate target's hediff, not by hediff def name or naming convention - so it is safe even if a target's `defName` doesn't hint that it's a battery. No Harmony patch is required for this behavior or for battery transfer in general; it runs entirely through `CompPostTickInterval`.

### Single resource type

The current version supports **electricity only**. There is no multi-resource or multi-type abstraction. All charge values are dimensionless floats on a modder-defined scale.

### Caravan and off-map behavior

Decay runs while `CanDecayNow` is true:

- Pawn is **alive** AND
- Pawn is **spawned on a map** OR **in a caravan** (`pawn.GetCaravan() != null`).

Decay is **suspended** when the pawn is dead, is in a cryptosleep casket (no caravan), or is otherwise off-map in a context that is not a caravan. Transport pods, orbital trade ships, and generated-world contexts do not count as caravans.
