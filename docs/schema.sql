CREATE DATABASE IF NOT EXISTS alerta_cidadao
  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE alerta_cidadao;

CREATE TABLE IF NOT EXISTS Citizens (
  Id INT NOT NULL AUTO_INCREMENT,
  Name VARCHAR(150) NOT NULL,
  Email VARCHAR(254) NOT NULL,
  PasswordHash VARCHAR(512) NOT NULL,
  Municipality VARCHAR(120) NOT NULL,
  MunicipalityIbgeCode INT NULL,
  Neighborhood VARCHAR(120) NULL,
  PostalCode VARCHAR(20) NULL,
  Role VARCHAR(20) NOT NULL DEFAULT 'Citizen',
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NULL,
  PRIMARY KEY (Id), UNIQUE KEY UX_Citizens_Email (Email), KEY IX_Citizens_MunicipalityIbgeCode (MunicipalityIbgeCode)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS OfficialAlerts (
  Id INT NOT NULL AUTO_INCREMENT,
  ExternalIdentifier BIGINT NOT NULL,
  Title VARCHAR(200) NOT NULL,
  Description TEXT NULL,
  EventType VARCHAR(100) NOT NULL,
  Severity VARCHAR(40) NOT NULL,
  Level INT NULL,
  IsFuture BOOLEAN NULL,
  MunicipalityIbgeCode INT NULL,
  AffectedMunicipalityCodes JSON NOT NULL,
  Source VARCHAR(120) NOT NULL DEFAULT 'INMET via RadarMeteorológico',
  SourceUrl VARCHAR(300) NOT NULL DEFAULT 'https://radarmeteorologico.com.br/api-publica',
  StartsAt DATETIME(6) NULL,
  ExpiresAt DATETIME(6) NULL,
  RetrievedAt DATETIME(6) NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NULL,
  PRIMARY KEY (Id), UNIQUE KEY UX_OfficialAlerts_ExternalIdentifier (ExternalIdentifier),
  KEY IX_OfficialAlerts_Municipality_Severity (MunicipalityIbgeCode, Severity)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CitizenOccurrences (
  Id INT NOT NULL AUTO_INCREMENT,
  CitizenId INT NOT NULL,
  Title VARCHAR(200) NOT NULL,
  Description VARCHAR(4000) NOT NULL,
  Type VARCHAR(80) NOT NULL,
  Severity VARCHAR(40) NOT NULL,
  Status VARCHAR(30) NOT NULL DEFAULT 'Open',
  Municipality VARCHAR(120) NOT NULL,
  Neighborhood VARCHAR(120) NULL,
  PostalCode VARCHAR(20) NULL,
  Latitude DECIMAL(10,7) NULL,
  Longitude DECIMAL(10,7) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NULL,
  PRIMARY KEY (Id), KEY IX_CitizenOccurrences_Citizen_Status (CitizenId, Status), KEY IX_CitizenOccurrences_Region_Type_Severity (Municipality, Type, Severity),
  CONSTRAINT FK_CitizenOccurrences_Citizens FOREIGN KEY (CitizenId) REFERENCES Citizens(Id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CitizenNotifications (
  Id INT NOT NULL AUTO_INCREMENT,
  CitizenId INT NOT NULL,
  OfficialAlertId INT NOT NULL,
  Title VARCHAR(200) NOT NULL,
  Message VARCHAR(2000) NOT NULL,
  Status VARCHAR(30) NOT NULL DEFAULT 'Pending',
  CreatedAt DATETIME(6) NOT NULL,
  SentAt DATETIME(6) NULL,
  PRIMARY KEY (Id), UNIQUE KEY UX_CitizenNotifications_Citizen_Alert (CitizenId, OfficialAlertId),
  CONSTRAINT FK_CitizenNotifications_Citizens FOREIGN KEY (CitizenId) REFERENCES Citizens(Id) ON DELETE RESTRICT,
  CONSTRAINT FK_CitizenNotifications_OfficialAlerts FOREIGN KEY (OfficialAlertId) REFERENCES OfficialAlerts(Id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS MonitoringExecutions (
  Id INT NOT NULL AUTO_INCREMENT,
  Provider VARCHAR(100) NOT NULL,
  StartedAt DATETIME(6) NOT NULL,
  FinishedAt DATETIME(6) NULL,
  Status VARCHAR(30) NOT NULL,
  RecordsFound INT NOT NULL DEFAULT 0,
  RecordsCreated INT NOT NULL DEFAULT 0,
  RecordsUpdated INT NOT NULL DEFAULT 0,
  RecordsDuplicated INT NOT NULL DEFAULT 0,
  ErrorMessage VARCHAR(4000) NULL,
  PRIMARY KEY (Id), KEY IX_MonitoringExecutions_StartedAt (StartedAt)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS IdempotencyRecords (
  Id INT NOT NULL AUTO_INCREMENT,
  IdempotencyKey VARCHAR(100) NOT NULL,
  RequestHash CHAR(64) NOT NULL,
  ResponseStatusCode INT NOT NULL,
  ResponseBody LONGTEXT NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  ExpiresAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id), UNIQUE KEY UX_IdempotencyRecords_Key (IdempotencyKey), KEY IX_IdempotencyRecords_ExpiresAt (ExpiresAt)
) ENGINE=InnoDB;
