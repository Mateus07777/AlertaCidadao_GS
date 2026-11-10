CREATE TABLE IF NOT EXISTS alerta_cidadao.Municipalities (
  IbgeCode INT NOT NULL,
  Name VARCHAR(120) NOT NULL,
  Uf CHAR(2) NOT NULL,
  NormalizedName VARCHAR(120) NOT NULL,
  PRIMARY KEY (IbgeCode),
  UNIQUE KEY UX_Municipalities_Uf_NormalizedName (Uf, NormalizedName),
  KEY IX_Municipalities_Name (Name)
) ENGINE=InnoDB;
