﻿using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurFoodChain.Gotchi {

    public class GotchiMoveRegistry {

        public static async Task<GotchiMove> GetMoveByNameAsync(string name) {

            if (Registry.Count <= 0)
                await _registerAllMovesAsync();

            if (Registry.TryGetValue(name.ToLower(), out GotchiMove result))
                return result.Clone();

            throw new Exception(string.Format("No move with the name \"{0}\" exists in the registry.", name));

        }

        public static Dictionary<string, GotchiMove> Registry { get; } = new Dictionary<string, GotchiMove>();

        private static void _addMoveToRegistry(GotchiMove move) {

            Registry.Add(move.Name.ToLower(), move);

        }
        private static async Task _registerAllMovesAsync() {

            await OurFoodChainBot.Instance.LogAsync(Discord.LogSeverity.Info, "Gotchi", "Registering moves");

            Registry.Clear();

            await _registerLuaMovesAsync();

            await OurFoodChainBot.Instance.LogAsync(Discord.LogSeverity.Info, "Gotchi", "Registered moves");

        }
        private static async Task _registerLuaMovesAsync() {

            // Create and initialize the script object we'll use for registering all of the moves.
            // The same script object will be used for all moves.

            Script script = new Script();

            LuaUtils.InitializeLuaContext(script);

            // Register all moves.

            foreach (string file in System.IO.Directory.GetFiles(Global.GotchiMovesDirectory, "*.lua", System.IO.SearchOption.TopDirectoryOnly)) {

                try {

                    GotchiMove move = new GotchiMove {
                        LuaScriptFilePath = file
                    };

                    script.DoFile(file);

                    script.Call(script.Globals["register"], move);

                    _addMoveToRegistry(move);

                }
                catch (Exception) {
                    await OurFoodChainBot.Instance.LogAsync(Discord.LogSeverity.Error, "Gotchi", "Failed to register move: " + System.IO.Path.GetFileName(file));
                }

            }

        }

    }

}