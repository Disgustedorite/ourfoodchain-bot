﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurFoodChain.Gotchi {

    public class GotchiStatsCalculator {

        // Public members

        public GotchiTypeRegistry TypeRegistry { get; private set; }

        public GotchiStatsCalculator(GotchiTypeRegistry typeRegistry) {

            TypeRegistry = typeRegistry;

        }

        public async Task<GotchiStats> GetBaseStatsAsync(Gotchi gotchi) {

            GotchiStats result = new GotchiStats();

            int denominator = 0;

            GotchiType[] gotchiTypes = await TypeRegistry.GetTypesAsync(gotchi);

            if (gotchiTypes.Count() > 0) {

                // Include the average of all types of this species.

                result.MaxHp = gotchiTypes.Sum(x => x.BaseHp);
                result.Atk = gotchiTypes.Sum(x => x.BaseAtk);
                result.Def = gotchiTypes.Sum(x => x.BaseDef);
                result.Spd = gotchiTypes.Sum(x => x.BaseSpd);

                denominator += gotchiTypes.Count();

            }

            long[] ancestor_ids = await SpeciesUtils.GetAncestorIdsAsync(gotchi.SpeciesId);

            // Factor in the base stats of this species' ancestor (which will, in turn, factor in all other ancestors).

            if (ancestor_ids.Count() > 0) {

                GotchiStats ancestor_stats = await GetBaseStatsAsync(new Gotchi { SpeciesId = ancestor_ids.Last() });

                result.MaxHp += ancestor_stats.MaxHp;
                result.Atk += ancestor_stats.Atk;
                result.Def += ancestor_stats.Def;
                result.Spd += ancestor_stats.Spd;

                denominator += 1;

            }

            // Add 20 points if this species has an ancestor (this effect will be compounded by the previous loop).

            if (ancestor_ids.Count() > 0) {

                result.MaxHp += 20;
                result.Atk += 20;
                result.Def += 20;
                result.Spd += 20;

            }

            // Get the average of each base stat.

            denominator = Math.Max(denominator, 1);

            result.MaxHp /= denominator;
            result.Atk /= denominator;
            result.Def /= denominator;
            result.Spd /= denominator;

            // Add 0.1 points for every day the gotchi has been alive.

            result.MaxHp += gotchi.Age / 10;
            result.Atk += gotchi.Age / 10;
            result.Def += gotchi.Age / 10;
            result.Spd += gotchi.Age / 10;

            // Add or remove stats based on the species' description.
            await _calculateDescriptionBasedBaseStats(gotchi, result);

            return result;

        }
        public async Task<GotchiStats> GetStatsAsync(Gotchi gotchi) {

            GotchiStats result = await GetBaseStatsAsync(gotchi);

            result.Level = GotchiExperienceCalculator.GetLevel(gotchi.ExperienceGroup, gotchi.Experience);

            // Calculate final stats based on level and base stats.
            // #todo Implement IVs/EVs

            result.MaxHp = _calculateHp(result.MaxHp, 0, 0, result.Level);
            result.Atk = _calculateStat(result.Atk, 0, 0, result.Level);
            result.Def = _calculateStat(result.Def, 0, 0, result.Level);
            result.Spd = _calculateStat(result.Spd, 0, 0, result.Level);

            result.Hp = result.MaxHp;

            return await Task.FromResult(result);

        }

        private static int _calculateHp(int baseStat, int iv, int ev, int level) {

            return (int)((Math.Floor(((2.0 * baseStat) + iv + (ev / 4.0)) * level) / 100.0) + level + 10.0);

        }
        private static int _calculateStat(int baseStat, int iv, int ev, int level) {

            return (int)(Math.Floor((((2.0 * baseStat) + iv + (ev / 4.0)) * level) / 100.0) + 5.0);

        }

        private static async Task _calculateDescriptionBasedBaseStats(Gotchi gotchi, GotchiStats stats) {

            Species species = await SpeciesUtils.GetSpeciesAsync(gotchi.SpeciesId);

            if (Regex.IsMatch(species.description, "photosynthesi(s|izes)", RegexOptions.IgnoreCase))
                stats.MaxHp += 10;

            if (Regex.IsMatch(species.description, "spikes?|claws?|teeth|jaws|fangs", RegexOptions.IgnoreCase))
                stats.Atk += 10;

            if (Regex.IsMatch(species.description, "shell|carapace|exoskeleton", RegexOptions.IgnoreCase))
                stats.Def += 10;

            foreach (Match m in Regex.Matches(species.description, "flies|fly|quick|fast|agile|nimble", RegexOptions.IgnoreCase))
                stats.Spd += 10;

            foreach (Match m in Regex.Matches(species.description, "slow|heavy", RegexOptions.IgnoreCase))
                stats.Spd -= 10;

        }

    }

}