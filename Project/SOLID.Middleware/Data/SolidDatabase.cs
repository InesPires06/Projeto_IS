

using Microsoft.Data.Sqlite;
using SOLID.Middleware.Models;

namespace SOLID.Middleware.Data;


public class SolidDatabase
{
    private readonly string _connectionString = "Data Source=solid.db";

    public SolidDatabase()
    {
        InitializeDatabase();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
        pragmaCmd.ExecuteNonQuery();

        return connection;
    }

    private void InitializeDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Applications (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ResourceName TEXT NOT NULL UNIQUE,
                ResourceDesc TEXT NOT NULL,
                DtCreation TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Channels (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ApplicationId INTEGER NOT NULL,
                ResourceName TEXT NOT NULL,
                ResourceDesc TEXT NOT NULL,
                DtCreation TEXT NOT NULL,
                UNIQUE (ApplicationId, ResourceName),
                FOREIGN KEY (ApplicationId) REFERENCES Applications(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS DataRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChannelId INTEGER NOT NULL,
                ResourceName TEXT NOT NULL,
                ContentType TEXT NOT NULL,
                Content TEXT NOT NULL,
                DtCreation TEXT NOT NULL,
                DtExpiration TEXT,
                UNIQUE (ChannelId, ResourceName),
                FOREIGN KEY (ChannelId) REFERENCES Channels(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Subscriptions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChannelId INTEGER NOT NULL,
                ResourceName TEXT NOT NULL,
                Evt INTEGER NOT NULL,
                Endpoint TEXT NOT NULL,
                DtCreation TEXT NOT NULL,
                DtExpiration TEXT,
                UNIQUE (ChannelId, ResourceName),
                FOREIGN KEY (ChannelId) REFERENCES Channels(Id) ON DELETE CASCADE
            );

            -- Registos mínimos obrigatórios (Secção 2.4 do Enunciado)
            INSERT OR IGNORE INTO Applications (Id, ResourceName, ResourceDesc, DtCreation)
            VALUES (1, 'app1', 'Sample application 1', '2026-09-25T12:00:00');

            INSERT OR IGNORE INTO Channels (Id, ApplicationId, ResourceName, ResourceDesc, DtCreation)
            VALUES (1, 1, 'channel1', 'Sample channel 1', '2026-09-25T12:01:00');

            INSERT OR IGNORE INTO DataRecords (Id, ChannelId, ResourceName, ContentType, Content, DtCreation, DtExpiration)
            VALUES (1, 1, 'dr1', 'text/plain', 'Sample data record 1', '2026-09-25T12:02:00', '');

            INSERT OR IGNORE INTO Subscriptions (Id, ChannelId, ResourceName, Evt, Endpoint, DtCreation, DtExpiration)
            VALUES (1, 1, 'sub1', 1, 'http://localhost:9000/notify', '2026-09-25T12:03:00', '');
        ";

        command.ExecuteNonQuery();
    }

    // =========================================================================
    // MÉTODOS AUXILIARES (IDs INTERNOS E UNICIDADE DE NOMES)
    // =========================================================================

    private int? GetApplicationId(SqliteConnection connection, string appName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Applications WHERE ResourceName = @name;";
        cmd.Parameters.AddWithValue("@name", appName);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : null;
    }

    private int? GetChannelId(SqliteConnection connection, int appId, string channelName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT Id
                          FROM Channels
                          WHERE ApplicationId = @appId AND ResourceName = @chanName;";
        cmd.Parameters.AddWithValue("@appId", appId);
        cmd.Parameters.AddWithValue("@chanName", channelName);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : null;
    }

    private string EnsureUniqueAppName(SqliteConnection connection, string? requestedName)
    {
        string baseName = string.IsNullOrWhiteSpace(requestedName) ? "app" : requestedName.Trim();
        string candidate = string.IsNullOrWhiteSpace(requestedName)
            ? $"app-{Guid.NewGuid().ToString("N")[..6]}"
            : requestedName.Trim();

        while (GetApplicationId(connection, candidate) != null)
        {
            candidate = $"{baseName}-{Guid.NewGuid().ToString("N")[..4]}";
        }

        return candidate;
    }

    private string EnsureUniqueChannelName(SqliteConnection connection, int appId, string? requestedName)
    {
        string baseName = string.IsNullOrWhiteSpace(requestedName) ? "channel" : requestedName.Trim();
        string candidate = string.IsNullOrWhiteSpace(requestedName)
            ? $"channel-{Guid.NewGuid().ToString("N")[..6]}"
            : requestedName.Trim();

        while (GetChannelId(connection, appId, candidate) != null)
        {
            candidate = $"{baseName}-{Guid.NewGuid().ToString("N")[..4]}";
        }

        return candidate;
    }

    // =========================================================================
    // CRUD: APPLICATION
    // =========================================================================

    public SolidApplication CreateApplication(SolidApplication input)
    {
        using var connection = OpenConnection();

        string uniqueName = EnsureUniqueAppName(connection, input.ResourceName);
        string dtCreation = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        string desc = input.ResourceDesc ?? string.Empty;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Applications (ResourceName, ResourceDesc, DtCreation)
            VALUES (@name, @desc, @dt);
        ";
        cmd.Parameters.AddWithValue("@name", uniqueName);
        cmd.Parameters.AddWithValue("@desc", desc);
        cmd.Parameters.AddWithValue("@dt", dtCreation);
        cmd.ExecuteNonQuery();

        return new SolidApplication
        {
            ResType = "application",
            ResourceName = uniqueName,
            ResourceDesc = desc,
            DtCreation = dtCreation
        };
    }

    public SolidApplication? GetApplication(string appName)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT ResourceName, ResourceDesc, DtCreation
            FROM Applications
            WHERE ResourceName = @name;
        ";
        cmd.Parameters.AddWithValue("@name", appName);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new SolidApplication
        {
            ResType = "application",
            ResourceName = reader.GetString(0),
            ResourceDesc = reader.GetString(1),
            DtCreation = reader.GetString(2)
        };
    }

    public SolidApplication? UpdateApplication(string currentAppName, SolidApplication input)
    {
        using var connection = OpenConnection();
        var existing = GetApplication(currentAppName);
        if (existing == null)
            return null;

        string newName = existing.ResourceName!;
        if (!string.IsNullOrWhiteSpace(input.ResourceName) && input.ResourceName.Trim() != currentAppName)
        {
            newName = EnsureUniqueAppName(connection, input.ResourceName);
        }

        string newDesc = input.ResourceDesc ?? existing.ResourceDesc ?? string.Empty;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Applications
            SET ResourceName = @newName, ResourceDesc = @newDesc
            WHERE ResourceName = @currentName;
        ";
        cmd.Parameters.AddWithValue("@newName", newName);
        cmd.Parameters.AddWithValue("@newDesc", newDesc);
        cmd.Parameters.AddWithValue("@currentName", currentAppName);
        cmd.ExecuteNonQuery();

        return new SolidApplication
        {
            ResType = "application",
            ResourceName = newName,
            ResourceDesc = newDesc,
            DtCreation = existing.DtCreation
        };
    }

    public bool DeleteApplication(string appName)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Applications WHERE ResourceName = @name;";
        cmd.Parameters.AddWithValue("@name", appName);
        return cmd.ExecuteNonQuery() > 0;
    }

