﻿using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurFoodChain.Gotchis {

    public class GotchiMoveRegistry {

        // Public members

        public event Func<LogMessage, Task> LogAsync;

        public async Task RegisterAsync(string filePath) {

            try {

                GotchiMove move = new GotchiMove {
                    LuaScriptFilePath = filePath
                };

                if (await new GotchiMoveLuaScript(move.LuaScriptFilePath).OnRegisterAsync(move))
                    _addMoveToRegistry(move);

            }
            catch (Exception) {

                await _logAsync(LogSeverity.Error, string.Format("Failed to register move {0}", System.IO.Path.GetFileName(filePath)));

            }

        }
        public async Task RegisterAllAsync(string directoryPath) {

            if (System.IO.Directory.Exists(directoryPath)) {

                string[] files = System.IO.Directory.GetFiles(directoryPath, "*.lua");

                foreach (string file in files)
                    await RegisterAsync(file);

            }

        }

        public async Task<GotchiMove> GetMoveAsync(string name) {

            GotchiMove move = await _getMoveAsync(name);

            if (move != null)
                return move;
            else
                throw new Exception(string.Format("No move with the name \"{0}\" exists in the registry.", name));

        }

        public async Task<GotchiMove[]> GetLearnSetAsync(Gotchi gotchi) {

            List<GotchiMove> moves = new List<GotchiMove>();

            // all gotchis can use hit regardless of species

            GotchiMove universalMove = await _getMoveAsync("hit");

            if (universalMove != null)
                moves.Add(universalMove);

            foreach (GotchiMove move in Registry.Values)
                if (await new GotchiRequirementsChecker { Requires = move.Requires }.CheckAsync(gotchi))
                    moves.Add(move.Clone());

            return moves.ToArray();

        }
        public async Task<GotchiMoveSet> GetMoveSetAsync(Gotchi gotchi) {

            GotchiMove[] learnSet = await GetLearnSetAsync(gotchi);

            GotchiMoveSet set = new GotchiMoveSet();

            Random rng = new Random((int)gotchi.SpeciesId);

            set.AddRange(learnSet
                .Skip(Math.Max(0, learnSet.Count() - GotchiMoveSet.MoveLimit))
                .OrderBy(x => rng.Next()));

            set.Moves.Sort((lhs, rhs) => lhs.Name.CompareTo(rhs.Name));

            return set;

        }

        // Private members

        private Dictionary<string, GotchiMove> Registry { get; } = new Dictionary<string, GotchiMove>();

        private void _addMoveToRegistry(GotchiMove move) {

            Registry.Add(move.Name.ToLower(), move);

        }

        private async Task<GotchiMove> _getMoveAsync(string name) {

            if (Registry.TryGetValue(name.ToLower(), out GotchiMove result))
                return await Task.FromResult(result.Clone());

            return null;

        }
        private async Task _logAsync(LogSeverity severity, string message) {

            await LogAsync?.Invoke(new LogMessage {
                Source = "Gotchi",
                Severity = severity,
                Message = message
            });

        }

    }

}