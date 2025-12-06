using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    // Declaration now correctly implements IDonationService
    public class DonationService : IDonationService
    {
        private readonly string _databasePath;

        // Selects all 11 columns for consistency across read operations (Index 10 is FirebaseId).
        private const string SelectColumns = @"Id, Title, Category, Description, ContactEmail, Location, ImageData, CreatedDate, IsSynced, UserId, FirebaseId";

        public DonationService()
        {
            _databasePath = Path.Combine(FileSystem.AppDataDirectory, "Donation2.db3");
            InitializeDatabase();
        }

        // Helper property to safely construct the connection string, resolving path/syntax errors.
        private string ConnectionString
        {
            get
            {
                // Use UriBuilder to correctly escape the file path and format it as a file URI
                var uriBuilder = new UriBuilder
                {
                    Path = _databasePath,
                    Scheme = "file"
                };

                // Use SqliteConnectionStringBuilder for guaranteed format compliance
                var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    // FIX: Using AbsolutePath is often more robust than LocalPath in MAUI for Sqlite.
                    DataSource = uriBuilder.Uri.AbsolutePath,
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate
                };
                return builder.ToString();
            }
        }


        private void InitializeDatabase()
        {
            // FIX: Use the safe ConnectionString property
            using var connection = new SqliteConnection(ConnectionString);

            connection.Open();

            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Donations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Description TEXT,
                    ContactEmail TEXT NOT NULL,
                    Location TEXT,
                    ImageData BLOB,
                    CreatedDate TEXT NOT NULL,
                    IsSynced INTEGER NOT NULL DEFAULT 0,
                    UserId TEXT NOT NULL DEFAULT 'default_user',
                    FirebaseId TEXT NULL
                )";
            createTableCommand.ExecuteNonQuery();

            // Logic to ensure UserId column exists for existing databases
            try
            {
                var addUserIdColumnCommand = connection.CreateCommand();
                addUserIdColumnCommand.CommandText = "ALTER TABLE Donations ADD COLUMN UserId TEXT NOT NULL DEFAULT 'default_user'";
                addUserIdColumnCommand.ExecuteNonQuery();
            }
            catch (SqliteException) { }

            // Ensure FirebaseId column exists for older databases
            try
            {
                var addFirebaseIdColumnCommand = connection.CreateCommand();
                addFirebaseIdColumnCommand.CommandText = "ALTER TABLE Donations ADD COLUMN FirebaseId TEXT NULL";
                addFirebaseIdColumnCommand.ExecuteNonQuery();
            }
            catch (SqliteException) { }
        }

        // --- Core CRUD Operations (IDonationService requirements) ---

        public async Task<List<DonationItem>> GetDonationsAsync()
        {
            var donations = new List<DonationItem>();
            // FIX: Use the safe ConnectionString property
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Donations ORDER BY CreatedDate DESC";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                donations.Add(MapDonation(reader));
            }

            return donations;
        }

        public async Task<DonationItem?> GetDonationAsync(int id)
        {
            // FIX: Use the safe ConnectionString property
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Donations WHERE Id = @id";
            command.Parameters.AddWithValue("id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapDonation(reader);
            }

            return null;
        }

        public async Task<int> SaveDonationAsync(DonationItem item)
        {
            // FIX: Use the safe ConnectionString property
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            if (item.Id == 0)
            {
                command.CommandText = $@"
                        INSERT INTO Donations (Title, Category, Description, ContactEmail, Location, ImageData, CreatedDate, IsSynced, UserId, FirebaseId)
                        VALUES (@Title, @Category, @Description, @ContactEmail, @Location, @ImageData, @CreatedDate, @IsSynced, @UserId, @FirebaseId);
                        SELECT last_insert_rowid();";
            }
            else
            {
                command.CommandText = $@"
                        UPDATE Donations
                        SET Title = @Title, Category = @Category, Description = @Description,
                            ContactEmail = @ContactEmail, Location = @Location, ImageData = @ImageData,
                            IsSynced = @IsSynced, UserId = @UserId, FirebaseId = @FirebaseId
                        WHERE Id = @Id";

                command.Parameters.AddWithValue("@Id", item.Id);
            }

            command.Parameters.AddWithValue("@Title", item.Title);
            command.Parameters.AddWithValue("@Category", item.Category);
            command.Parameters.AddWithValue("@Description", (object)item.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ContactEmail", item.ContactEmail);
            command.Parameters.AddWithValue("@Location", (object)item.Location ?? DBNull.Value);
            command.Parameters.AddWithValue("@ImageData", (object)item.ImageData ?? DBNull.Value);
            command.Parameters.AddWithValue("@IsSynced", item.IsSynced ? 1 : 0);
            command.Parameters.AddWithValue("@UserId", item.UserId);
            // Added FirebaseId parameter handling
            command.Parameters.AddWithValue("@FirebaseId", (object)item.FirebaseId ?? DBNull.Value);

            if (item.Id == 0)
            {
                command.Parameters.AddWithValue("@CreatedDate", item.CreatedDate.ToString("O"));
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }

            return await command.ExecuteNonQueryAsync();
        }

        public async Task<int> DeleteDonationAsync(DonationItem item)
        {
            // FIX: Use the safe ConnectionString property
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Donations WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", item.Id);

            return await command.ExecuteNonQueryAsync();
        }

        // --- Filtering Methods ---

        public async Task<List<DonationItem>> GetUserDonationsAsync(string userId)
        {
            var donations = new List<DonationItem>();
            // FIX: Use the safe ConnectionString property
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Donations WHERE UserId = @UserId ORDER BY CreatedDate DESC";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                donations.Add(MapDonation(reader));
            }

            return donations;
        }

        public async Task<List<DonationItem>> GetUnsyncedDonationsAsync()
        {
            var donations = new List<DonationItem>();
            // FIX: Use the safe ConnectionString property
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Donations WHERE IsSynced = 0";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                donations.Add(MapDonation(reader));
            }

            return donations;
        }

        // --- Mapping ---
        private static DonationItem MapDonation(SqliteDataReader reader)
        {
            return new DonationItem
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Category = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                ContactEmail = reader.GetString(4),
                Location = reader.IsDBNull(5) ? null : reader.GetString(5),
                ImageData = reader.IsDBNull(6) ? null : (byte[])reader[6],
                CreatedDate = DateTime.Parse(reader.GetString(7)),
                IsSynced = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
                UserId = reader.IsDBNull(9) ? "default_user" : reader.GetString(9),
                // Reads the 11th column (Index 10) for FirebaseId
                FirebaseId = reader.IsDBNull(10) ? null : reader.GetString(10)
            };
        }
    }
}