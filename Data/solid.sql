

PRAGMA foreign_keys = ON;

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

INSERT OR IGNORE INTO Applications (Id, ResourceName, ResourceDesc, DtCreation)
VALUES (1, 'app1', 'Sample application 1', '2026-09-25T12:00:00');

INSERT OR IGNORE INTO Channels (Id, ApplicationId, ResourceName, ResourceDesc, DtCreation)
VALUES (1, 1, 'channel1', 'Sample channel 1', '2026-09-25T12:01:00');

INSERT OR IGNORE INTO DataRecords (Id, ChannelId, ResourceName, ContentType, Content, DtCreation, DtExpiration)
VALUES (1, 1, 'dr1', 'text/plain', 'Sample data record 1', '2026-09-25T12:02:00', '');

INSERT OR IGNORE INTO Subscriptions (Id, ChannelId, ResourceName, Evt, Endpoint, DtCreation, DtExpiration)
VALUES (1, 1, 'sub1', 1, 'http://localhost:9000/notify', '2026-09-25T12:03:00', '');