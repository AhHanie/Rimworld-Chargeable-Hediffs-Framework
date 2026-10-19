# Chargeable Hediffs Framework

A Rimworld framework mod that gives any hediff an independent electrical charge store. Pawns seek powered charge stations automatically when charge runs low, and every rechargeable hediff shows its battery level in the Health tab.

---

## For modders

Everything is XML-driven. Add one `HediffComp`, tag a powered building with one `DefModExtension`, and you're done. No required base classes.

See the Modder Guide ([1.6](1.6/Docs/ModderGuide.md), [1.7](1.7/Docs/ModderGuide.md)) for the full reference, or browse the Github Wikis.

## Supported versions

RimWorld 1.6 and 1.7. The two versions ship separate assemblies (`1.6/Assemblies`, `1.7/Assemblies`) and each carry their own Defs/ folder (keep the two copies in step, and keep the order of Defs/TrainableDefs/Trainables_ChargeableHediffs.xml unchanged); Languages and Textures are shared at the mod root. Harmony is required in both.

## Building

- 1.6: `1.6/source/Chargeable Hediffs Framework.csproj` (.NET Framework 4.7.2).
- 1.7: `dotnet build "1.7/source/Chargeable Hediffs Framework.csproj" -c Release` (.NET 10). Override the reference paths with `-p:RimWorldDir=...` and `-p:HarmonyDll=...`.

## License

See [LICENSE](LICENSE).
