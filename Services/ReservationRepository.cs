using Microsoft.Data.Sqlite;
using Studio62Bot.Configuration;
using Studio62Bot.Models;

namespace Studio62Bot.Services;

public sealed class ReservationRepository
{
    private readonly string _connectionString;

    public ReservationRepository(BotSettings settings)
    {
        var dbPath = settings.DatabasePath;
        if (!Path.IsPathRooted(dbPath))
        {
            dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
        }

        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS reservations (
                reservation_id INTEGER PRIMARY KEY AUTOINCREMENT,
                band_role_id INTEGER NOT NULL,
                band_name TEXT NOT NULL,
                reserved_by_user_id INTEGER NOT NULL,
                start_local TEXT NOT NULL,
                end_local TEXT NOT NULL,
                created_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_reservations_start_local ON reservations(start_local);
            CREATE INDEX IF NOT EXISTS idx_reservations_band_role_id ON reservations(band_role_id);
            """;

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Reservation?> FindConflictAsync(DateTime startLocal, DateTime endLocal)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT reservation_id, band_role_id, band_name, reserved_by_user_id, start_local, end_local, created_utc
            FROM reservations
            WHERE @newStart < end_local AND start_local < @newEnd
            ORDER BY start_local
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@newStart", ToStorage(startLocal));
        command.Parameters.AddWithValue("@newEnd", ToStorage(endLocal));

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapReservation(reader);
        }

        return null;
    }

    public async Task<Reservation> CreateAsync(ulong bandRoleId, string bandName, ulong reservedByUserId, DateTime startLocal, DateTime endLocal)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createdUtc = DateTime.UtcNow;
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO reservations (band_role_id, band_name, reserved_by_user_id, start_local, end_local, created_utc)
            VALUES (@bandRoleId, @bandName, @reservedByUserId, @startLocal, @endLocal, @createdUtc);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("@bandRoleId", (long)bandRoleId);
        command.Parameters.AddWithValue("@bandName", bandName);
        command.Parameters.AddWithValue("@reservedByUserId", (long)reservedByUserId);
        command.Parameters.AddWithValue("@startLocal", ToStorage(startLocal));
        command.Parameters.AddWithValue("@endLocal", ToStorage(endLocal));
        command.Parameters.AddWithValue("@createdUtc", ToStorage(createdUtc));

        var reservationId = (long)(await command.ExecuteScalarAsync() ?? 0L);

        return new Reservation
        {
            ReservationId = reservationId,
            BandRoleId = bandRoleId,
            BandName = bandName,
            ReservedByUserId = reservedByUserId,
            StartLocal = startLocal,
            EndLocal = endLocal,
            CreatedUtc = createdUtc
        };
    }

    public async Task<Reservation?> GetByIdAsync(long reservationId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT reservation_id, band_role_id, band_name, reserved_by_user_id, start_local, end_local, created_utc
            FROM reservations
            WHERE reservation_id = @reservationId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@reservationId", reservationId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapReservation(reader);
        }

        return null;
    }

    public async Task<bool> DeleteAsync(long reservationId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM reservations WHERE reservation_id = @reservationId;";
        command.Parameters.AddWithValue("@reservationId", reservationId);

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<IReadOnlyList<Reservation>> GetUpcomingAsync(DateTime nowLocal)
    {
        var results = new List<Reservation>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT reservation_id, band_role_id, band_name, reserved_by_user_id, start_local, end_local, created_utc
            FROM reservations
            WHERE end_local >= @nowLocal
            ORDER BY band_name COLLATE NOCASE, start_local;
            """;
        command.Parameters.AddWithValue("@nowLocal", ToStorage(nowLocal));

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(MapReservation(reader));
        }

        return results;
    }

    private static Reservation MapReservation(SqliteDataReader reader)
    {
        return new Reservation
        {
            ReservationId = reader.GetInt64(0),
            BandRoleId = (ulong)reader.GetInt64(1),
            BandName = reader.GetString(2),
            ReservedByUserId = (ulong)reader.GetInt64(3),
            StartLocal = FromStorage(reader.GetString(4)),
            EndLocal = FromStorage(reader.GetString(5)),
            CreatedUtc = FromStorage(reader.GetString(6))
        };
    }

    private static string ToStorage(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss");
    private static DateTime FromStorage(string value) => DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", null);
}
