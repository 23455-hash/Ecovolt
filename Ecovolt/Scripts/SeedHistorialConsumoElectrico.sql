IF OBJECT_ID('dbo.HistorialConsumoElectrico', 'U') IS NOT NULL
    DROP TABLE dbo.HistorialConsumoElectrico;
GO

CREATE TABLE dbo.HistorialConsumoElectrico
(
    RegistroID INT NOT NULL PRIMARY KEY,
    Dispositivo NVARCHAR(80) NOT NULL,
    Espacio NVARCHAR(80) NOT NULL,
    TipoEspacio NVARCHAR(50) NOT NULL,
    FechaHora DATETIME2 NOT NULL,
    ConsumoKWh DECIMAL(10,3) NOT NULL,
    Amperaje DECIMAL(10,2) NOT NULL,
    Voltaje DECIMAL(10,1) NOT NULL,
    ConsumoWatts DECIMAL(10,2) NOT NULL,
    EstadoConsumo NVARCHAR(20) NOT NULL
);
GO

;WITH n AS
(
    SELECT TOP (200)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS RegistroID
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
),
base AS
(
    SELECT
        RegistroID,
        ((RegistroID - 1) % 5) + 1 AS DeviceId,
        DATEADD(MINUTE, (RegistroID * 17) % 60,
            DATEADD(HOUR,
                CASE ((RegistroID - 1) % 5) + 1
                    WHEN 1 THEN (RegistroID * 3) % 24
                    WHEN 2 THEN 13 + (RegistroID % 6)
                    WHEN 3 THEN CASE WHEN RegistroID % 2 = 0 THEN 19 ELSE 6 END
                    WHEN 4 THEN 8 + (RegistroID % 10)
                    ELSE CASE WHEN RegistroID % 3 = 0 THEN 10 ELSE 18 END
                END,
                DATEADD(DAY, -40 + ((RegistroID - 1) / 5), CAST(CAST(GETDATE() AS DATE) AS DATETIME2))
            )
        ) AS FechaHora
    FROM n
),
calc AS
(
    SELECT
        RegistroID,
        CASE DeviceId
            WHEN 1 THEN N'Refrigerador'
            WHEN 2 THEN N'Aire Acondicionado'
            WHEN 3 THEN N'Iluminación'
            WHEN 4 THEN N'Computadora'
            ELSE N'Lavadora'
        END AS Dispositivo,
        CASE DeviceId
            WHEN 1 THEN N'Cocina'
            WHEN 2 THEN N'Sala'
            WHEN 3 THEN N'Zonas comunes'
            WHEN 4 THEN N'Oficina'
            ELSE N'Lavandería'
        END AS Espacio,
        N'Casa' AS TipoEspacio,
        FechaHora,
        CAST(116.5 + (RegistroID % 16) * 0.45 AS DECIMAL(10,1)) AS Voltaje,
        CAST(
            CASE DeviceId
                WHEN 1 THEN 320 * (0.86 + (RegistroID % 9) * 0.018)
                WHEN 2 THEN 1450 * (1.02 + (RegistroID % 11) * 0.028)
                WHEN 3 THEN 180 * (0.92 + (RegistroID % 7) * 0.055)
                WHEN 4 THEN 260 * (0.88 + (RegistroID % 10) * 0.035)
                ELSE 520 * (0.70 + (RegistroID % 8) * 0.065)
            END AS DECIMAL(10,2)
        ) AS ConsumoWatts,
        CASE DeviceId
            WHEN 1 THEN 460
            WHEN 2 THEN 2100
            WHEN 3 THEN 300
            WHEN 4 THEN 420
            ELSE 900
        END AS ConsumoMaxWatts,
        CASE DeviceId
            WHEN 1 THEN 320
            WHEN 2 THEN 1450
            WHEN 3 THEN 180
            WHEN 4 THEN 260
            ELSE 520
        END AS ConsumoNormalWatts
    FROM base
)
INSERT INTO dbo.HistorialConsumoElectrico
    (RegistroID, Dispositivo, Espacio, TipoEspacio, FechaHora, ConsumoKWh, Amperaje, Voltaje, ConsumoWatts, EstadoConsumo)
SELECT
    RegistroID,
    Dispositivo,
    Espacio,
    TipoEspacio,
    FechaHora,
    CAST(ConsumoWatts / 1000.0 AS DECIMAL(10,3)) AS ConsumoKWh,
    CAST(ConsumoWatts / Voltaje AS DECIMAL(10,2)) AS Amperaje,
    Voltaje,
    ConsumoWatts,
    CASE
        WHEN ConsumoWatts > ConsumoMaxWatts THEN N'Crítico'
        WHEN ConsumoWatts > ConsumoNormalWatts * 1.10 THEN N'Advertencia'
        ELSE N'Normal'
    END AS EstadoConsumo
FROM calc
ORDER BY RegistroID;
GO

SELECT COUNT(*) AS TotalRegistros FROM dbo.HistorialConsumoElectrico;