    // =========================================================================
    // CRUD: CHANNEL
    // =========================================================================

    public SolidChannel? CreateChannel(string appName, SolidChannel input)
    {
        using var connection = OpenConnection();
        int? appId = GetApplicationId(connection, appName);
        if (appId == null)
            return null;

        string uniqueName = EnsureUniqueChannelName(connection, appId.Value, input.ResourceName);
        string dtCreation = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        string desc = input.ResourceDesc ?? string.Empty;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Channels (ApplicationId, ResourceName, ResourceDesc, DtCreation)
            VALUES (@appId, @name, @desc, @dt);
        ";
        cmd.Parameters.AddWithValue("@appId", appId.Value);
        cmd.Parameters.AddWithValue("@name", uniqueName);
        cmd.Parameters.AddWithValue("@desc", desc);
        cmd.Parameters.AddWithValue("@dt", dtCreation);
        cmd.ExecuteNonQuery();

        return new SolidChannel
        {
            ResType = "channel",
            ResourceName = uniqueName,
            ResourceDesc = desc,
            DtCreation = dtCreation
        };
    }

    public SolidChannel? GetChannel(string appName, string channelName)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT c.ResourceName, c.ResourceDesc, c.DtCreation
            FROM Channels c
            INNER JOIN Applications a ON c.ApplicationId = a.Id
            WHERE a.ResourceName = @appName AND c.ResourceName = @channelName;
        ";
        cmd.Parameters.AddWithValue("@appName", appName);
        cmd.Parameters.AddWithValue("@channelName", channelName);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new SolidChannel
        {
            ResType = "channel",
            ResourceName = reader.GetString(0),
            ResourceDesc = reader.GetString(1),
            DtCreation = reader.GetString(2)
        };
    }

    public SolidChannel? UpdateChannel(string appName, string currentChannelName, SolidChannel input)
    {
        using var connection = OpenConnection();
        int? appId = GetApplicationId(connection, appName);
        if (appId == null)
            return null;

        var existing = GetChannel(appName, currentChannelName);
        if (existing == null)
            return null;

        string newName = existing.ResourceName!;
        if (!string.IsNullOrWhiteSpace(input.ResourceName) && input.ResourceName.Trim() != currentChannelName)
        {
            newName = EnsureUniqueChannelName(connection, appId.Value, input.ResourceName);
        }

        string newDesc = input.ResourceDesc ?? existing.ResourceDesc ?? string.Empty;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Channels
            SET ResourceName = @newName, ResourceDesc = @newDesc
            WHERE ApplicationId = @appId AND ResourceName = @currentName;
        ";
        cmd.Parameters.AddWithValue("@newName", newName);
        cmd.Parameters.AddWithValue("@newDesc", newDesc);
        cmd.Parameters.AddWithValue("@appId", appId.Value);
        cmd.Parameters.AddWithValue("@currentName", currentChannelName);
        cmd.ExecuteNonQuery();

        return new SolidChannel
        {
            ResType = "channel",
            ResourceName = newName,
            ResourceDesc = newDesc,
            DtCreation = existing.DtCreation
        };
    }

    public bool DeleteChannel(string appName, string channelName)
    {
        using var connection = OpenConnection();
        int? appId = GetApplicationId(connection, appName);
        if (appId == null)
            return false;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM Channels
            WHERE ApplicationId = @appId AND ResourceName = @channelName;
        ";
        cmd.Parameters.AddWithValue("@appId", appId.Value);
        cmd.Parameters.AddWithValue("@channelName", channelName);
        return cmd.ExecuteNonQuery() > 0;
    }

    // =========================================================================
    // OPERAÇÃO DE DESCOBERTA (solid-discovery)
    // =========================================================================

    public List<string> DiscoverFromRoot(string targetResType)
    {
        var paths = new List<string>();
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();

        switch (targetResType)
        {
            case "application":
                cmd.CommandText = "SELECT ResourceName FROM Applications;";
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{r.GetString(0)}");
                break;

            case "channel":
                cmd.CommandText = @"
                    SELECT a.ResourceName, c.ResourceName
                    FROM Channels c
                    INNER JOIN Applications a ON c.ApplicationId = a.Id;";
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{r.GetString(0)}/{r.GetString(1)}");
                break;

            case "data-record":
                cmd.CommandText = @"
                    SELECT a.ResourceName, c.ResourceName, d.ResourceName
                    FROM DataRecords d
                    INNER JOIN Channels c ON d.ChannelId = c.Id
                    INNER JOIN Applications a ON c.ApplicationId = a.Id;";
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{r.GetString(0)}/{r.GetString(1)}/{r.GetString(2)}");
                break;

            case "subscription":
                cmd.CommandText = @"
                    SELECT a.ResourceName, c.ResourceName, s.ResourceName
                    FROM Subscriptions s
                    INNER JOIN Channels c ON s.ChannelId = c.Id
                    INNER JOIN Applications a ON c.ApplicationId = a.Id;";
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{r.GetString(0)}/{r.GetString(1)}/subs/{r.GetString(2)}");
                break;
        }

        return paths;
    }

    public List<string> DiscoverFromApplication(string appName, string targetResType)
    {
        var paths = new List<string>();
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();

        switch (targetResType)
        {
            case "application":
                // Uma aplicação não contém aplicações filhas -> devolve lista vazia
                break;

            case "channel":
                cmd.CommandText = @"
                    SELECT c.ResourceName
                    FROM Channels c
                    INNER JOIN Applications a ON c.ApplicationId = a.Id
                    WHERE a.ResourceName = @appName;";
                cmd.Parameters.AddWithValue("@appName", appName);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{appName}/{r.GetString(0)}");
                break;

            case "data-record":
                cmd.CommandText = @"
                    SELECT c.ResourceName, d.ResourceName
                    FROM DataRecords d
                    INNER JOIN Channels c ON d.ChannelId = c.Id
                    INNER JOIN Applications a ON c.ApplicationId = a.Id
                    WHERE a.ResourceName = @appName;";
                cmd.Parameters.AddWithValue("@appName", appName);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{appName}/{r.GetString(0)}/{r.GetString(1)}");
                break;

            case "subscription":
                cmd.CommandText = @"
                    SELECT c.ResourceName, s.ResourceName
                    FROM Subscriptions s
                    INNER JOIN Channels c ON s.ChannelId = c.Id
                    INNER JOIN Applications a ON c.ApplicationId = a.Id
                    WHERE a.ResourceName = @appName;";
                cmd.Parameters.AddWithValue("@appName", appName);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{appName}/{r.GetString(0)}/subs/{r.GetString(1)}");
                break;
        }

        return paths;
    }

    public List<string> DiscoverFromChannel(string appName, string channelName, string targetResType)
    {
        var paths = new List<string>();
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();

        switch (targetResType)
        {
            case "application":
            case "channel":
                // Um canal não contém aplicações nem canais filhos -> devolve lista vazia
                break;

            case "data-record":
                cmd.CommandText = @"
                    SELECT d.ResourceName
                    FROM DataRecords d
                    INNER JOIN Channels c ON d.ChannelId = c.Id
                    INNER JOIN Applications a ON c.ApplicationId = a.Id
                    WHERE a.ResourceName = @appName AND c.ResourceName = @channelName;";
                cmd.Parameters.AddWithValue("@appName", appName);
                cmd.Parameters.AddWithValue("@channelName", channelName);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{appName}/{channelName}/{r.GetString(0)}");
                break;

            case "subscription":
                cmd.CommandText = @"
                    SELECT s.ResourceName
                    FROM Subscriptions s
                    INNER JOIN Channels c ON s.ChannelId = c.Id
                    INNER JOIN Applications a ON c.ApplicationId = a.Id
                    WHERE a.ResourceName = @appName AND c.ResourceName = @channelName;";
                cmd.Parameters.AddWithValue("@appName", appName);
                cmd.Parameters.AddWithValue("@channelName", channelName);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        paths.Add($"/api/solid/{appName}/{channelName}/subs/{r.GetString(0)}");
                break;
        }

        return paths;
    }


    // =========================================================================
    // DATA-RECORDS 
    // =========================================================================

    public SolidDataRecord? CreateDataRecord(string appName, string channelName, SolidDataRecord input)
    {
        using var conn = OpenConnection();
        var appId = GetApplicationId(conn, appName);
        if (appId == null) return null;

        var chanId = GetChannelId(conn, appId.Value, channelName);
        if (chanId == null) return null;

        string name = string.IsNullOrWhiteSpace(input.ResourceName) ? $"dr-{Guid.NewGuid().ToString("N")[..6]}" : input.ResourceName;
        string dt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        string contentType = input.ContentType ?? "text/plain";
        string content = input.Content ?? "";

        string sql =
         @"INSERT INTO DataRecords (ChannelId, ResourceName, ContentType, Content, DtCreation, DtExpiration) 
           VALUES (@chanId, @name, @type, @content, @dt, @exp)";
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@chanId", chanId.Value);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@type", contentType);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@dt", dt);
        cmd.Parameters.AddWithValue("@exp", (object?)input.DtExpiration ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        return new SolidDataRecord
        {
            ResType = "data-record",
            ResourceName = name,
            ContentType = contentType,
            Content = content,
            DtCreation = dt,
            DtExpiration = input.DtExpiration
        };
    }

    public SolidDataRecord? GetDataRecord(string appName, string channelName, string drName)
    {
        using var conn = OpenConnection();
        string sql =
        @"SELECT dr.ResourceName, dr.ContentType, dr.Content, dr.DtCreation, dr.DtExpiration 
        FROM DataRecords dr JOIN Channels c ON dr.ChannelId = c.Id 
        JOIN Applications a ON c.ApplicationId = a.Id 
        WHERE a.ResourceName = @app
         AND c.ResourceName = @chan 
         AND dr.ResourceName = @dr";
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@app", appName);
        cmd.Parameters.AddWithValue("@chan", channelName);
        cmd.Parameters.AddWithValue("@dr", drName);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new SolidDataRecord
            {
                ResType = "data-record",
                ResourceName = reader.GetString(0),
                ContentType = reader.GetString(1),
                Content = reader.GetString(2),
                DtCreation = reader.GetString(3),
                DtExpiration = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    public bool DeleteDataRecord(string appName, string channelName, string drName)
    {
        using var conn = OpenConnection();
        var appId = GetApplicationId(conn, appName);
        if (appId == null) return false;

        var chanId = GetChannelId(conn, appId.Value, channelName);
        if (chanId == null) return false;

        string sql = @"DELETE FROM DataRecords WHERE ResourceName = @dr AND ChannelId = @chanId";
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@dr", drName);
        cmd.Parameters.AddWithValue("@chanId", chanId.Value);
        return cmd.ExecuteNonQuery() > 0;
    }

    // =========================================================================
    // SUBSCRIPTIONS
    // =========================================================================


    public SolidSubscription? CreateSubscription(string appName, string channelName, SolidSubscription input)
    {
        using var conn = OpenConnection();
        int? appId = GetApplicationId(conn, appName);
        if (appId == null) return null;

        int? chanId = GetChannelId(conn, appId.Value, channelName);
        if (chanId == null) return null;

        string name = string.IsNullOrWhiteSpace(input.ResourceName) ? $"sub-{Guid.NewGuid().ToString("N")[..6]}" : input.ResourceName;
        string dt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        string sql =
        @"INSERT INTO Subscriptions (ChannelId, ResourceName, Evt, Endpoint, DtCreation, DtExpiration) 
          VALUES (@chanId, @name, @evt, @endpoint, @dt, @exp)";
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@chanId", chanId.Value);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@evt", input.Evt);
        cmd.Parameters.AddWithValue("@endpoint", input.Endpoint);
        cmd.Parameters.AddWithValue("@dt", dt);
        cmd.Parameters.AddWithValue("@exp", (object?)input.DtExpiration ?? DBNull.Value); cmd.ExecuteNonQuery();
        return new SolidSubscription
        {
            ResType = "subscription",
            ResourceName = name,
            Evt = input.Evt,
            Endpoint = input.Endpoint,
            DtCreation = dt,
            DtExpiration = input.DtExpiration
        };
    }


    public SolidSubscription? GetSubscription(string appName, string channelName, string subName)
    {
        using var conn = OpenConnection();
        string sql =
        @"SELECT s.ResourceName, s.Evt, s.Endpoint, s.DtCreation, s.DtExpiration 
          FROM Subscriptions s 
          JOIN Channels c ON s.ChannelId = c.Id 
          JOIN Applications a ON c.ApplicationId = a.Id 
          WHERE a.ResourceName = @app AND c.ResourceName = @chan 
          AND s.ResourceName = @sub";
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@app", appName);
        cmd.Parameters.AddWithValue("@chan", channelName);
        cmd.Parameters.AddWithValue("@sub", subName);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new SolidSubscription
            {
                ResType = "subscription",
                ResourceName = reader.GetString(0),
                Evt = reader.GetInt32(1),
                Endpoint = reader.GetString(2),
                DtCreation = reader.GetString(3),
                DtExpiration = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    public bool DeleteSubscription(string appName, string channelName, string subName)
    {
        using var conn = OpenConnection();
        int? appId = GetApplicationId(conn, appName);
        if (appId == null) return false;

        int? chanId = GetChannelId(conn, appId.Value, channelName);
        if (chanId == null) return false;
        
        string sql =
        @"DELETE FROM Subscriptions
           WHERE ResourceName = @sub AND ChannelId = (@chanId)";
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sub", subName);
        cmd.Parameters.AddWithValue("@chanId", chanId.Value);    
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<SolidSubscription> GetActiveSubscriptions(string appName, string channelName, int evtType)
    {
        var list = new List<SolidSubscription>();
        using var conn = OpenConnection();


        string sql =
        @"SELECT s.ResourceName, s.Evt, s.Endpoint, s.DtCreation, s.DtExpiration 
        FROM Subscriptions s 
        JOIN Channels c ON s.ChannelId = c.Id 
        JOIN Applications a ON c.ApplicationId = a.Id 
        WHERE a.ResourceName = @app 
        AND c.ResourceName = @chan 
        AND s.Evt = @evt";

        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@app", appName);
        cmd.Parameters.AddWithValue("@chan", channelName);
        cmd.Parameters.AddWithValue("@evt", evtType);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SolidSubscription
            {
                ResType = "subscription",
                ResourceName = reader.GetString(0),
                Evt = reader.GetInt32(1),
                Endpoint = reader.GetString(2),
                DtCreation = reader.GetString(3),
                DtExpiration = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return list;
    }
}