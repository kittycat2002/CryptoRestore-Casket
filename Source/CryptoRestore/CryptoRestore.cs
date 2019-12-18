using RimWorld;
using System;
using System.Linq;
using Verse;
using System.Collections.Generic;
using UnityEngine;

namespace CryptoRestore
{
    public class CryptoRestoreSettings : ModSettings
    {
        public static bool luciAddiction = true;
        public static int unageRate = 30;
        public static int fuelRate = 20;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref luciAddiction, "LuciAddiction", true);
            Scribe_Values.Look(ref unageRate, "UnageRate", 30);
            Scribe_Values.Look(ref fuelRate, "fuelRate", 20);
            base.ExposeData();
        }
        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("Luciferium Addiction", ref luciAddiction, "Toggles Luciferium Addiction from use.");
            listingStandard.Label("Unaging Rate: " + unageRate, -1, "How quickly the Pawn unages when inside the casket.");
            unageRate = (int)Math.Round(listingStandard.Slider(unageRate, 10, 100));
            listingStandard.Label("Fuel Consumption Rate: " + fuelRate, -1, "How much fuel the casket uses per year.");
            fuelRate = (int)Math.Round(listingStandard.Slider(fuelRate, 1, 60));
            listingStandard.End();
        }
    }

    public class CryptoRestoreMod : Mod
    {
        CryptoRestoreSettings settings;

        public CryptoRestoreMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<CryptoRestoreSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            CryptoRestoreSettings.DoSettingsWindowContents(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "CryptoRestore Caskets";
        }
    }
    public class Building_CryptoRestore : Building_CryptosleepCasket
    {
        int cryptoHediffCooldown;
        readonly int cryptoHediffCooldownBase = GenDate.TicksPerQuadrum / 2;
        int enterTime;
        readonly HediffDef luciAddiDef = HediffDef.Named("LuciferiumAddiction");
        readonly HediffDef luciDef = HediffDef.Named("LuciferiumHigh");
        readonly NeedDef luciNeed = DefDatabase<NeedDef>.GetNamed("Chemical_Luciferium", true);
        CompRefuelable refuelable;
        CompPowerTrader power;
        CompProperties_Power props;
        CompProperties_Refuelable fuelprops;


        public int AgeHediffs(Pawn pawn)
        {
            if (pawn != null)
            {
                bool hasCataracts = false;
                bool hasHearingLoss = false;
                int hediffs = 0;
                foreach (Hediff injury in pawn.health.hediffSet.GetHediffs<Hediff>().ToList())
                {
                    string injuryName = injury.def.label;
                    if (injuryName == "cataract" && !hasCataracts)
                    {
                        hediffs += 1;
                        hasCataracts = true;
                    }
                    else if (injuryName == "hearing loss" && !hasHearingLoss)
                    {
                        hediffs += 1;
                        hasHearingLoss = true;
                    }
                    else if (injuryName == "bad back" || injuryName == "frail" || injuryName == "dementia" || injuryName == "alzheimer's")
                        hediffs += 1;
                }
                return hediffs;
            }
            return 0;
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            refuelable = GetComp<CompRefuelable>();
            power = GetComp<CompPowerTrader>();
            props = power.Props;
            fuelprops = refuelable.Props;
            fuelprops.fuelConsumptionRate = CryptoRestoreSettings.fuelRate / 60f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cryptoHediffCooldown, "cryptoHediffCooldown");
            Scribe_Values.Look(ref enterTime, "enterTime");
        }
        public override void Tick()
        {
            fuelprops.fuelConsumptionRate = CryptoRestoreSettings.fuelRate / 60f;
            if (HasAnyContents && refuelable.HasFuel)
            {
                Pawn pawn = ContainedThing as Pawn;
                bool hasHediffs = AgeHediffs(pawn) > 0;
                if (hasHediffs || pawn.ageTracker.AgeBiologicalTicks > GenDate.TicksPerYear * 21)
                {
                    power.PowerOutput = -props.basePowerConsumption;
                    if (power.PowerOn)
                    {
                        refuelable.ConsumeFuel(CryptoRestoreSettings.fuelRate / GenDate.TicksPerYear);
                        cryptoHediffCooldown = Math.Max(cryptoHediffCooldown - 1, 0);
                        if (pawn.ageTracker.AgeBiologicalTicks > GenDate.TicksPerYear * 21)
                            pawn.ageTracker.AgeBiologicalTicks = Math.Max(pawn.ageTracker.AgeBiologicalTicks - CryptoRestoreSettings.unageRate, GenDate.TicksPerYear * 21);
                        if (hasHediffs && cryptoHediffCooldown == 0)
                        {
                            foreach (Hediff oldHediff in pawn.health.hediffSet.GetHediffs<Hediff>().ToList())
                            {
                                string hediffName = oldHediff.def.label;
                                if (hediffName == "bad back" && pawn.ageTracker.AgeBiologicalYears < 39)
                                {
                                    pawn.health.RemoveHediff(oldHediff);
                                    cryptoHediffCooldown = cryptoHediffCooldownBase;
                                    break;
                                }
                                else if (hediffName == "frail" && pawn.ageTracker.AgeBiologicalYears < 48)
                                {
                                    pawn.health.RemoveHediff(oldHediff);
                                    cryptoHediffCooldown = cryptoHediffCooldownBase;
                                    break;
                                }
                                else if (hediffName == "cataract" && pawn.ageTracker.AgeBiologicalYears < 52)
                                {
                                    foreach (Hediff cataractHediff in pawn.health.hediffSet.GetHediffs<Hediff>().ToList())
                                    {
                                        if (cataractHediff.def.label == "cataract")
                                        {
                                            pawn.health.RemoveHediff(cataractHediff);
                                        }
                                    }
                                    cryptoHediffCooldown = cryptoHediffCooldownBase;
                                    break;
                                }
                                else if (hediffName == "hearing loss" && pawn.ageTracker.AgeBiologicalYears < 52)
                                {
                                    foreach (Hediff hearingHediff in pawn.health.hediffSet.GetHediffs<Hediff>().ToList())
                                    {
                                        if (hearingHediff.def.label.ToString() == "hearing loss")
                                        {
                                            pawn.health.RemoveHediff(hearingHediff);
                                        }
                                    }
                                    cryptoHediffCooldown = cryptoHediffCooldownBase;
                                    break;
                                }
                                else if (hediffName == "dementia" && pawn.ageTracker.AgeBiologicalYears < 66)
                                {
                                    pawn.health.RemoveHediff(oldHediff);
                                    cryptoHediffCooldown = cryptoHediffCooldownBase;
                                    break;
                                }
                                else if (hediffName == "alzheimer's" && pawn.ageTracker.AgeBiologicalYears < 72)
                                {
                                    oldHediff.Heal(1 / 7.5f);
                                    if (oldHediff.Severity > 0) cryptoHediffCooldown = GenDate.TicksPerDay;
                                    else
                                    {
                                        cryptoHediffCooldown = cryptoHediffCooldownBase;
                                        pawn.health.RemoveHediff(oldHediff);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                    power.PowerOutput = 0;
            }
            else
                power.PowerOutput = 0;
        }
        public override void EjectContents()
        {
            Pawn pawn = ContainedThing as Pawn;
            if (CryptoRestoreSettings.luciAddiction && pawn.ageTracker.AgeBiologicalTicks >= GenDate.TicksPerYear * 21)
            {
                pawn.health.AddHediff(luciDef);
                pawn.health.AddHediff(luciAddiDef);
                if (Find.TickManager.TicksGame - enterTime >= GenDate.TicksPerDay * 3)
                    pawn.needs.TryGetNeed(luciNeed).CurLevelPercentage = 1f;
            }
            power.PowerOutput = 0;
            base.EjectContents();
        }

        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (base.TryAcceptThing(thing, allowSpecialEffects))
            {
                cryptoHediffCooldown = cryptoHediffCooldownBase;
                enterTime = Find.TickManager.TicksGame;
                if (refuelable.HasFuel && (AgeHediffs(thing as Pawn) > 0 || (thing as Pawn).ageTracker.AgeBiologicalTicks > GenDate.TicksPerYear * 21))
                    power.PowerOutput = -props.basePowerConsumption;
                return true;
            }
            return false;
        }

        public override string GetInspectString()
        {
            if (HasAnyContents)
            {
                Pawn pawn = ContainedThing as Pawn;
                pawn.ageTracker.AgeBiologicalTicks.TicksToPeriod(out int years, out int quadrums, out int days, out float hours);
                ((long)Math.Ceiling((float)(pawn.ageTracker.AgeBiologicalTicks - GenDate.TicksPerYear * 21) / CryptoRestoreSettings.unageRate)).TicksToPeriod(out int years2, out int quadrums2, out int days2, out float hours2);
                string bioTime = TranslatorFormattedStringExtensions.Translate("AgeBiological",years,quadrums,days);
                return base.GetInspectString() + ", " + AgeHediffs(pawn).ToString() + " Age Hediffs\n" + bioTime + "\n" + "Time Remaining: " + years2 + (years2 == 1 ? " year " : " years ") + quadrums2 + (quadrums2 == 1 ? " quadrum " : " quadrums ") + days2 + (days2 == 1 ? " day " : " days ") + Math.Ceiling(hours2) + (Math.Ceiling(hours2) == 1 ? " hour" : " hours");
            }
            else return base.GetInspectString();
        }
    }
}