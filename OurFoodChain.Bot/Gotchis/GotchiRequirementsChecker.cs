﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurFoodChain.Gotchis {

    public class GotchiRequirementsChecker {

        public GotchiRequirements Requires { get; set; } = new GotchiRequirements();

        public async Task<bool> CheckAsync(Gotchi gotchi) {

            List<GotchiRequirements> requirements = new List<GotchiRequirements> {
                Requires
            };

            if (Requires != null)
                requirements.AddRange(Requires.OrValue);

            foreach (GotchiRequirements requirement in requirements)
                if (!await CheckAsync(gotchi, requirement))
                    return false;

            return true;

        }
        public async Task<bool> CheckAsync(Gotchi gotchi, GotchiRequirements requirements) {

            if (requirements is null)
                return true;

            if (requirements.AlwaysFailValue)
                return false;

            if (!_checkLevels(gotchi, requirements))
                return false;

            if (!string.IsNullOrEmpty(requirements.RolePattern) && !await _checkRolesAsync(gotchi, requirements))
                return false;

            if (!string.IsNullOrEmpty(requirements.TypePattern) && !await _checkTypesAsync(gotchi, requirements))
                return false;

            Species species = await SpeciesUtils.GetSpeciesAsync(gotchi.SpeciesId);

            if (!string.IsNullOrEmpty(requirements.DescriptionPattern) && !_checkDescription(species, requirements))
                return false;

            return true;

        }

        private async Task<bool> _checkRolesAsync(Gotchi gotchi, GotchiRequirements requirements) {

            try {

                Role[] roles = await SpeciesUtils.GetRolesAsync(gotchi.SpeciesId);

                foreach (Role role in roles)
                    if (Regex.IsMatch(role.Name, requirements.RolePattern, RegexOptions.IgnoreCase))
                        return true;

            }
            catch (Exception) { }

            return false;

        }
        private bool _checkDescription(Species species, GotchiRequirements requirements) {

            try {

                if (Regex.IsMatch(species.Description, requirements.DescriptionPattern, RegexOptions.IgnoreCase))
                    return true;

            }
            catch (Exception) { }

            return false;

        }
        private bool _checkLevels(Gotchi gotchi, GotchiRequirements requirements) {

            int level = GotchiExperienceCalculator.GetLevel(ExperienceGroup.Default, gotchi.Experience);

            return level >= requirements.MinimumLevelValue && level <= requirements.MaximumLevelValue;

        }
        private async Task<bool> _checkTypesAsync(Gotchi gotchi, GotchiRequirements requirements) {

            try {

                // Get the types assigned to this gotchi.
                GotchiType[] types = await Global.GotchiContext.TypeRegistry.GetTypesAsync(gotchi);

                // Compare each type using the type pattern provided.
                // #todo Types can also have aliases (AliasPattern). For now, we'll just try to match against the type pattern as well.

                foreach (GotchiType type in types)
                    if (Regex.IsMatch(type.Name, requirements.TypePattern, RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(type.AliasPattern, requirements.TypePattern, RegexOptions.IgnoreCase))
                        return true;

            }
            catch (Exception) { }

            return false;

        }

    }

}