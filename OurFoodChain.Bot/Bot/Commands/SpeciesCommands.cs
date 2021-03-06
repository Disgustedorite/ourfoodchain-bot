﻿using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.Bot {

    public class SpeciesCommands :
        ModuleBase {

        // Public members

        [Command("species"), Alias("sp", "s")]
        public async Task SpeciesInfo() {
            await ListSpecies();
        }
        [Command("species"), Alias("sp", "s")]
        public async Task SpeciesInfo(string species) {
            await ShowSpeciesInfoAsync(Context, species);
        }
        [Command("species"), Alias("sp", "s")]
        public async Task SpeciesInfo(string genus, string species) {
            await ShowSpeciesInfoAsync(Context, genus, species);
        }

        [Command("listspecies"), Alias("specieslist", "listsp", "splist")]
        public async Task ListSpecies() {

            // Get all species.

            List<Species> species = new List<Species>();

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Species;"))
            using (DataTable table = await Database.GetRowsAsync(cmd))
                foreach (DataRow row in table.Rows)
                    species.Add(await SpeciesUtils.SpeciesFromDataRow(row));

            // If there are no species, state so.

            if (species.Count <= 0) {

                await BotUtils.ReplyAsync_Info(Context, "No species have been added yet.");

                return;

            }

            // Create embed pages.

            species.Sort((lhs, rhs) => lhs.ShortName.CompareTo(rhs.ShortName));

            List<EmbedBuilder> pages = EmbedUtils.SpeciesListToEmbedPages(species, fieldName: string.Format("All species ({0}):", species.Count()));

            // Send the result.

            Bot.PaginatedMessage reply = new Bot.PaginatedMessage();

            foreach (EmbedBuilder page in pages)
                reply.Pages.Add(page.Build());

            await Bot.DiscordUtils.SendMessageAsync(Context, reply);

        }
        [Command("listspecies"), Alias("specieslist", "listsp", "splist")]
        public async Task ListSpecies(string taxonName) {

            // Get the taxon.

            Taxon taxon = await BotUtils.GetTaxonFromDb(taxonName);

            if (taxon is null) {

                await BotUtils.ReplyAsync_Error(Context, "No such taxon exists.");

                return;

            }

            // Get all species under that taxon.

            List<Species> species = new List<Species>();
            species.AddRange(await BotUtils.GetSpeciesInTaxonFromDb(taxon));

            species.Sort((lhs, rhs) => lhs.FullName.CompareTo(rhs.FullName));

            // We might get a lot of species, which may not fit in one embed.
            // We'll need to use a paginated embed to reliably display the full list.

            // Create embed pages.

            List<EmbedBuilder> pages = EmbedUtils.SpeciesListToEmbedPages(species, fieldName: string.Format("Species in this {0} ({1}):", taxon.GetTypeName(), species.Count()));

            if (pages.Count <= 0)
                pages.Add(new EmbedBuilder());

            // Add description to the first page.

            StringBuilder description_builder = new StringBuilder();
            description_builder.AppendLine(taxon.GetDescriptionOrDefault());

            if (species.Count() <= 0) {

                description_builder.AppendLine();
                description_builder.AppendLine(string.Format("This {0} contains no species.", Taxon.GetRankName(taxon.type)));

            }

            // Add title to all pages.

            foreach (EmbedBuilder page in pages) {

                page.WithTitle(string.IsNullOrEmpty(taxon.CommonName) ? taxon.GetName() : string.Format("{0} ({1})", taxon.GetName(), taxon.GetCommonName()));
                page.WithDescription(description_builder.ToString());
                page.WithThumbnailUrl(taxon.pics);

            }

            // Send the result.

            Bot.PaginatedMessage reply = new Bot.PaginatedMessage();

            foreach (EmbedBuilder page in pages)
                reply.Pages.Add(page.Build());

            await Bot.DiscordUtils.SendMessageAsync(Context, reply);

        }

        [Command("setspecies"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task SetSpecies(string species, string newName) {
            await SetSpecies("", species, newName);
        }
        [Command("setspecies"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task SetSpecies(string genus, string species, string newName) {

            // Get the specified species.

            Species sp = await BotUtils.ReplyFindSpeciesAsync(Context, genus, species);

            if (sp is null)
                return;

            // Update the species.

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Species SET name=$name WHERE id=$species_id;")) {

                cmd.Parameters.AddWithValue("$name", newName.ToLower());
                cmd.Parameters.AddWithValue("$species_id", sp.Id);

                await Database.ExecuteNonQuery(cmd);

            }

            await BotUtils.ReplyAsync_Success(Context, string.Format("**{0}** has been successfully renamed to **{1}**.", sp.ShortName, BotUtils.GenerateSpeciesName(sp.GenusName, newName)));

        }

        [Command("addspecies"), Alias("addsp"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task AddSpecies(string genus, string species, string zone = "", string description = "") {

            // Check if the species already exists before attempting to add it.

            if ((await BotUtils.GetSpeciesFromDb(genus, species)).Count() > 0) {
                await BotUtils.ReplyAsync_Warning(Context, string.Format("The species \"{0}\" already exists.", BotUtils.GenerateSpeciesName(genus, species)));
                return;
            }

            await BotUtils.AddGenusToDb(genus);

            Taxon genus_info = await BotUtils.GetGenusFromDb(genus);

            using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO Species(name, description, genus_id, owner, timestamp, user_id) VALUES($name, $description, $genus_id, $owner, $timestamp, $user_id);")) {

                cmd.Parameters.AddWithValue("$name", species.ToLower());
                cmd.Parameters.AddWithValue("$description", description);
                cmd.Parameters.AddWithValue("$genus_id", genus_info.id);
                cmd.Parameters.AddWithValue("$owner", Context.User.Username);
                cmd.Parameters.AddWithValue("$user_id", Context.User.Id);
                cmd.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                await Database.ExecuteNonQuery(cmd);

            }

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);
            Species sp = sp_list.Count() > 0 ? sp_list[0] : null;
            long species_id = sp == null ? -1 : sp.Id;

            if (species_id < 0) {
                await BotUtils.ReplyAsync_Error(Context, "Failed to add species (invalid Species ID).");
                return;
            }

            // Add to all given zones.
            await _plusZone(sp, zone, string.Empty, onlyShowErrors: true);

            // Add the user to the trophy scanner queue in case their species earned them any new trophies.

            if (OurFoodChainBot.Instance.Config.TrophiesEnabled)
                await Global.TrophyScanner.AddToQueueAsync(Context, Context.User.Id);

            await BotUtils.ReplyAsync_Success(Context, string.Format("Successfully created new species, **{0}**.", BotUtils.GenerateSpeciesName(genus, species)));

        }

        [Command("setzone"), Alias("setzones"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task SetZone(string genus, string species, string zone = "") {

            // If the zone argument is empty, assume the user omitted the genus.

            if (string.IsNullOrEmpty(zone)) {
                zone = species;
                species = genus;
                genus = string.Empty;
            }

            // Get the specified species.

            Species sp = await BotUtils.ReplyFindSpeciesAsync(Context, genus, species);

            if (sp is null)
                return;

            // Delete existing zone information for the species.

            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM SpeciesZones WHERE species_id=$species_id;")) {

                cmd.Parameters.AddWithValue("$species_id", sp.Id);

                await Database.ExecuteNonQuery(cmd);

            }

            // Add new zone information for the species.
            await _plusZone(sp, zone, string.Empty, onlyShowErrors: false);

        }

        [Command("+zone"), Alias("+zones"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task PlusZone(string arg0, string arg1, string arg2) {

            // Possible cases:
            // 1. <species> <zone> <notes>
            // 2. <genus> <species> <zone>

            // If a species exists with the given genus/species, assume the user intended case (2).

            Species[] species_list = await SpeciesUtils.GetSpeciesAsync(arg0, arg1);

            if (species_list.Count() == 1) {

                // If there is a unqiue species match, proceed with the assumption of case (2).

                await _plusZone(species_list[0], zoneList: arg2, notes: string.Empty, onlyShowErrors: false);

            }
            else if (species_list.Count() > 1) {

                // If there are species matches but no unique result, show the user.
                await BotUtils.ReplyValidateSpeciesAsync(Context, species_list);

            }
            else if (species_list.Count() <= 0) {

                // If there were no matches, assume the user intended case (1).

                species_list = await SpeciesUtils.GetSpeciesAsync(string.Empty, arg0);

                if (await BotUtils.ReplyValidateSpeciesAsync(Context, species_list))
                    await _plusZone(species_list[0], zoneList: arg1, notes: arg2, onlyShowErrors: false);

            }

        }
        [Command("+zone"), Alias("+zones"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task PlusZone(string species, string zoneList) {
            await PlusZone(string.Empty, species, zoneList, string.Empty);
        }
        [Command("+zone"), Alias("+zones"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task PlusZone(string genus, string species, string zoneList, string notes) {

            // Ensure that the user has necessary privileges to use this command.
            if (!await BotUtils.ReplyHasPrivilegeAsync(Context, PrivilegeLevel.ServerModerator))
                return;

            Species sp = await BotUtils.ReplyFindSpeciesAsync(Context, genus, species);

            if (!(sp is null))
                await _plusZone(sp, zoneList: zoneList, notes: notes, onlyShowErrors: false);

        }

        [Command("-zone"), Alias("-zones"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task MinusZone(string species, string zone) {
            await MinusZone("", species, zone);
        }
        [Command("-zone"), Alias("-zones"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task MinusZone(string genus, string species, string zoneList) {

            // Ensure that the user has necessary privileges to use this command.
            if (!await BotUtils.ReplyHasPrivilegeAsync(Context, PrivilegeLevel.ServerModerator))
                return;

            // Get the specified species.

            Species sp = await BotUtils.ReplyFindSpeciesAsync(Context, genus, species);

            if (sp is null)
                return;

            // Get the zones that the species currently resides in.
            // These will be used to show warning messages (e.g., doesn't exist in the given zone).

            long[] current_zone_ids = (await BotUtils.GetZonesFromDb(sp.Id)).Select(x => x.Id).ToArray();

            // Get the zones from user input.
            ZoneListResult zones = await ZoneUtils.GetZonesByZoneListAsync(zoneList);

            // Remove the zones from the species.
            await SpeciesUtils.RemoveZonesAsync(sp, zones.Zones);

            if (zones.InvalidZones.Count() > 0) {

                // Show a warning if the user provided any invalid zones.

                await BotUtils.ReplyAsync_Warning(Context, string.Format("{0} {1} not exist.",
                    StringUtils.ConjunctiveJoin(", ", zones.InvalidZones.Select(x => string.Format("**{0}**", ZoneUtils.FormatZoneName(x))).ToArray()),
                    zones.InvalidZones.Count() == 1 ? "does" : "do"));

            }

            if (zones.Zones.Any(x => !current_zone_ids.Contains(x.Id))) {

                // Show a warning if the species wasn't in one or more of the zones provided.

                await BotUtils.ReplyAsync_Warning(Context, string.Format("**{0}** is already absent from {1}.",
                    sp.ShortName,
                    StringUtils.ConjunctiveJoin(", ", zones.Zones.Where(x => !current_zone_ids.Contains(x.Id)).Select(x => string.Format("**{0}**", x.GetFullName())).ToArray())));

            }

            if (zones.Zones.Any(x => current_zone_ids.Contains(x.Id))) {

                // Show a confirmation of all valid zones.

                await BotUtils.ReplyAsync_Success(Context, string.Format("**{0}** no longer inhabits {1}.",
                    sp.ShortName,
                    StringUtils.DisjunctiveJoin(", ", zones.Zones.Where(x => current_zone_ids.Contains(x.Id)).Select(x => string.Format("**{0}**", x.GetFullName())).ToArray())));

            }

        }

        [Command("setowner"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task SetOwner(string speciesName, IUser user) {
            await SetOwner(string.Empty, speciesName, user);
        }
        [Command("setowner"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task SetOwner(string genusName, string speciesName, IUser user) {

            Species species = await BotUtils.ReplyFindSpeciesAsync(Context, genusName, speciesName);

            if (species != null) {

                await SpeciesUtils.SetOwnerAsync(species, user.Username, user.Id);

                // Add the new owner to the trophy scanner queue in case their species earned them any new trophies.

                if (OurFoodChainBot.Instance.Config.TrophiesEnabled)
                    await Global.TrophyScanner.AddToQueueAsync(Context, user.Id);

                await BotUtils.ReplyAsync_Success(Context, string.Format("**{0}** is now owned by **{1}**.", species.ShortName, user.Username));

            }

        }
        [Command("setowner"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task SetOwner(string speciesName, string ownerName) {
            await SetOwner(string.Empty, speciesName, ownerName);
        }
        [Command("setowner"), RequirePrivilege(PrivilegeLevel.ServerModerator)]
        public async Task SetOwner(string genusName, string speciesName, string ownerName) {

            Species species = await BotUtils.ReplyFindSpeciesAsync(Context, genusName, speciesName);

            if (species != null) {

                // If we've seen this user before, get their user ID from the database.

                UserInfo userInfo = await UserUtils.GetUserInfoAsync(ownerName);

                if (userInfo != null) {

                    ownerName = userInfo.Username;

                    await SpeciesUtils.SetOwnerAsync(species, userInfo.Username, userInfo.Id);

                }
                else
                    await SpeciesUtils.SetOwnerAsync(species, ownerName);

                await BotUtils.ReplyAsync_Success(Context, string.Format("**{0}** is now owned by **{1}**.", species.ShortName, ownerName));

            }

        }

        [Command("addedby"), Alias("ownedby", "own", "owned")]
        public async Task AddedBy() {
            await AddedBy(Context.User);
        }
        [Command("addedby"), Alias("ownedby", "own", "owned")]
        public async Task AddedBy(IUser user) {

            if (user is null)
                user = Context.User;

            // Get all species belonging to this user.

            List<Species> species_list = new List<Species>();

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Species WHERE owner = $owner OR user_id = $user_id;")) {

                cmd.Parameters.AddWithValue("$owner", user.Username);
                cmd.Parameters.AddWithValue("$user_id", user.Id);

                using (DataTable rows = await Database.GetRowsAsync(cmd)) {

                    foreach (DataRow row in rows.Rows)
                        species_list.Add(await SpeciesUtils.SpeciesFromDataRow(row));

                    species_list.Sort((lhs, rhs) => lhs.ShortName.CompareTo(rhs.ShortName));

                }

            }

            // Display the species belonging to this user.

            await _displaySpeciesAddedBy(user.Username, user.GetAvatarUrl(size: 32), species_list);

        }
        [Command("addedby"), Alias("ownedby", "own", "owned")]
        public async Task AddedBy(string owner) {

            // If we get this overload, then the requested user does not currently exist in the guild.

            // If we've seen the user before, we can get their information from the database.
            UserInfo userInfo = await UserUtils.GetUserInfoAsync(owner);

            if (userInfo != null) {

                // The user exists in the database, so create a list of all species they own.
                Species[] species = await UserUtils.GetSpeciesAsync(userInfo);

                // Display the species list.
                await _displaySpeciesAddedBy(userInfo.Username, string.Empty, species.ToList());

            }
            else {

                // The user does not exist in the database.
                await BotUtils.ReplyAsync_Error(Context, "No such user exists.");

            }

        }

        public static async Task ShowSpeciesInfoAsync(ICommandContext context, Species species) {

            if (await BotUtils.ReplyValidateSpeciesAsync(context, species)) {

                EmbedBuilder embed = new EmbedBuilder();
                StringBuilder descriptionBuilder = new StringBuilder();

                string embed_title = species.FullName;
                Color embed_color = Color.Blue;

                CommonName[] common_names = await SpeciesUtils.GetCommonNamesAsync(species);

                if (common_names.Count() > 0)
                    embed_title += string.Format(" ({0})", string.Join(", ", (object[])common_names));

                // Show generation only if generations are enabled.

                if (OurFoodChainBot.Instance.Config.GenerationsEnabled) {

                    Generation gen = await GenerationUtils.GetGenerationByTimestampAsync(species.Timestamp);

                    embed.AddField("Gen", gen is null ? "???" : gen.Number.ToString(), inline: true);

                }

                embed.AddField("Owner", await SpeciesUtils.GetOwnerOrDefaultAsync(species, context), inline: true);

                SpeciesZone[] zone_list = await SpeciesUtils.GetZonesAsync(species);

                if (zone_list.Count() > 0) {

                    embed_color = Bot.DiscordUtils.ConvertColor((await ZoneUtils.GetZoneTypeAsync(zone_list
                        .GroupBy(x => x.Zone.ZoneTypeId)
                        .OrderBy(x => x.Count())
                        .Last()
                        .Key)).Color);

                }

                string zones_value = new SpeciesZoneCollection(zone_list).ToString(SpeciesZoneCollectionToStringOptions.Default, Bot.DiscordUtils.MaxFieldLength);

                embed.AddField("Zone(s)", string.IsNullOrEmpty(zones_value) ? "None" : zones_value, inline: true);

                // Check if the species is extinct.
                using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Extinctions WHERE species_id=$species_id;")) {

                    cmd.Parameters.AddWithValue("$species_id", species.Id);

                    DataRow row = await Database.GetRowAsync(cmd);

                    if (!(row is null)) {

                        embed_title = "[EXTINCT] " + embed_title;
                        embed_color = Color.Red;

                        string reason = row.Field<string>("reason");
                        long timestamp = (long)row.Field<decimal>("timestamp");

                        if (!string.IsNullOrEmpty(reason))
                            descriptionBuilder.AppendLine(string.Format("**Extinct ({0}):** _{1}_\n", await BotUtils.TimestampToDateStringAsync(timestamp), reason));

                    }

                }

                descriptionBuilder.Append(species.GetDescriptionOrDefault());

                embed.WithTitle(embed_title);
                embed.WithThumbnailUrl(species.Picture);
                embed.WithColor(embed_color);

                if (!string.IsNullOrEmpty(OurFoodChainBot.Instance.Config.WikiUrlFormat)) {

                    // Discord automatically encodes certain characters in URIs, which doesn't allow us to update the config via Discord when we have "{0}" in the URL.
                    // Replace this with the proper string before attempting to call string.Format.
                    string format = OurFoodChainBot.Instance.Config.WikiUrlFormat.Replace("%7B0%7D", "{0}");
                
                    embed.WithUrl(string.Format(format, Uri.EscapeUriString(GetWikiPageTitleForSpecies(species, common_names))));

                }

                if (embed.Length + descriptionBuilder.Length > DiscordUtils.MaxEmbedLength) {

                    // If the description puts us over the character limit, we'll paginate.

                    int pageLength = DiscordUtils.MaxEmbedLength - embed.Length;

                    List<EmbedBuilder> pages = new List<EmbedBuilder>();

                    foreach (string pageText in new StringPaginator(descriptionBuilder.ToString()) { MaxPageLength = pageLength }) {

                        EmbedBuilder page = new EmbedBuilder();

                        page.WithTitle(embed.Title);
                        page.WithThumbnailUrl(embed.ThumbnailUrl);
                        page.WithFields(embed.Fields);
                        page.WithDescription(pageText);

                        pages.Add(page);

                    }

                    PaginatedMessageBuilder builder = new Bot.PaginatedMessageBuilder(pages);
                    builder.AddPageNumbers();
                    builder.SetColor(embed_color);

                    await DiscordUtils.SendMessageAsync(context, builder.Build());

                }
                else {

                    embed.WithDescription(descriptionBuilder.ToString());

                    await context.Channel.SendMessageAsync("", false, embed.Build());

                }

            }

        }
        public static async Task ShowSpeciesInfoAsync(ICommandContext context, string speciesName) {
            await ShowSpeciesInfoAsync(context, string.Empty, speciesName);
        }
        public static async Task ShowSpeciesInfoAsync(ICommandContext context, string genusName, string speciesName) {

            Species sp = await BotUtils.ReplyAsync_FindSpecies(context, genusName, speciesName,
            async (BotUtils.ConfirmSuggestionArgs args) => await ShowSpeciesInfoAsync(context, args.Suggestion));

            if (sp is null)
                return;

            await ShowSpeciesInfoAsync(context, sp);

        }

        // Private members

        public async Task _plusZone(Species species, string zoneList, string notes, bool onlyShowErrors = false) {

            // Get the zones from user input.
            ZoneListResult zones = await ZoneUtils.GetZonesByZoneListAsync(zoneList);

            // Add the zones to the species.
            await SpeciesUtils.AddZonesAsync(species, zones.Zones, notes);

            if (zones.InvalidZones.Count() > 0) {

                // Show a warning if the user provided any invalid zones.

                await BotUtils.ReplyAsync_Warning(Context, string.Format("{0} {1} not exist.",
                    StringUtils.ConjunctiveJoin(", ", zones.InvalidZones.Select(x => string.Format("**{0}**", ZoneUtils.FormatZoneName(x))).ToArray()),
                    zones.InvalidZones.Count() == 1 ? "does" : "do"));

            }

            if (zones.Zones.Count() > 0 && !onlyShowErrors) {

                // Show a confirmation of all valid zones.

                await BotUtils.ReplyAsync_Success(Context, string.Format("**{0}** now inhabits {1}.",
                      species.ShortName,
                      StringUtils.ConjunctiveJoin(", ", zones.Zones.Select(x => string.Format("**{0}**", x.GetFullName())).ToArray())));

            }

        }
        private async Task _displaySpeciesAddedBy(string username, string thumbnailUrl, List<Species> speciesList) {

            if (speciesList.Count() <= 0) {

                await BotUtils.ReplyAsync_Info(Context, string.Format("**{0}** has not submitted any species yet.", username));

            }
            else {

                Bot.PaginatedMessageBuilder embed = new Bot.PaginatedMessageBuilder(EmbedUtils.SpeciesListToEmbedPages(speciesList,
                    fieldName: string.Format("Species owned by {0} ({1})", username, speciesList.Count())));

                embed.SetThumbnailUrl(thumbnailUrl);

                await Bot.DiscordUtils.SendMessageAsync(Context, embed.Build());

            }

        }
        private static string GetWikiPageTitleForSpecies(Species species, CommonName[] commonNames) {

            // This is the same process as used in SpeciesPageBuilder.BuildTitleAsync.
            // #todo Instead of being copy-pasted, this process should be in its own function used by both classes.

            string title = string.Empty;

            if (!string.IsNullOrWhiteSpace(species.CommonName))
                title = species.CommonName;
            else {

                if (commonNames.Count() > 0)
                    title = commonNames.First().Value;
                else
                    title = species.FullName;

            }

            if (string.IsNullOrWhiteSpace(title))
                title = species.FullName;

            if (!string.IsNullOrEmpty(title))
                title = title.Trim();

            return title;

        }


    }

}