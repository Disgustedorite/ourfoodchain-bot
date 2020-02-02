﻿using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain {

    public class Database {

        // Public members

        public static string FilePath { get; } = Constants.DatabaseFilePath;

        public static async Task InitializeAsync() {

            if (_initialized)
                throw new Exception("Database is already initialized.");
            else
                await _initializeAsync();

        }

        public static async Task<SQLiteConnection> GetConnectionAsync() {

            if (!_initialized)
                await _initializeAsync();

            return new SQLiteConnection(_database_connection_string);

        }
        public static async Task<DataRow> GetRowAsync(SQLiteCommand command) {

            using (SQLiteConnection conn = await GetConnectionAsync()) {

                await conn.OpenAsync();

                command.Connection = conn;

                return await GetRowAsync(conn, command);

            }

        }
        public static async Task<DataRow> GetRowAsync(SQLiteConnection conn, SQLiteCommand command) {

            using (DataTable dt = await GetRowsAsync(conn, command))
                if (dt.Rows.Count > 0)
                    return dt.Rows[0];

            return null;

        }
        public static async Task<DataRow> GetRowAsync(SQLiteConnection conn, string query) {

            using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                return await GetRowAsync(conn, cmd);

        }
        public static async Task<DataTable> GetRowsAsync(SQLiteCommand command) {

            using (SQLiteConnection conn = await GetConnectionAsync()) {

                command.Connection = conn;

                return await GetRowsAsync(conn, command);

            }

        }
        public static async Task<DataTable> GetRowsAsync(SQLiteConnection conn, SQLiteCommand command) {

            command.Connection = conn;

            using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(command))
            using (DataTable dt = new DataTable()) {

                await Task.Run(() => adapter.Fill(dt));

                return dt;

            }

        }
        public static async Task<int> ExecuteNonQuery(string query) {

            using (SQLiteCommand cmd = new SQLiteCommand(query))
                return await ExecuteNonQuery(cmd);

        }
        public static async Task<int> ExecuteNonQuery(SQLiteCommand command) {

            using (SQLiteConnection conn = await GetConnectionAsync()) {

                await conn.OpenAsync();

                return await ExecuteNonQuery(conn, command);

            }

        }
        public static async Task<int> ExecuteNonQuery(SQLiteConnection conn, SQLiteCommand command) {

            command.Connection = conn;

            return await command.ExecuteNonQueryAsync();

        }
        public static async Task<T> GetScalar<T>(SQLiteCommand command) {

            using (SQLiteConnection conn = await GetConnectionAsync()) {

                await conn.OpenAsync();

                DataRow row = await GetRowAsync(conn, command);

                return (T)row[0];

            }

        }

        private static readonly string _database_connection_string = string.Format("Data Source={0}", FilePath);
        private static bool _initialized = false;

        private static void _backupDatabase() {

            if (System.IO.File.Exists(FilePath)) {

                System.IO.Directory.CreateDirectory("backups");

                System.IO.File.Copy(FilePath, System.IO.Path.Combine("backups", string.Format("{0}-{1}", DateTimeOffset.Now.ToUnixTimeSeconds(), System.IO.Path.GetFileName(FilePath))));

            }

        }

        private static async Task _initializeAsync() {

            if (_initialized)
                return;

            _initialized = true;

            // Backup the database before performing any operations on it.
            _backupDatabase();

            using (SQLiteConnection conn = await GetConnectionAsync()) {

                await conn.OpenAsync();

                // Load database updater config.

                string config_fname = System.IO.Path.Combine(Constants.DatabaseUpdatesDirectory, "config.json");
                DatabaseUpdaterConfig config = new DatabaseUpdaterConfig();

                if (System.IO.File.Exists(config_fname))
                    config = JsonConvert.DeserializeObject<DatabaseUpdaterConfig>(System.IO.File.ReadAllText(config_fname));
                else
                    Console.WriteLine(new LogMessage(LogSeverity.Warning, "Database", "Database updates config not found").ToString());

                config.UpdatesDirectory = Constants.DatabaseUpdatesDirectory;

                await new DatabaseUpdater(conn, config).UpdateToLatestVersionAsync();

                conn.Close();

            }

        }

    }

}