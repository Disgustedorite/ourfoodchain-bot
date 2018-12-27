﻿using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurFoodChain {

    public class Commands :
        ModuleBase {

        [Command("genus"), Alias("g", "genera")]
        public async Task Genus(string name = "") {

            using (SQLiteConnection conn = await Database.GetConnectionAsync()) {

                EmbedBuilder embed = new EmbedBuilder();
                StringBuilder builder = new StringBuilder();

                // If no genus name was provided, list all genera.

                if (string.IsNullOrEmpty(name)) {

                    List<Genus> genera = new List<Genus>();

                    using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Genus ORDER BY name ASC;"))
                    using (DataTable rows = await Database.GetRowsAsync(conn, cmd))
                        foreach (DataRow row in rows.Rows)
                            genera.Add(OurFoodChain.Genus.FromDataRow(row));

                    foreach (Genus genus_info in genera) {

                        long count = 0;

                        using (SQLiteCommand cmd = new SQLiteCommand("SELECT count(*) FROM Species WHERE genus_id=$genus_id;")) {

                            cmd.Parameters.AddWithValue("$genus_id", genus_info.id);

                            count = (await Database.GetRowAsync(cmd)).Field<long>("count(*)");

                        }

                        builder.AppendLine(string.Format("{0} ({1})",
                            StringUtils.ToTitleCase(genus_info.name),
                            count
                            ));

                    }

                    embed.WithTitle(string.Format("All genera ({0})", genera.Count()));
                    embed.WithDescription(builder.ToString());

                    await ReplyAsync("", false, embed.Build());

                    return;

                }

                embed.WithTitle(StringUtils.ToTitleCase(name));

                // Get information about the genus.

                long genus_id = -1;
                string genus_name = name;

                using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Genus WHERE name=$name;")) {

                    cmd.Parameters.AddWithValue("$name", name.ToLower());

                    DataRow row = await Database.GetRowAsync(conn, cmd);

                    if (!(row is null)) {

                        string description = row.Field<string>("description");
                        genus_id = row.Field<long>("id");
                        genus_name = row.Field<string>("name");

                        if (string.IsNullOrEmpty(description))
                            description = BotUtils.DEFAULT_GENUS_DESCRIPTION;

                        builder.AppendLine(description);

                    }
                    else {

                        await ReplyAsync("No such genus exists.");

                        return;

                    }

                }

                // Get information about the species.

                using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Species WHERE genus_id=$genus_id;")) {

                    cmd.Parameters.AddWithValue("$genus_id", genus_id);

                    using (DataTable rows = await Database.GetRowsAsync(conn, cmd)) {

                        builder.AppendLine();
                        builder.AppendLine("**Species in this genus:**");

                        foreach (DataRow row in rows.Rows)
                            builder.AppendLine(BotUtils.GenerateSpeciesName(genus_name, row.Field<string>("name")));

                        embed.WithDescription(builder.ToString());

                        await ReplyAsync("", false, embed.Build());

                    }

                }

            }

        }

        [Command("info"), Alias("i", "species", "sp", "s")]
        public async Task Info(string genus, string species = "") {

            // If the user does not provide a genus + species, query by species only.
            if (string.IsNullOrEmpty(species)) {

                species = genus;
                genus = "";

            }

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (!await BotUtils.ReplyAsync_ValidateSpecies(Context, sp_list))
                return;

            EmbedBuilder embed = new EmbedBuilder();
            StringBuilder description_builder = new StringBuilder();

            Species sp = sp_list[0];

            string embed_title = sp.GetFullName();
            Color embed_color = Color.Blue;

            if (!string.IsNullOrEmpty(sp.commonName))
                embed_title += string.Format(" ({0})", StringUtils.ToTitleCase(sp.commonName));

            embed.WithColor(embed_color);
            embed.AddInlineField("Owner", sp.owner);

            List<string> zone_names = new List<string>();

            foreach (Zone zone in await BotUtils.GetZonesFromDb(sp.id)) {

                if (zone.type == ZoneType.Terrestrial)
                    embed_color = Color.DarkGreen;

                zone_names.Add(zone.GetShortName());

            }

            zone_names.Sort((lhs, rhs) => new ArrayUtils.NaturalStringComparer().Compare(lhs, rhs));

            embed.AddInlineField("Zone(s)", string.Join(", ", zone_names));

            // Check if the species is extinct.
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Extinctions WHERE species_id=$species_id;")) {

                cmd.Parameters.AddWithValue("$species_id", sp.id);

                DataRow row = await Database.GetRowAsync(cmd);

                if (!(row is null)) {

                    embed_title = "[EXTINCT] " + embed_title;
                    embed.WithColor(Color.Red);

                    string reason = row.Field<string>("reason");

                    if (!string.IsNullOrEmpty(reason))
                        description_builder.AppendLine(string.Format("**{0}**\n", reason));

                }

            }

            description_builder.Append(sp.GetDescriptionOrDefault());

            embed.WithTitle(embed_title);
            embed.WithDescription(description_builder.ToString());
            embed.WithThumbnailUrl(sp.pics);

            await ReplyAsync("", false, embed.Build());

        }

        [Command("setpic")]
        public async Task SetPic(string genus, string species, string imageUrl) {

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() <= 0)
                await ReplyAsync("No such species exists.");
            else if (!Regex.Match(imageUrl, "^https?:").Success)
                await ReplyAsync("Please provide a valid image URL.");
            else {

                Species sp = sp_list[0];

                using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Species SET pics=$url WHERE id=$species_id;")) {

                    cmd.Parameters.AddWithValue("$url", imageUrl);
                    cmd.Parameters.AddWithValue("$species_id", sp.id);

                    await Database.ExecuteNonQuery(cmd);

                }

                await ReplyAsync("Image added successfully.");

            }

        }

        [Command("addspecies"), Alias("addsp")]
        public async Task AddSpecies(string genus, string species, string zone, string description = "") {

            string[] zones = zone.Split(',', '/');
            species = species.ToLower();

            await BotUtils.AddGenusToDb(genus);

            Genus genus_info = await BotUtils.GetGenusFromDb(genus);

            using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO Species(name, description, phylum_id, genus_id, owner, timestamp) VALUES($name, $description, $phylum_id, $genus_id, $owner, $timestamp);")) {

                cmd.Parameters.AddWithValue("$name", species);
                cmd.Parameters.AddWithValue("$description", description);
                cmd.Parameters.AddWithValue("$phylum_id", 0);
                cmd.Parameters.AddWithValue("$genus_id", genus_info.id);
                cmd.Parameters.AddWithValue("$owner", Context.User.Username);
                cmd.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                await Database.ExecuteNonQuery(cmd);

            }

            long species_id = await BotUtils.GetSpeciesIdFromDb(genus_info.id, species);

            if (species_id < 0) {
                await ReplyAsync("Failed to add species (invalid ID).");
                return;
            }

            // Add to all given zones.

            foreach (string zoneName in zones) {

                Zone zone_info = await BotUtils.GetZoneFromDb(zoneName);

                if (zone_info is null || zone_info.id == -1) {

                    await ReplyAsync("The given Zone does not exist.");

                    return;

                }

                using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO SpeciesZones(species_id, zone_id) VALUES($species_id, $zone_id);")) {

                    cmd.Parameters.AddWithValue("$species_id", species_id);
                    cmd.Parameters.AddWithValue("$zone_id", zone_info.id);

                    await Database.ExecuteNonQuery(cmd);

                }

            }

            await ReplyAsync("Species added successfully.");

        }

        [Command("setdescription"), Alias("setdesc")]
        public async Task SetDescription(string genus, string species, string description = "") {

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() <= 0)
                await ReplyAsync("No such species exists.");
            else {

                if (string.IsNullOrEmpty(description)) {

                    TwoPartCommandWaitParams p = new TwoPartCommandWaitParams();
                    p.type = TwoPartCommandWaitParamsType.Description;
                    p.args = new string[] { genus, species };
                    p.timestamp = DateTime.Now;

                    BotUtils.TWO_PART_COMMAND_WAIT_PARAMS[Context.User.Id] = p;

                    await ReplyAsync(string.Format("Enter a description for {0}.", BotUtils.GenerateSpeciesName(genus, species)));

                }
                else {

                    await BotUtils.UpdateSpeciesDescription(genus, species, description);

                    await ReplyAsync("Description added successfully.");

                }

            }

        }

        [Command("addzone"), Alias("addz")]
        public async Task AddZone(string name, string type = "", string description = "") {

            // Allow the user to specify zones with numbers (e.g., "1") or single letters (e.g., "A").
            // Otherwise, the name is taken as-is.
            name = OurFoodChain.Zone.GetFullName(name);

            name = name.ToLower();

            // If an invalid type was provided, use it as the description instead.
            if (type.ToLower() != "aquatic" && type.ToLower() != "terrestrial") {

                description = type;
                type = "";

            }

            type = type.ToLower();

            // Attempt to determine the type of the zone automatically if it was not provided.

            if (string.IsNullOrEmpty(type)) {

                if (Regex.Match(name, @"\d+$").Success)
                    type = "aquatic";
                else if (Regex.Match(name, "[a-z]+$").Success)
                    type = "terrestrial";
                else
                    type = "unknown";

            }

            using (SQLiteCommand cmd = new SQLiteCommand("INSERT OR REPLACE INTO Zones(name, type, description) VALUES($name, $type, $description);")) {

                cmd.Parameters.AddWithValue("$name", name.ToLower());
                cmd.Parameters.AddWithValue("$type", type.ToLower());
                cmd.Parameters.AddWithValue("$description", description);

                await Database.ExecuteNonQuery(cmd);

            }

            await ReplyAsync("Zone added successfully.");

        }

        [Command("zone"), Alias("z", "zones")]
        public async Task Zone(string name = "") {

            // If no zone was provided, list all zones.

            if (string.IsNullOrEmpty(name) || name == "aquatic" || name == "terrestrial") {

                string cmd_str = "SELECT * FROM Zones;";

                if (!string.IsNullOrEmpty(name))
                    cmd_str = string.Format("SELECT * FROM Zones WHERE type=\"{0}\";", name);

                List<Zone> zone_list = new List<Zone>();

                using (SQLiteConnection conn = await Database.GetConnectionAsync())
                using (SQLiteCommand cmd = new SQLiteCommand(cmd_str))
                using (DataTable rows = await Database.GetRowsAsync(conn, cmd))
                    foreach (DataRow row in rows.Rows)
                        zone_list.Add(OurFoodChain.Zone.FromDataRow(row));

                zone_list.Sort((lhs, rhs) => new ArrayUtils.NaturalStringComparer().Compare(lhs.name, rhs.name));

                EmbedBuilder embed = new EmbedBuilder();

                foreach (Zone zone_info in zone_list) {

                    string description = zone_info.description;

                    if (string.IsNullOrEmpty(description))
                        description = BotUtils.DEFAULT_ZONE_DESCRIPTION;

                    embed.AddField(string.Format("**{0}** ({1})",
                        StringUtils.ToTitleCase(zone_info.name), zone_info.type.ToString()),
                        OurFoodChain.Zone.GetShortDescription(description)
                        );

                }

                if (string.IsNullOrEmpty(name))
                    name = "all";
                else if (name == "aquatic")
                    embed.WithColor(Color.Blue);
                else if (name == "terrestrial")
                    embed.WithColor(Color.DarkGreen);

                embed.WithTitle(StringUtils.ToTitleCase(string.Format("{0} zones", name)));

                await ReplyAsync("", false, embed.Build());

                return;

            }
            else {

                Zone zone = await BotUtils.GetZoneFromDb(name);

                if (!await BotUtils.ReplyAsync_ValidateZone(Context, zone))
                    return;

                List<Embed> pages = new List<Embed>();

                string title = string.Format("{0} ({1})", StringUtils.ToTitleCase(zone.name), zone.type.ToString());
                string description = zone.GetDescriptionOrDefault();
                Color color = Color.Blue;

                switch (zone.type) {
                    case ZoneType.Aquatic:
                        color = Color.Blue;
                        break;
                    case ZoneType.Terrestrial:
                        color = Color.DarkGreen;
                        break;
                }

                // Page #1 will contain a simple list of organisms.

                EmbedBuilder embed1 = new EmbedBuilder();

                embed1.WithTitle(title);
                embed1.WithDescription(description);
                embed1.WithColor(color);

                // Get all species living in this zone.

                List<Species> species_list = new List<Species>(await BotUtils.GetSpeciesFromDbByZone(zone));

                species_list.Sort((lhs, rhs) => lhs.GetShortName().CompareTo(rhs.GetShortName()));

                if (species_list.Count() > 0) {

                    StringBuilder lines = new StringBuilder();

                    foreach (Species sp in species_list)
                        lines.AppendLine(sp.GetShortName());

                    embed1.AddField(string.Format("Extant species in this zone ({0}):", species_list.Count()), lines.ToString());

                }

                pages.Add(embed1.Build());

                // Page 2 will contain the organisms organized by role.

                EmbedBuilder embed2 = new EmbedBuilder();

                embed2.WithTitle(title);
                embed2.WithDescription(description);
                embed2.WithColor(color);

                Dictionary<string, List<Species>> roles_map = new Dictionary<string, List<Species>>();

                foreach (Species sp in species_list) {

                    Role[] roles_list = await BotUtils.GetRolesFromDbBySpecies(sp);

                    if (roles_list.Count() <= 0) {

                        if (!roles_map.ContainsKey("no role"))
                            roles_map["no role"] = new List<Species>();

                        roles_map["no role"].Add(sp);

                        continue;

                    }

                    foreach (Role role in roles_list) {

                        if (!roles_map.ContainsKey(role.name))
                            roles_map[role.name] = new List<Species>();

                        roles_map[role.name].Add(sp);

                    }

                }

                // Sort the list of species belonging to each role.
                foreach (List<Species> i in roles_map.Values)
                    i.Sort((lhs, rhs) => lhs.GetShortName().CompareTo(rhs.GetShortName()));

                // Create a sorted list of keys so that the roles are in order.
                List<string> sorted_keys = new List<string>(roles_map.Keys);
                sorted_keys.Sort();

                foreach (string i in sorted_keys) {

                    StringBuilder lines = new StringBuilder();

                    foreach (Species j in roles_map[i])
                        lines.AppendLine(j.GetShortName());

                    embed2.AddInlineField(string.Format("{0}s ({1})", StringUtils.ToTitleCase(i), roles_map[i].Count()), lines.ToString());

                }

                pages.Add(embed2.Build());

                // 

                IUserMessage message = await ReplyAsync("", false, pages[0]);

                // Only bother with pagination if the zone actually contains species.

                if (species_list.Count() > 0) {

                    await message.AddReactionAsync(new Emoji("🇷"));

                    CommandUtils.PaginatedMessage paginated = new CommandUtils.PaginatedMessage {
                        pages = pages.ToArray()
                    };

                    CommandUtils.PAGINATED_MESSAGES.Add(message.Id, paginated);

                }

            }

        }

        [Command("setextinct")]
        public async Task SetExtinct(string genus, string species, string reason = "") {

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() <= 0)
                await ReplyAsync("No such species exists.");
            else {

                using (SQLiteCommand cmd = new SQLiteCommand("INSERT OR REPLACE INTO Extinctions(species_id, reason, timestamp) VALUES($species_id, $reason, $timestamp);")) {

                    cmd.Parameters.AddWithValue("$species_id", sp_list[0].id);
                    cmd.Parameters.AddWithValue("$reason", reason);
                    cmd.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                    await Database.ExecuteNonQuery(cmd);

                    await ReplyAsync("The species is now extinct.");

                }

            }

        }

        [Command("map")]
        public async Task Map() {

            EmbedBuilder page1 = new EmbedBuilder {
                ImageUrl = "https://cdn.discordapp.com/attachments/526503466001104926/527194144225886218/OFC2.png"
            };

            EmbedBuilder page2 = new EmbedBuilder {
                ImageUrl = "https://cdn.discordapp.com/attachments/526503466001104926/527194196260683778/OFCtruelabels.png"
            };

            IUserMessage message = await ReplyAsync("", false, page1.Build());
            await message.AddReactionAsync(new Emoji("🇿"));

            CommandUtils.PaginatedMessage paginated = new CommandUtils.PaginatedMessage {
                pages = new Embed[] { page1.Build(), page2.Build() }
            };

            CommandUtils.PAGINATED_MESSAGES.Add(message.Id, paginated);

        }

        [Command("setancestor")]
        public async Task SetAncestor(string genus, string species, string ancestorGenus, string ancestorSpecies = "") {

            // If the ancestor species was left blank, assume the same genus as current species.
            if (string.IsNullOrEmpty(ancestorSpecies)) {

                ancestorSpecies = ancestorGenus;
                ancestorGenus = genus;

            }

            Species[] descendant_list = await BotUtils.GetSpeciesFromDb(genus, species);
            Species[] ancestor_list = await BotUtils.GetSpeciesFromDb(ancestorGenus, ancestorSpecies);

            if (descendant_list.Count() == 0)
                await ReplyAsync("The child species does not exist.");
            else if (ancestor_list.Count() == 0)
                await ReplyAsync("The parent species does not exist.");
            else if (descendant_list[0].id == ancestor_list[0].id)
                await ReplyAsync("A species cannot be its own ancestor.");
            else {

                using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO Ancestors(species_id, ancestor_id) VALUES($species_id, $ancestor_id);")) {

                    cmd.Parameters.AddWithValue("$species_id", descendant_list[0].id);
                    cmd.Parameters.AddWithValue("$ancestor_id", ancestor_list[0].id);

                    await Database.ExecuteNonQuery(cmd);

                }

                await ReplyAsync("Ancestor updated successfully.");

            }

        }

        [Command("lineage"), Alias("ancestry", "ancestors")]
        public async Task Lineage(string genus, string species) {

            Species[] species_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (species_list.Count() <= 0)
                await ReplyAsync("No such species exists.");
            else {

                List<string> entries = new List<string>();

                entries.Add(string.Format("**{0} - {1}**", species_list[0].GetTimeStampAsDateString(), species_list[0].GetShortName()));

                long species_id = species_list[0].id;

                while (true) {

                    using (SQLiteCommand cmd = new SQLiteCommand("SELECT ancestor_id FROM Ancestors WHERE species_id=$species_id;")) {

                        cmd.Parameters.AddWithValue("$species_id", species_id);

                        DataRow row = await Database.GetRowAsync(cmd);

                        if (row is null)
                            break;

                        species_id = row.Field<long>("ancestor_id");

                        Species ancestor = await BotUtils.GetSpeciesFromDb(species_id);

                        entries.Add(string.Format("{0} - {1}", ancestor.GetTimeStampAsDateString(), ancestor.GetShortName()));

                    }

                }

                entries.Reverse();

                await ReplyAsync(string.Join(Environment.NewLine, entries));

            }

        }

        [Command("lineage2")]
        public async Task Lineage2(string genus, string species) {

            Species[] species_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (!await BotUtils.ReplyAsync_ValidateSpecies(Context, species_list))
                return;

            string image = await BotUtils.GenerateEvolutionTreeImage(species_list[0]);

            await Context.Channel.SendFileAsync(image);

        }

        [Command("setzone"), Alias("setzones")]
        public async Task SetZone(string genus, string species, string zone) {

            string[] zones = zone.Split(',', '/');

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() <= 0)
                await ReplyAsync("No such species exists.");
            else {

                // Remove existing zone information for this species.
                using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM SpeciesZones WHERE species_id=$species_id;")) {

                    cmd.Parameters.AddWithValue("$species_id", sp_list[0].id);

                    await Database.ExecuteNonQuery(cmd);

                }

                // Add new zone information for this species.
                foreach (string zoneName in zones) {

                    string name = zoneName.Trim();

                    using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO SpeciesZones(species_id, zone_id) VALUES($species_id, $zone_id);")) {

                        cmd.Parameters.AddWithValue("$species_id", sp_list[0].id);
                        cmd.Parameters.AddWithValue("$zone_id", (await BotUtils.GetZoneFromDb(name)).id);

                        await Database.ExecuteNonQuery(cmd);

                    }

                }

                await ReplyAsync("Zone(s) added successfully.");

            }

        }

        [Command("setcommonname"), Alias("setcommon")]
        public async Task SetCommonName(string genus, string species, string commonName) {

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() <= 0)
                await ReplyAsync("No such species exists.");
            else {

                using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Species SET common_name = $common_name WHERE id=$species_id;")) {

                    cmd.Parameters.AddWithValue("$species_id", sp_list[0].id);
                    cmd.Parameters.AddWithValue("$common_name", commonName);

                    await Database.ExecuteNonQuery(cmd);

                }

                await ReplyAsync("Common name added successfully.");

            }

        }

        [Command("setowner"), Alias("setown", "claim")]
        public async Task SetOwner(string genus, string species, IUser user = null) {

            if (user is null)
                user = Context.User;

            string owner = user.Username;

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() <= 0)
                await ReplyAsync("No such species exists.");
            else {

                using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Species SET owner = $owner WHERE id=$species_id;")) {

                    cmd.Parameters.AddWithValue("$species_id", sp_list[0].id);
                    cmd.Parameters.AddWithValue("$owner", owner);

                    await Database.ExecuteNonQuery(cmd);

                }

                await ReplyAsync("Owner added successfully.");

            }

        }

        [Command("help"), Alias("h")]
        public async Task Help(string command = "") {

            EmbedBuilder builder = new EmbedBuilder();

            if (string.IsNullOrEmpty(command)) {

                builder.WithTitle("Commands list");
                builder.WithFooter("For more information, use \"help <command>\".");

                builder.AddField("Info", "`genus` `info` `zone` `map` `lineage` `help` `predates` `prey` `ownedby` `search` `roles`");
                builder.AddField("Updates", "`addsp` `addzone` `setpic` `setdesc` `setextinct` `setowner` `setancestor` `setcommonname` `setprey` `setgenusdesc` `+role` `-role` `addrole` `setroledesc`");

            }
            else {

                builder.WithTitle(string.Format("Help: {0}", command));
                string description = "No description available";
                string aliases = "-";
                string example = "-";

                switch (command) {

                    case "genus":
                    case "g":
                    case "genera":
                        description = "Lists all species under the given genus. If no genus is provided, lists all genera.";
                        aliases = "genus, g, genera";
                        example = "?genus helix";
                        break;

                    case "info":
                    case "i":
                        description = "Shows information about the given species.";
                        aliases = "info, i, sp, species, s";
                        example = "?info H. quattuorus";
                        break;

                    case "zone":
                    case "z":
                    case "zones":
                        description = "Shows information about the given zone. If no zone is provided, lists all zones.";
                        aliases = "zone, zones, z";
                        example = "?zone 1\n?zones aquatic\n?zones terrestrial";
                        break;

                    case "map":
                        description = "Displays the map.";
                        aliases = "map";
                        example = "?map";
                        break;

                    case "lineage":
                    case "ancestry":
                    case "ancestors":
                        description = "Lists ancestors of the given species.";
                        aliases = "lineage, ancestry, ancestors";
                        example = "?lineage H. quattuorus";
                        break;

                    case "help":
                    case "h":
                        description = "Displays help information.";
                        aliases = "help, h";
                        example = "?help";
                        break;

                    case "addsp":
                    case "addspecies":
                        description = "Adds a new species to the database.";
                        aliases = "addsp, addspecies";
                        example = "?addsp helix quattuorus \"zone 12\" \"my description\"\n?addsp helix quattuorus 12";
                        break;

                    case "addzone":
                    case "addz":
                        description = "Adds a new zone to the database. Numeric zones are automatically categorized as aquatic, and alphabetic zones are categorized as terrestrial.";
                        aliases = "addz, addzone";
                        example = "?addzone 25\n?addzone 25 aquatic\n?addzone 25 terrestrial \"my description\"";
                        break;

                    case "setpic":
                        description = "Sets the picture for the given species.";
                        aliases = "setpic";
                        example = "?setpic H. quattuorus https://website.com/image.jpg";
                        break;

                    case "setdesc":
                    case "setdescription":
                        description = "Sets the description for the given species. Leave description blank to provide it in a separate message.";
                        aliases = "setdesc, setdescription";
                        example = "?setdesc H. quattuorus \"my description\"\n?setdesc H. quattuorus";
                        break;

                    case "setextinct":
                        description = "Marks the given species as extinct.";
                        aliases = "setextinct";
                        example = "?setextinct H. quattuorus \"died of starvation\"\n?setextinct H. quattuorus";
                        break;

                    case "setown":
                    case "claim":
                    case "setowner":
                        description = "Sets the owner of the given species.";
                        aliases = "setowner, setown, claim";
                        example = "?claim H. quattuorus\n?setowner H. quattuorus \"my name\"";
                        break;

                    case "setancestor":
                        description = "Sets the ancestor of the given species (i.e., the species it evolved from).";
                        aliases = "setancestor";
                        example = "?setancestor <derived species> <ancestor species>\n?setancestor H. quattuorus H. ancientous";
                        break;

                    case "setcommon":
                    case "setcommonname":
                        description = "Sets the common name for the given species.";
                        aliases = "setcommonname, setcommon";
                        example = "?setcommonname H. quattuorus \"swirly star\"";
                        break;

                    case "setpredates":
                    case "seteats":
                    case "setprey":
                        description = "Sets a species eaten by another species. Successive calls are additive, and do not replace existing relationships.";
                        aliases = "setprey, seteats, setpredates";
                        example = "?setprey <predator species> <prey species>\n?setprey P. filterarious H. quattuorus\n?setprey P. filterarious H. quattuorus \"babies only\"";
                        break;

                    case "prey":
                        description = "Lists the species prayed upon by the given species.";
                        aliases = "prey";
                        example = "?prey P. filterarious";
                        break;

                    case "eats":
                    case "predates":
                        description = "Lists the species that pray upon the given species.";
                        aliases = "predates, eats";
                        example = "?predates H. quattuorus";
                        break;

                    case "setgenusdescription":
                    case "setgenusdesc":
                    case "setgdesc":
                        description = "Sets the description for the given genus.";
                        aliases = "setgenusdescription, setgenusdesc, setgdesc";
                        example = "?setgdesc helix \"they have swirly shells\"";
                        break;

                    case "ownedby":
                    case "addedby":
                        description = "Lists all species owned by the given user. If no username is provided, lists all species owned by the user who used the command.";
                        aliases = "ownedby, addedby";
                        example = "?ownedby username";
                        break;

                    case "search":
                        description = "Lists species that have names or descriptions matching the search terms.";
                        aliases = "search";
                        example = "?search \"coral\"";
                        break;

                    case "+role":
                    case "setrole":
                        description = "Sets the given species' role.";
                        aliases = "+role, setrole";
                        example = "?+role H. quattuorus detritivore\n?+role H. quattuorus detritivore \"larvae only\"";
                        break;

                    case "-role":
                    case "unsetrole":
                        description = "Removes the given species' role.";
                        aliases = "-role, unsetrole";
                        example = "?-role H. quattuorus predator";
                        break;

                    case "roles":
                    case "role":
                        description = "Lists all roles, shows information about the given role, or lists roles assigned to the given species.";
                        aliases = "roles, role";
                        example = "?roles\n?role predator\n?roles H. quattuorus";
                        break;

                    case "setroledescription":
                    case "setroledesc":
                        description = "Sets the description for the given role.";
                        aliases = "setroledescription, setroledesc";
                        example = "?setroledesc predator \"eats living things\"";
                        break;

                    case "addrole":
                        description = "Adds a new role. To add a role to a species, use `+role` instead.";
                        aliases = "addrole";
                        example = "?addrole predator \"eats living things\"";
                        break;

                    default:
                        await ReplyAsync("No such command exists.");
                        return;

                }

                builder.AddField("Description", description);
                builder.AddField("Aliases", aliases);
                builder.AddField("Example(s)", example);

            }

            await ReplyAsync("", false, builder.Build());

        }

        [Command("setprey"), Alias("seteats", "setpredates")]
        public async Task SetPredates(string genus, string species, string eatsGenus, string eatsSpecies, string notes = "") {

            Species[] predator_list = await BotUtils.GetSpeciesFromDb(genus, species);
            Species[] eaten_list = await BotUtils.GetSpeciesFromDb(eatsGenus, eatsSpecies);

            if (predator_list.Count() == 0)
                await ReplyAsync("The predator species does not exist.");
            else if (eaten_list.Count() == 0)
                await ReplyAsync("The victim species does not exist.");
            else {

                using (SQLiteCommand cmd = new SQLiteCommand("INSERT OR REPLACE INTO Predates(species_id, eats_id, notes) VALUES($species_id, $eats_id, $notes);")) {

                    cmd.Parameters.AddWithValue("$species_id", predator_list[0].id);
                    cmd.Parameters.AddWithValue("$eats_id", eaten_list[0].id);
                    cmd.Parameters.AddWithValue("$notes", notes);

                    await Database.ExecuteNonQuery(cmd);

                }

                await ReplyAsync("Predation updated successfully.");

            }

        }

        [Command("predates"), Alias("eats")]
        public async Task Predates(string genus, string species) {

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() == 0)
                await ReplyAsync("No such species exists.");
            else {

                EmbedBuilder embed = new EmbedBuilder();

                using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Predates WHERE eats_id=$eats_id;")) {

                    cmd.Parameters.AddWithValue("$eats_id", sp_list[0].id);

                    using (DataTable rows = await Database.GetRowsAsync(cmd)) {

                        if (rows.Rows.Count <= 0)
                            await ReplyAsync("This species has no natural predators.");
                        else {

                            List<string> lines = new List<string>();

                            foreach (DataRow row in rows.Rows) {

                                Species sp = await BotUtils.GetSpeciesFromDb(row.Field<long>("species_id"));
                                string notes = row.Field<string>("notes");

                                string line_text = sp.GetShortName();

                                if (!string.IsNullOrEmpty(notes))
                                    line_text += string.Format(" ({0})", notes);

                                lines.Add(line_text);

                            }

                            lines.Sort();

                            embed.WithTitle(string.Format("Predators of {0} ({1})", sp_list[0].GetShortName(), lines.Count()));
                            embed.WithDescription(string.Join(Environment.NewLine, lines));

                            await ReplyAsync("", false, embed.Build());

                        }

                    }

                }

            }

        }

        [Command("prey")]
        public async Task Prey(string genus, string species) {

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() == 0)
                await ReplyAsync("No such species exists.");
            else {

                EmbedBuilder embed = new EmbedBuilder();

                embed.WithTitle(string.Format("Species preyed upon by {0}", sp_list[0].GetShortName()));

                using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Predates WHERE species_id=$species_id;")) {

                    cmd.Parameters.AddWithValue("$species_id", sp_list[0].id);

                    DataTable rows = await Database.GetRowsAsync(cmd);

                    if (rows.Rows.Count <= 0)
                        await ReplyAsync("This species does not prey upon any other species.");
                    else {

                        StringBuilder builder = new StringBuilder();

                        foreach (DataRow row in rows.Rows) {

                            Species sp = await BotUtils.GetSpeciesFromDb(row.Field<long>("eats_id"));
                            string notes = row.Field<string>("notes");

                            builder.Append(sp.GetShortName());

                            if (!string.IsNullOrEmpty(notes))
                                builder.Append(string.Format(" ({0})", notes));

                            builder.AppendLine();

                        }

                        embed.WithDescription(builder.ToString());

                        await ReplyAsync("", false, embed.Build());

                    }

                }

            }

        }

        [Command("setgenusdescription"), Alias("setgenusdesc", "setgdesc")]
        public async Task SetGenusDescription(string genus, string description) {

            Genus genus_info = await BotUtils.GetGenusFromDb(genus);

            if (genus_info is null || genus_info.id == -1) {

                await ReplyAsync("No such genus exists");

                return;

            }
            else {

                using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Genus SET description=$description WHERE id=$genus_id;")) {

                    cmd.Parameters.AddWithValue("$description", description);
                    cmd.Parameters.AddWithValue("$genus_id", genus_info.id);

                    await Database.ExecuteNonQuery(cmd);

                }

                await ReplyAsync("Description added successfully.");
            }

        }

        [Command("setphylum")]
        public async Task SetPhylum(string genus, string species, string phylum) {

            // Get the specified species.

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (sp_list.Count() <= 0) {

                await ReplyAsync("No such species exists.");

                return;

            }

            // Create the phylum if it doesn't already exist.

            phylum = phylum.ToLower();

            using (SQLiteCommand cmd = new SQLiteCommand("INSERT OR IGNORE INTO Phylum(name) VALUES($phylum);")) {

                cmd.Parameters.AddWithValue("$phylum", phylum);

                await Database.ExecuteNonQuery(cmd);

            }

            // Get the ID of the phylum.

            long phylum_id = -1;

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT id FROM Phylum WHERE name=$name;")) {

                cmd.Parameters.AddWithValue("$name", phylum);

                phylum_id = (await Database.GetRowAsync(cmd)).Field<long>("id");

            }

            // Update the species.

            Species sp = sp_list[0];

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Species SET phylum_id=$phylum_id WHERE id=$species_id;")) {

                cmd.Parameters.AddWithValue("$phylum_id", phylum_id);
                cmd.Parameters.AddWithValue("$species_id", sp.id);

                await Database.ExecuteNonQuery(cmd);

            }

            await ReplyAsync("Phylum set successfully.");

        }

        [Command("addedby"), Alias("ownedby")]
        public async Task AddedBy(IUser user = null) {

            if (user is null)
                user = Context.User;

            string username = user.Username;

            // List all species owned by the given user.

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Species WHERE owner=$owner;")) {

                cmd.Parameters.AddWithValue("$owner", username);

                using (DataTable rows = await Database.GetRowsAsync(cmd)) {

                    EmbedBuilder embed = new EmbedBuilder();
                    List<string> lines = new List<string>();

                    foreach (DataRow row in rows.Rows)
                        lines.Add((await Species.FromDataRow(row)).GetShortName());

                    lines.Sort();

                    embed.WithTitle(string.Format("Species owned by {0}", username));
                    embed.WithDescription(string.Join(Environment.NewLine, lines));

                    await ReplyAsync("", false, embed.Build());

                }

            }

        }

        [Command("search")]
        public async Task Search(params string[] terms) {

            if (terms.Count() <= 0) {

                await ReplyAsync("Too few search terms have been provided.");

                return;

            }

            List<Species> list = new List<Species>();

            List<string> term_query_builder = new List<string>();

            for (int i = 0; i < terms.Count(); ++i)
                term_query_builder.Add(string.Format("(name LIKE {0} OR description LIKE {0} OR common_name LIKE {0})", string.Format("$term{0}", i)));

            string query_str = string.Format("SELECT * FROM Species WHERE {0};", string.Join(" AND ", term_query_builder));

            using (SQLiteCommand cmd = new SQLiteCommand(query_str)) {

                // Add all terms to the query.

                for (int i = 0; i < terms.Count(); ++i) {

                    string term = "%" + terms[i].Trim() + "%";

                    cmd.Parameters.AddWithValue(string.Format("$term{0}", i), term);

                }

                using (DataTable rows = await Database.GetRowsAsync(cmd))
                    foreach (DataRow row in rows.Rows)
                        list.Add(await Species.FromDataRow(row));

            }

            SortedSet<string> names_list = new SortedSet<string>();

            foreach (Species sp in list)
                names_list.Add(sp.GetShortName());

            EmbedBuilder embed = new EmbedBuilder();
            embed.WithTitle("Search results");
            embed.WithDescription(string.Join(Environment.NewLine, names_list));

            await ReplyAsync("", false, embed);

        }

        [Command("addrole")]
        public async Task AddRole(string name, string description = "") {

            Role role = new Role {
                name = name,
                description = description
            };

            await BotUtils.AddRoleToDb(role);

            await ReplyAsync("Role added successfully.");

        }

        [Command("+role"), Alias("setrole")]
        public async Task SetRole(string genus, string species, string role, string notes = "") {

            // Get the species.

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (!await BotUtils.ReplyAsync_ValidateSpecies(Context, sp_list))
                return;

            // Get the role.

            Role role_info = await BotUtils.GetRoleFromDb(role);

            if (!await BotUtils.ReplyAsync_ValidateRole(Context, role_info))
                return;

            // Update the species.

            using (SQLiteCommand cmd = new SQLiteCommand("INSERT OR REPLACE INTO SpeciesRoles(species_id, role_id, notes) VALUES($species_id, $role_id, $notes);")) {

                cmd.Parameters.AddWithValue("$species_id", sp_list[0].id);
                cmd.Parameters.AddWithValue("$role_id", role_info.id);
                cmd.Parameters.AddWithValue("$notes", notes);

                await Database.ExecuteNonQuery(cmd);

                await ReplyAsync("Role added successfully.");

            }

        }

        [Command("-role"), Alias("unsetrole")]
        public async Task RemoveRole(string genus, string species, string role) {

            // Get the species.

            Species[] sp_list = await BotUtils.GetSpeciesFromDb(genus, species);

            if (!await BotUtils.ReplyAsync_ValidateSpecies(Context, sp_list))
                return;

            // Get the role.

            Role role_info = await BotUtils.GetRoleFromDb(role);

            if (!await BotUtils.ReplyAsync_ValidateRole(Context, role_info))
                return;

            // Update the species.

            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM SpeciesRoles WHERE species_id=$species_id AND role_id=$role_id;")) {

                cmd.Parameters.AddWithValue("$species_id", sp_list[0].id);
                cmd.Parameters.AddWithValue("$role_id", role_info.id);

                await Database.ExecuteNonQuery(cmd);

                await ReplyAsync("Role removed successfully.");

            }

        }

        [Command("roles"), Alias("role")]
        public async Task Roles(string nameOrGenus = "", string species = "") {

            // If both arguments were left empty, just list all roles.

            if (string.IsNullOrEmpty(nameOrGenus) && string.IsNullOrEmpty(species)) {

                EmbedBuilder embed = new EmbedBuilder();

                Role[] roles_list = await BotUtils.GetRolesFromDb();

                embed.WithTitle(string.Format("All roles ({0})", roles_list.Count()));

                foreach (Role role in roles_list) {

                    long count = 0;

                    using (SQLiteCommand cmd = new SQLiteCommand("SELECT count(*) FROM SpeciesRoles WHERE role_id=$role_id;")) {

                        cmd.Parameters.AddWithValue("$role_id", role.id);

                        count = (await Database.GetRowAsync(cmd)).Field<long>("count(*)");

                    }

                    string title = string.Format("{0} ({1})",
                        StringUtils.ToTitleCase(role.name),
                        count);

                    embed.AddField(title, role.GetDescriptionOrDefault());

                }

                await ReplyAsync("", false, embed.Build());

                return;

            }

            // If only the first argument was provided, show the role with that name.

            if (!string.IsNullOrEmpty(nameOrGenus) && string.IsNullOrEmpty(species)) {

                Role role = await BotUtils.GetRoleFromDb(nameOrGenus);

                if (!await BotUtils.ReplyAsync_ValidateRole(Context, role))
                    return;

                EmbedBuilder embed = new EmbedBuilder();
                embed.WithTitle(string.Format("Role: {0}", StringUtils.ToTitleCase(role.name)));
                embed.WithDescription(role.GetDescriptionOrDefault());

                // List species with this role.

                Species[] species_list = await BotUtils.GetSpeciesFromDbByRole(role);

                if (species_list.Count() > 0) {

                    StringBuilder lines = new StringBuilder();

                    foreach (Species sp in species_list)
                        lines.AppendLine(sp.GetShortName());

                    embed.WithDescription(string.Format("{2}\n\n**Species with this role ({1}):**\n{0}", lines.ToString(), species_list.Count(), role.GetDescriptionOrDefault()));

                }

                await ReplyAsync("", false, embed.Build());

            }

            // If two arguments were provided, take them as a genus and species.
            // We will display the roles assigned to that species.

            if (!string.IsNullOrEmpty(nameOrGenus) && !string.IsNullOrEmpty(species)) {

                // Get the species.

                Species[] sp_list = await BotUtils.GetSpeciesFromDb(nameOrGenus, species);

                if (!await BotUtils.ReplyAsync_ValidateSpecies(Context, sp_list))
                    return;

                // Get the role(s) assigned to this species.

                Role[] roles = await BotUtils.GetRolesFromDbBySpecies(sp_list[0]);

                if (roles.Count() <= 0) {
                    await ReplyAsync("No roles have been assigned to this species.");
                    return;
                }

                // Display the role(s) to the user.

                StringBuilder lines = new StringBuilder();

                foreach (Role i in roles) {

                    lines.Append(StringUtils.ToTitleCase(i.name));

                    if (!string.IsNullOrEmpty(i.notes))
                        lines.Append(string.Format(" ({0})", i.notes));

                    lines.AppendLine();

                }

                EmbedBuilder embed = new EmbedBuilder();

                embed.WithTitle(string.Format("{0}'s role(s) ({1})", sp_list[0].GetShortName(), roles.Count()));
                embed.WithDescription(lines.ToString());

                await ReplyAsync("", false, embed.Build());

            }

        }

        [Command("setroledescription"), Alias("setroledesc")]
        public async Task SetRoleDescription(string name, string description) {

            Role role = await BotUtils.GetRoleFromDb(name);

            if (!await BotUtils.ReplyAsync_ValidateRole(Context, role))
                return;

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Roles SET description=$description WHERE name=$name;")) {

                cmd.Parameters.AddWithValue("$name", role.name);
                cmd.Parameters.AddWithValue("$description", description);

                await Database.ExecuteNonQuery(cmd);

            }

            await ReplyAsync("Set description successfully.");

        }

    }

}