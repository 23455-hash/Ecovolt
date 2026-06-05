// ═══════════════════════════════════════════════════════════════════════════
//  EcoVolt — DatabaseService.cs  (VERSIÓN CORREGIDA)
//  Carpeta: Models\DatabaseService.cs
//  Correcciones aplicadas:
//    • Se eliminaron PrediccionItem y DatoDiario de este archivo
//      (ya están definidas en Models\PrediccionItem.cs).
//    • Compatibilidad con C# 7.3 (.NET Framework):
//      - switch expression → switch statement
//      - string interpolation → string.Format donde aplica
//    • Sin referencias a CharacterSpacing (es UWP, no WPF).
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace EcoVolt.Models
{
    // ═══════════════════════════════════════════════════════════════════════
    // MODELOS DE DATOS  (solo los que NO están en PrediccionItem.cs)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Resumen de consumo por dispositivo — barras del Dashboard.</summary>
    public class DeviceConsumption
    {
        public string Dispositivo { get; set; }
        public double ConsumoKWh { get; set; }
        public double Porcentaje { get; set; }
        public string Color { get; set; }
        public string Espacio { get; set; }
        public string Estado { get; set; }
        public double PromedioWatts { get; set; }
        public double ConsumoNormalWatts { get; set; }
    }

    /// <summary>Consumo promedio por hora del día.</summary>
    public class HourlyData
    {
        public int Hora { get; set; }
        public double Consumo { get; set; }
        public string HoraLabel { get { return string.Format("{0:00}:00", Hora); } }
    }

    /// <summary>Ítem de alerta para las tarjetas del panel Alertas.</summary>
    public class AlertaItem
    {
        public string Tipo { get; set; }  // "warning" | "info" | "success" | "critical"
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Hora { get; set; }
        public string Fecha { get; set; }
        public string Equipo { get; set; }
        public string Espacio { get; set; }
        public bool Resuelta { get; set; }
    }

    /// <summary>KPIs principales del Dashboard.</summary>
    public class KpiData
    {
        public double PotenciaKW { get; set; }
        public double ConsumoHoyKWh { get; set; }
        public double ConsumoKWh { get; set; }
        public double CostoMesCOP { get; set; }
        public double PromedioWatts { get; set; }
        public int DispositivosActivos { get; set; }
        public int AlertasActivas { get; set; }
        public string EstadoGeneral { get; set; }
        public string ColorEstado { get; set; }
    }

    /// <summary>Fila del historial — Registro_Consumo con JOINs.</summary>
    public class RegistroConsumoRow
    {
        public int RegistroID { get; set; }
        public string Equipo { get; set; }
        public string Espacio { get; set; }
        public string TipoEspacio { get; set; }
        public string Fecha { get; set; }
        public string Hora { get; set; }
        public string FechaHora { get; set; }
        public double Voltaje { get; set; }
        public double ConsumoWatts { get; set; }
        public double ConsumoNormalWatts { get; set; }
        public string EstadoConsumo { get; set; }
        public string ColorEstado
        {
            get
            {
                if (EstadoConsumo == "Crítico") return "#EF4444";
                if (EstadoConsumo == "Advertencia") return "#F97316";
                return "#32A76B";
            }
        }
    }

    /// <summary>Fila de la tabla Equipos con JOIN a Espacios.</summary>
    public class EquipoRow
    {
        public int EquipoID { get; set; }
        public string Nombre { get; set; }
        public string Espacio { get; set; }
        public string TipoEspacio { get; set; }
        public double ConsumoNormalWatts { get; set; }
        public double? ConsumoMaxWatts { get; set; }
        public string Estado { get; set; }
        public string ColorEstado
        {
            get
            {
                if (Estado == "Activo") return "#32A76B";
                if (Estado == "Mantenimiento") return "#F97316";
                return "#7D8590";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SERVICIO DE BASE DE DATOS
    // ═══════════════════════════════════════════════════════════════════════
    public static class DatabaseService
    {
        // ─── CADENA DE CONEXIÓN ───────────────────────────────────────────
        private static readonly string ConnectionString =
            @"Data Source=.\SQLEXPRESS;" +
            @"Initial Catalog=Consumo_Electrico_EcoVolt;" +
            @"Integrated Security=True;" +
            @"TrustServerCertificate=True;" +
            @"Connect Timeout=5;";

        // ─── PALETA DE COLORES ────────────────────────────────────────────
        private static readonly string[] PaletaColores = new[]
        {
            "#308BB1", "#32A76B", "#9B59B6", "#F39C12",
            "#E91E63", "#32A6C5", "#63C768", "#FF6B35",
            "#3498DB", "#E74C3C", "#1ABC9C", "#F1C40F"
        };

        // ─── HELPER: periodo → DATEADD string ────────────────────────────
        private static string GetPeriodoHoras(string periodo)
        {
            switch (periodo)
            {
                case "hoy": return "DATEADD(HOUR, -24, @fechaMax)";
                case "semana": return "DATEADD(DAY,  -7,  @fechaMax)";
                case "mes": return "DATEADD(DAY,  -30, @fechaMax)";
                case "anio": return "DATEADD(DAY, -365, @fechaMax)";
                default: return "DATEADD(HOUR, -24, @fechaMax)";
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // TEST DE CONEXIÓN
        // ═══════════════════════════════════════════════════════════════════
        public static bool TestConexion(out string error)
        {
            error = string.Empty;
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // KPIs PRINCIPALES
        // ═══════════════════════════════════════════════════════════════════
        public static KpiData GetKpis(string periodo = "hoy")
        {
            string dbErr;
            if (!TestConexion(out dbErr))
                return GetKpisMock();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        DECLARE @fechaMax DATETIME;
                        SELECT @fechaMax = MAX(FechaHora) FROM Registro_Consumo;

                        DECLARE @inicio DATETIME;
                        IF @Periodo = 'hoy'
                            SET @inicio = DATEADD(HOUR, -24, @fechaMax);
                        ELSE IF @Periodo = 'semana'
                            SET @inicio = DATEADD(DAY, -7, @fechaMax);
                        ELSE IF @Periodo = 'mes'
                            SET @inicio = DATEADD(DAY, -30, @fechaMax);
                        ELSE IF @Periodo = 'anio'
                            SET @inicio = DATEADD(DAY, -365, @fechaMax);
                        ELSE
                            SET @inicio = DATEADD(HOUR, -24, @fechaMax);

                        SELECT
                            ISNULL(MAX(ConsumoWatts) / 1000.0, 0)         AS PotenciaKW,
                            ISNULL(SUM(ConsumoWatts) / 1000.0, 0)         AS ConsumoKWh,
                            ISNULL(SUM(ConsumoWatts) * 800.0 / 1000.0, 0) AS CostoMesCOP,
                            ISNULL(AVG(ConsumoWatts), 0)                   AS PromedioWatts,
                            (SELECT COUNT(*) FROM Equipos WHERE Estado = 'Activo')  AS DispositivosActivos,
                            (SELECT COUNT(*) FROM Alertas
                             WHERE Resuelta = 0
                               AND FechaGeneracion >= @inicio)              AS AlertasActivas
                        FROM Registro_Consumo
                        WHERE FechaHora BETWEEN @inicio AND @fechaMax;";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Periodo", periodo);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                double potencia = Convert.ToDouble(rdr["PotenciaKW"]);
                                double consumo = Convert.ToDouble(rdr["ConsumoKWh"]);
                                double costo = Convert.ToDouble(rdr["CostoMesCOP"]);
                                double promedio = Convert.ToDouble(rdr["PromedioWatts"]);
                                int dispositivos = Convert.ToInt32(rdr["DispositivosActivos"]);
                                int alertas = Convert.ToInt32(rdr["AlertasActivas"]);

                                string estado;
                                string colorEstado;
                                if (alertas > 5)
                                {
                                    estado = "Crítico";
                                    colorEstado = "#EF4444";
                                }
                                else if (alertas > 2)
                                {
                                    estado = "Advertencia";
                                    colorEstado = "#F97316";
                                }
                                else
                                {
                                    estado = "Normal";
                                    colorEstado = "#32A76B";
                                }

                                return new KpiData
                                {
                                    PotenciaKW = potencia,
                                    ConsumoKWh = consumo,
                                    ConsumoHoyKWh = consumo,
                                    CostoMesCOP = costo,
                                    PromedioWatts = promedio,
                                    DispositivosActivos = dispositivos,
                                    AlertasActivas = alertas,
                                    EstadoGeneral = estado,
                                    ColorEstado = colorEstado
                                };
                            }
                        }
                    }
                }
            }
            catch { }

            return GetKpisMock();
        }

        // ═══════════════════════════════════════════════════════════════════
        // CONSUMO POR DISPOSITIVO — Top 10
        // ═══════════════════════════════════════════════════════════════════
        public static List<DeviceConsumption> GetConsumoDispositivos(string periodo = "hoy")
        {
            string dbErr;
            if (!TestConexion(out dbErr))
                return GetDispositivosMock();

            var result = new List<DeviceConsumption>();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        DECLARE @fechaMax DATETIME;
                        SELECT @fechaMax = MAX(FechaHora) FROM Registro_Consumo;

                        DECLARE @inicio DATETIME;
                        IF @Periodo = 'hoy'
                            SET @inicio = DATEADD(HOUR, -24, @fechaMax);
                        ELSE IF @Periodo = 'semana'
                            SET @inicio = DATEADD(DAY, -7, @fechaMax);
                        ELSE IF @Periodo = 'mes'
                            SET @inicio = DATEADD(DAY, -30, @fechaMax);
                        ELSE IF @Periodo = 'anio'
                            SET @inicio = DATEADD(DAY, -365, @fechaMax);
                        ELSE
                            SET @inicio = DATEADD(HOUR, -24, @fechaMax);

                        SELECT TOP 10
                            e.Nombre                      AS Dispositivo,
                            es.Nombre                     AS Espacio,
                            e.Estado,
                            e.ConsumoNormalWatts,
                            SUM(rc.ConsumoWatts) / 1000.0 AS ConsumoKWh,
                            AVG(rc.ConsumoWatts)           AS PromedioWatts
                        FROM Registro_Consumo rc
                        INNER JOIN Equipos  e  ON rc.EquipoID  = e.EquipoID
                        INNER JOIN Espacios es ON e.EspacioID  = es.EspacioID
                        WHERE rc.FechaHora BETWEEN @inicio AND @fechaMax
                        GROUP BY e.EquipoID, e.Nombre, es.Nombre, e.Estado, e.ConsumoNormalWatts
                        ORDER BY ConsumoKWh DESC;";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Periodo", periodo);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            int idx = 0;
                            while (rdr.Read())
                            {
                                result.Add(new DeviceConsumption
                                {
                                    Dispositivo = rdr["Dispositivo"].ToString(),
                                    Espacio = rdr["Espacio"].ToString(),
                                    Estado = rdr["Estado"].ToString(),
                                    ConsumoNormalWatts = rdr["ConsumoNormalWatts"] != DBNull.Value
                                                         ? Convert.ToDouble(rdr["ConsumoNormalWatts"]) : 0,
                                    ConsumoKWh = rdr["ConsumoKWh"] != DBNull.Value
                                                         ? Convert.ToDouble(rdr["ConsumoKWh"]) : 0,
                                    PromedioWatts = rdr["PromedioWatts"] != DBNull.Value
                                                         ? Convert.ToDouble(rdr["PromedioWatts"]) : 0,
                                    Color = PaletaColores[idx % PaletaColores.Length]
                                });
                                idx++;
                            }
                        }
                    }

                    double total = 0;
                    foreach (var d in result) total += d.ConsumoKWh;
                    foreach (var d in result)
                        d.Porcentaje = total > 0 ? d.ConsumoKWh / total * 100 : 0;
                }
            }
            catch
            {
                return GetDispositivosMock();
            }

            return result.Count > 0 ? result : GetDispositivosMock();
        }

        // ═══════════════════════════════════════════════════════════════════
        // HISTORIAL DE CONSUMO — hasta 500 registros
        // ═══════════════════════════════════════════════════════════════════
        public static List<RegistroConsumoRow> GetRegistroConsumo(
            string periodo = "semana", string filtroEquipo = null)
        {
            string dbErr;
            if (!TestConexion(out dbErr))
                return GetRegistroMock();

            var result = new List<RegistroConsumoRow>();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    string filtroNombre = string.IsNullOrEmpty(filtroEquipo)
                        ? "" : "AND e.Nombre LIKE @FiltroEquipo";

                    string query = string.Format(@"
                        DECLARE @fechaMax DATETIME;
                        SELECT @fechaMax = MAX(FechaHora) FROM Registro_Consumo;

                        DECLARE @inicio DATETIME;
                        IF @Periodo = 'hoy'
                            SET @inicio = DATEADD(HOUR, -24, @fechaMax);
                        ELSE IF @Periodo = 'semana'
                            SET @inicio = DATEADD(DAY, -7, @fechaMax);
                        ELSE IF @Periodo = 'mes'
                            SET @inicio = DATEADD(DAY, -30, @fechaMax);
                        ELSE IF @Periodo = 'anio'
                            SET @inicio = DATEADD(DAY, -365, @fechaMax);
                        ELSE
                            SET @inicio = DATEADD(DAY, -7, @fechaMax);

                        SELECT TOP 500
                            rc.RegistroID,
                            e.Nombre                                 AS Equipo,
                            es.Nombre                                AS Espacio,
                            es.Tipo                                  AS TipoEspacio,
                            FORMAT(rc.FechaHora, 'dd/MM/yyyy')       AS Fecha,
                            FORMAT(rc.FechaHora, 'HH:mm')            AS Hora,
                            FORMAT(rc.FechaHora, 'dd/MM/yyyy HH:mm') AS FechaHora,
                            rc.Voltaje,
                            rc.ConsumoWatts,
                            e.ConsumoNormalWatts,
                            CASE
                                WHEN rc.ConsumoWatts > ISNULL(e.ConsumoMaxWatts, e.ConsumoNormalWatts * 1.30)
                                    THEN 'Crítico'
                                WHEN rc.ConsumoWatts > e.ConsumoNormalWatts * 1.10
                                    THEN 'Advertencia'
                                ELSE 'Normal'
                            END AS EstadoConsumo
                        FROM Registro_Consumo rc
                        INNER JOIN Equipos  e  ON rc.EquipoID  = e.EquipoID
                        INNER JOIN Espacios es ON e.EspacioID  = es.EspacioID
                        WHERE rc.FechaHora BETWEEN @inicio AND @fechaMax
                        {0}
                        ORDER BY rc.FechaHora DESC;", filtroNombre);

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Periodo", periodo);
                        if (!string.IsNullOrEmpty(filtroEquipo))
                            cmd.Parameters.AddWithValue("@FiltroEquipo", "%" + filtroEquipo + "%");

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                result.Add(new RegistroConsumoRow
                                {
                                    RegistroID = Convert.ToInt32(rdr["RegistroID"]),
                                    Equipo = rdr["Equipo"].ToString(),
                                    Espacio = rdr["Espacio"].ToString(),
                                    TipoEspacio = rdr["TipoEspacio"].ToString(),
                                    Fecha = rdr["Fecha"].ToString(),
                                    Hora = rdr["Hora"].ToString(),
                                    FechaHora = rdr["FechaHora"].ToString(),
                                    Voltaje = rdr["Voltaje"] != DBNull.Value
                                                         ? Convert.ToDouble(rdr["Voltaje"]) : 0,
                                    ConsumoWatts = rdr["ConsumoWatts"] != DBNull.Value
                                                         ? Convert.ToDouble(rdr["ConsumoWatts"]) : 0,
                                    ConsumoNormalWatts = rdr["ConsumoNormalWatts"] != DBNull.Value
                                                         ? Convert.ToDouble(rdr["ConsumoNormalWatts"]) : 0,
                                    EstadoConsumo = rdr["EstadoConsumo"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                return GetRegistroMock();
            }

            return result.Count > 0 ? result : GetRegistroMock();
        }

        // ═══════════════════════════════════════════════════════════════════
        // EQUIPOS — Todos con JOIN a Espacios
        // ═══════════════════════════════════════════════════════════════════
        public static List<EquipoRow> GetEquipos()
        {
            string dbErr;
            if (!TestConexion(out dbErr))
                return GetEquiposMock();

            var result = new List<EquipoRow>();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT
                            eq.EquipoID,
                            eq.Nombre,
                            es.Nombre  AS Espacio,
                            es.Tipo    AS TipoEspacio,
                            eq.ConsumoNormalWatts,
                            eq.ConsumoMaxWatts,
                            eq.Estado
                        FROM Equipos eq
                        INNER JOIN Espacios es ON eq.EspacioID = es.EspacioID
                        ORDER BY eq.Estado, eq.Nombre;";

                    using (var cmd = new SqlCommand(query, conn))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            result.Add(new EquipoRow
                            {
                                EquipoID = Convert.ToInt32(rdr["EquipoID"]),
                                Nombre = rdr["Nombre"].ToString(),
                                Espacio = rdr["Espacio"].ToString(),
                                TipoEspacio = rdr["TipoEspacio"].ToString(),
                                ConsumoNormalWatts = rdr["ConsumoNormalWatts"] != DBNull.Value
                                                     ? Convert.ToDouble(rdr["ConsumoNormalWatts"]) : 0,
                                ConsumoMaxWatts = rdr["ConsumoMaxWatts"] != DBNull.Value
                                                     ? (double?)Convert.ToDouble(rdr["ConsumoMaxWatts"]) : null,
                                Estado = rdr["Estado"].ToString()
                            });
                        }
                    }
                }
            }
            catch
            {
                return GetEquiposMock();
            }

            return result.Count > 0 ? result : GetEquiposMock();
        }

        // ═══════════════════════════════════════════════════════════════════
        // ALERTAS — Con detalle de equipo y espacio
        // ═══════════════════════════════════════════════════════════════════
        public static List<AlertaItem> GetAlertas(string periodo = "semana")
        {
            string dbErr;
            if (!TestConexion(out dbErr))
                return GetAlertasMock();

            var result = new List<AlertaItem>();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        DECLARE @fechaMax DATETIME;
                        SELECT @fechaMax = MAX(FechaGeneracion) FROM Alertas;

                        DECLARE @inicio DATETIME;
                        IF @Periodo = 'hoy'
                            SET @inicio = DATEADD(HOUR, -24, @fechaMax);
                        ELSE IF @Periodo = 'semana'
                            SET @inicio = DATEADD(DAY, -7, @fechaMax);
                        ELSE IF @Periodo = 'mes'
                            SET @inicio = DATEADD(DAY, -30, @fechaMax);
                        ELSE
                            SET @inicio = DATEADD(DAY, -7, @fechaMax);

                        SELECT TOP 50
                            a.TipoAlerta,
                            a.NivelSeveridad,
                            ISNULL(a.Descripcion, '')                AS Descripcion,
                            a.Resuelta,
                            FORMAT(a.FechaGeneracion, 'dd/MM/yyyy') AS FechaAlerta,
                            FORMAT(a.FechaGeneracion, 'HH:mm')      AS HoraAlerta,
                            e.Nombre                                 AS Equipo,
                            es.Nombre                                AS Espacio
                        FROM Alertas a
                        INNER JOIN Registro_Consumo rc ON a.RegistroID = rc.RegistroID
                        INNER JOIN Equipos  e          ON rc.EquipoID  = e.EquipoID
                        INNER JOIN Espacios es         ON e.EspacioID  = es.EspacioID
                        WHERE a.FechaGeneracion BETWEEN @inicio AND @fechaMax
                        ORDER BY a.FechaGeneracion DESC;";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Periodo", periodo);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string nivel = rdr["NivelSeveridad"].ToString();
                                string tipo;
                                if (nivel == "Alta")
                                    tipo = "warning";
                                else if (nivel == "Media")
                                    tipo = "info";
                                else
                                    tipo = "success";

                                string desc = rdr["Descripcion"].ToString();
                                if (string.IsNullOrEmpty(desc))
                                    desc = "Equipo: " + rdr["Equipo"].ToString()
                                           + " — Severidad: " + nivel;

                                result.Add(new AlertaItem
                                {
                                    Tipo = tipo,
                                    Titulo = rdr["TipoAlerta"].ToString(),
                                    Descripcion = desc,
                                    Hora = rdr["HoraAlerta"].ToString(),
                                    Fecha = rdr["FechaAlerta"].ToString(),
                                    Equipo = rdr["Equipo"].ToString(),
                                    Espacio = rdr["Espacio"].ToString(),
                                    Resuelta = Convert.ToBoolean(rdr["Resuelta"])
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                return GetAlertasMock();
            }

            return result.Count > 0 ? result : GetAlertasMock();
        }

        // ═══════════════════════════════════════════════════════════════════
        // CONSUMO HORARIO — Promedio por hora del día
        // ═══════════════════════════════════════════════════════════════════
        public static List<HourlyData> GetConsumoHorario()
        {
            string dbErr;
            if (!TestConexion(out dbErr))
                return GetHorarioMock();

            var result = new List<HourlyData>();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT
                            DATEPART(HOUR, FechaHora)  AS Hora,
                            AVG(ConsumoWatts) / 1000.0 AS Consumo
                        FROM Registro_Consumo
                        GROUP BY DATEPART(HOUR, FechaHora)
                        ORDER BY Hora;";

                    using (var cmd = new SqlCommand(query, conn))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            result.Add(new HourlyData
                            {
                                Hora = Convert.ToInt32(rdr["Hora"]),
                                Consumo = rdr["Consumo"] != DBNull.Value
                                          ? Convert.ToDouble(rdr["Consumo"]) : 0
                            });
                        }
                    }

                    // Rellenar horas sin datos con 0
                    for (int h = 0; h < 24; h++)
                    {
                        bool existe = false;
                        foreach (var r in result)
                        {
                            if (r.Hora == h) { existe = true; break; }
                        }
                        if (!existe)
                            result.Add(new HourlyData { Hora = h, Consumo = 0 });
                    }
                    result.Sort((a, b) => a.Hora.CompareTo(b.Hora));
                }
            }
            catch
            {
                return GetHorarioMock();
            }

            return result.Count > 0 ? result : GetHorarioMock();
        }

        // ═══════════════════════════════════════════════════════════════════
        // DATOS HISTÓRICOS PARA PREDICCIÓN
        // ═══════════════════════════════════════════════════════════════════
        public static List<DatoDiario> GetDatosHistoricosPrediccion(int diasHistorial = 30)
        {
            string dbErr;
            if (!TestConexion(out dbErr))
                return GetDatosHistoricosMock();

            var result = new List<DatoDiario>();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        DECLARE @fechaMax DATE;
                        SELECT @fechaMax = MAX(CAST(FechaHora AS DATE)) FROM Registro_Consumo;
                        DECLARE @inicio DATE = DATEADD(DAY, -@DiasHistorial, @fechaMax);
                        DECLARE @base   DATE;
                        SELECT @base = MIN(CAST(FechaHora AS DATE)) FROM Registro_Consumo;

                        SELECT
                            CAST(FechaHora AS DATE)        AS Fecha,
                            SUM(ConsumoWatts) / 1000.0     AS ConsumoKWh,
                            AVG(ConsumoWatts)               AS PromedioWatts,
                            COUNT(*)                        AS NumRegistros,
                            DATEDIFF(DAY, @base, CAST(FechaHora AS DATE)) AS DiaNumero
                        FROM Registro_Consumo
                        WHERE CAST(FechaHora AS DATE) BETWEEN @inicio AND @fechaMax
                        GROUP BY CAST(FechaHora AS DATE)
                        ORDER BY Fecha;";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DiasHistorial", diasHistorial);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                result.Add(new DatoDiario
                                {
                                    Fecha = Convert.ToDateTime(rdr["Fecha"]),
                                    ConsumoKWh = rdr["ConsumoKWh"] != DBNull.Value
                                                    ? Convert.ToDouble(rdr["ConsumoKWh"]) : 0,
                                    PromedioWatts = rdr["PromedioWatts"] != DBNull.Value
                                                    ? Convert.ToDouble(rdr["PromedioWatts"]) : 0,
                                    NumRegistros = Convert.ToInt32(rdr["NumRegistros"]),
                                    DiaNumero = Convert.ToInt32(rdr["DiaNumero"])
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                return GetDatosHistoricosMock();
            }

            return result.Count > 0 ? result : GetDatosHistoricosMock();
        }

        // ═══════════════════════════════════════════════════════════════════
        // ALGORITMO DE PREDICCIÓN
        // Promedio móvil 7 días + Regresión lineal por mínimos cuadrados
        // ═══════════════════════════════════════════════════════════════════
        public static List<PrediccionItem> GenerarPredicciones()
        {
            var datos = GetDatosHistoricosPrediccion(30);
            if (datos.Count < 3)
                return GetPrediccionesMock();

            // ── Promedio móvil ───────────────────────────────────────────
            int n7 = datos.Count >= 7 ? 7 : datos.Count;
            int n14 = datos.Count >= 14 ? 14 : datos.Count;

            double promovil7 = 0;
            for (int i = datos.Count - n7; i < datos.Count; i++)
                promovil7 += datos[i].ConsumoKWh;
            promovil7 /= n7;

            double promovil14 = 0;
            for (int i = datos.Count - n14; i < datos.Count; i++)
                promovil14 += datos[i].ConsumoKWh;
            promovil14 /= n14;

            // ── Regresión lineal: Y = a + b*X ───────────────────────────
            double n = datos.Count;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            foreach (var d in datos)
            {
                sumX += d.DiaNumero;
                sumY += d.ConsumoKWh;
                sumXY += d.DiaNumero * d.ConsumoKWh;
                sumX2 += (double)d.DiaNumero * d.DiaNumero;
            }
            double denom = n * sumX2 - sumX * sumX;
            double b = denom != 0 ? (n * sumXY - sumX * sumY) / denom : 0;
            double a = (sumY - b * sumX) / n;

            int maxDia = 0;
            foreach (var d in datos)
                if (d.DiaNumero > maxDia) maxDia = d.DiaNumero;

            double predMañana = Math.Max(0, a + b * (maxDia + 1));
            double predSemana = Math.Max(0, a + b * (maxDia + 7));

            // ── Cambios porcentuales ─────────────────────────────────────
            double consumoBase = datos[datos.Count - 1].ConsumoKWh;
            if (consumoBase == 0) consumoBase = promovil7;

            double cambioMañana = consumoBase > 0 ? (predMañana - consumoBase) / consumoBase * 100 : 0;
            double cambioSemana = consumoBase > 0 ? (predSemana - consumoBase) / consumoBase * 100 : 0;
            double cambioMovil = promovil14 > 0 ? (promovil7 - promovil14) / promovil14 * 100 : 0;

            // ── Confianza ────────────────────────────────────────────────
            double media = sumY / n;
            double varSum = 0;
            foreach (var d in datos)
                varSum += (d.ConsumoKWh - media) * (d.ConsumoKWh - media);
            double stdDev = Math.Sqrt(varSum / n);
            double cv = media > 0 ? stdDev / media : 1;
            double conf = Math.Max(10, Math.Min(95, (1 - cv) * 100));

            // ── Construir resultados ─────────────────────────────────────
            return new List<PrediccionItem>
            {
                new PrediccionItem
                {
                    Periodo            = "Mañana",
                    ConsumoEsperadoKWh = Math.Round(predMañana, 2),
                    PorcentajeCambio   = Math.Round(cambioMañana, 1),
                    Tendencia          = GetTendLabel(cambioMañana),
                    Color              = GetTendColor(cambioMañana),
                    Icono              = GetTendIcono(cambioMañana),
                    Confianza          = Math.Round(conf, 0),
                    Mensaje            = GetMsgMañana(cambioMañana, predMañana)
                },
                new PrediccionItem
                {
                    Periodo            = "Próxima semana",
                    ConsumoEsperadoKWh = Math.Round(predSemana * 7, 2),
                    PorcentajeCambio   = Math.Round(cambioSemana, 1),
                    Tendencia          = GetTendLabel(cambioSemana),
                    Color              = GetTendColor(cambioSemana),
                    Icono              = GetTendIcono(cambioSemana),
                    Confianza          = Math.Round(conf * 0.85, 0),
                    Mensaje            = GetMsgSemana(cambioSemana, predSemana)
                },
                new PrediccionItem
                {
                    Periodo            = "Promedio móvil (7 días)",
                    ConsumoEsperadoKWh = Math.Round(promovil7, 2),
                    PorcentajeCambio   = Math.Round(cambioMovil, 1),
                    Tendencia          = GetTendLabel(cambioMovil),
                    Color              = "#3498DB",
                    Icono              = "📊",
                    Confianza          = Math.Round(conf * 0.95, 0),
                    Mensaje            = string.Format(
                        "Promedio diario de los últimos 7 días: {0:0.0} kWh/día.", promovil7)
                }
            };
        }

        // ─── Helpers para predicción ──────────────────────────────────────
        private static string GetTendLabel(double pct)
        {
            if (pct > 5) return "Subida";
            if (pct < -5) return "Bajada";
            return "Estable";
        }
        private static string GetTendColor(double pct)
        {
            if (pct > 5) return "#EF4444";
            if (pct < -5) return "#32A76B";
            return "#3498DB";
        }
        private static string GetTendIcono(double pct)
        {
            if (pct > 5) return "📈";
            if (pct < -5) return "📉";
            return "➡";
        }
        private static string GetMsgMañana(double pct, double pred)
        {
            if (pct > 5)
                return string.Format(
                    "⚠️ Se espera un incremento del {0:0.0}% en consumo mañana.",
                    Math.Abs(pct));
            if (pct < -5)
                return string.Format(
                    "✅ Se proyecta una reducción del {0:0.0}% en consumo mañana.",
                    Math.Abs(pct));
            return string.Format(
                "ℹ️ El consumo de mañana se mantendrá estable ({0:0.0} kWh esperados).", pred);
        }
        private static string GetMsgSemana(double pct, double pred)
        {
            if (pct > 5)
                return string.Format(
                    "⚠️ La tendencia indica un incremento del {0:0.0}% la próxima semana.",
                    Math.Abs(pct));
            if (pct < -5)
                return string.Format(
                    "✅ La tendencia indica reducción del {0:0.0}% la próxima semana.",
                    Math.Abs(pct));
            return string.Format(
                "ℹ️ Consumo semanal proyectado: {0:0.0} kWh. Tendencia estable.", pred * 7);
        }

        // ═══════════════════════════════════════════════════════════════════
        // STORED PROCEDURE — Generar alertas inteligentes
        // ═══════════════════════════════════════════════════════════════════
        public static bool EjecutarGenerarAlertas(out int alertasGeneradas, out string error)
        {
            alertasGeneradas = 0;
            error = string.Empty;

            string dbErr;
            if (!TestConexion(out dbErr))
            {
                error = dbErr;
                return false;
            }

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("sp_GenerarAlertasInteligentes", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 30;
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                alertasGeneradas = rdr["AlertasGeneradas"] != DBNull.Value
                                    ? Convert.ToInt32(rdr["AlertasGeneradas"]) : 0;
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // DATOS MOCK — Fallback cuando SQL no está disponible
        // ═══════════════════════════════════════════════════════════════════
        private static KpiData GetKpisMock()
        {
            return new KpiData
            {
                PotenciaKW = 4.4,
                ConsumoKWh = 18.7,
                ConsumoHoyKWh = 18.7,
                CostoMesCOP = 14960,
                PromedioWatts = 850,
                DispositivosActivos = 5,
                AlertasActivas = 3,
                EstadoGeneral = "Advertencia",
                ColorEstado = "#F97316"
            };
        }

        private static List<DeviceConsumption> GetDispositivosMock()
        {
            return new List<DeviceConsumption>
            {
                new DeviceConsumption { Dispositivo="Aire Acond.",    Espacio="Salón A",  Estado="Activo",   ConsumoKWh=6.2, Porcentaje=45.5, PromedioWatts=1400, ConsumoNormalWatts=1380, Color="#308BB1" },
                new DeviceConsumption { Dispositivo="Refrigerador",   Espacio="Casa 1",   Estado="Activo",   ConsumoKWh=3.4, Porcentaje=25.0, PromedioWatts=350,  ConsumoNormalWatts=340,  Color="#32A76B" },
                new DeviceConsumption { Dispositivo="Servidor Local",  Espacio="Lab IoT",  Estado="Activo",   ConsumoKWh=2.1, Porcentaje=15.4, PromedioWatts=490,  ConsumoNormalWatts=480,  Color="#9B59B6" },
                new DeviceConsumption { Dispositivo="Computador",      Espacio="Sistemas", Estado="Activo",   ConsumoKWh=1.8, Porcentaje=13.2, PromedioWatts=250,  ConsumoNormalWatts=245,  Color="#F39C12" },
                new DeviceConsumption { Dispositivo="Iluminación",     Espacio="Pasillo",  Estado="Inactivo", ConsumoKWh=0.1, Porcentaje=0.9,  PromedioWatts=50,   ConsumoNormalWatts=50,   Color="#E91E63" }
            };
        }

        private static List<RegistroConsumoRow> GetRegistroMock()
        {
            return new List<RegistroConsumoRow>
            {
                new RegistroConsumoRow { RegistroID=1, Equipo="Refrigerador",  Espacio="Casa Principal", TipoEspacio="Casa",            Fecha="01/04/2026", Hora="10:00", FechaHora="01/04/2026 10:00", Voltaje=120.0, ConsumoWatts=1326.42, ConsumoNormalWatts=350,  EstadoConsumo="Crítico"     },
                new RegistroConsumoRow { RegistroID=2, Equipo="Servidor Local", Espacio="Lab IoT",       TipoEspacio="Salón de clases", Fecha="01/04/2026", Hora="09:30", FechaHora="01/04/2026 09:30", Voltaje=120.0, ConsumoWatts=456.95,  ConsumoNormalWatts=480,  EstadoConsumo="Normal"      },
                new RegistroConsumoRow { RegistroID=3, Equipo="Computador",     Espacio="Sistemas",      TipoEspacio="Salón de clases", Fecha="01/04/2026", Hora="09:00", FechaHora="01/04/2026 09:00", Voltaje=120.0, ConsumoWatts=224.58,  ConsumoNormalWatts=245,  EstadoConsumo="Normal"      },
                new RegistroConsumoRow { RegistroID=4, Equipo="Aire Acond.",    Espacio="Salón A",       TipoEspacio="Salón de clases", Fecha="01/04/2026", Hora="08:30", FechaHora="01/04/2026 08:30", Voltaje=120.0, ConsumoWatts=1108.75, ConsumoNormalWatts=1380, EstadoConsumo="Normal"      },
                new RegistroConsumoRow { RegistroID=5, Equipo="Aire Acond.",    Espacio="Salón B",       TipoEspacio="Salón de clases", Fecha="01/04/2026", Hora="01:15", FechaHora="01/04/2026 01:15", Voltaje=120.0, ConsumoWatts=3819.00, ConsumoNormalWatts=1628, EstadoConsumo="Crítico"     }
            };
        }

        private static List<EquipoRow> GetEquiposMock()
        {
            return new List<EquipoRow>
            {
                new EquipoRow { EquipoID=1, Nombre="Refrigerador Mod-61",  Espacio="Casa Principal", TipoEspacio="Casa",            ConsumoNormalWatts=351, ConsumoMaxWatts=456,  Estado="Activo"        },
                new EquipoRow { EquipoID=2, Nombre="Servidor Local Mod-44", Espacio="Lab IoT",       TipoEspacio="Salón de clases", ConsumoNormalWatts=482, ConsumoMaxWatts=627,  Estado="Activo"        },
                new EquipoRow { EquipoID=3, Nombre="Horno Microondas",      Espacio="Cocina 1",      TipoEspacio="Casa",            ConsumoNormalWatts=1099,ConsumoMaxWatts=1429, Estado="Inactivo"      },
                new EquipoRow { EquipoID=4, Nombre="Computador Escritorio", Espacio="Salón B",       TipoEspacio="Salón de clases", ConsumoNormalWatts=244, ConsumoMaxWatts=317,  Estado="Activo"        },
                new EquipoRow { EquipoID=5, Nombre="Aire Acond. Mod-25",    Espacio="Salón A",       TipoEspacio="Salón de clases", ConsumoNormalWatts=1628,ConsumoMaxWatts=2116, Estado="Mantenimiento" }
            };
        }

        private static List<HourlyData> GetHorarioMock()
        {
            double[] vals = { 0.5, 0.4, 0.3, 0.3, 0.4, 0.6, 0.9, 1.5,
                              1.8, 2.2, 2.8, 3.0, 3.2, 3.5, 3.8, 4.1,
                              4.4, 4.0, 3.6, 3.2, 2.8, 2.4, 2.1, 1.9 };
            var list = new List<HourlyData>();
            for (int h = 0; h < 24; h++)
                list.Add(new HourlyData { Hora = h, Consumo = vals[h] });
            return list;
        }

        private static List<DatoDiario> GetDatosHistoricosMock()
        {
            var lista = new List<DatoDiario>();
            var baseDate = new DateTime(2026, 4, 1);
            var rnd = new Random(42);
            for (int i = 0; i < 10; i++)
            {
                lista.Add(new DatoDiario
                {
                    Fecha = baseDate.AddDays(i),
                    ConsumoKWh = 18 + rnd.NextDouble() * 5,
                    PromedioWatts = 850 + rnd.NextDouble() * 200,
                    NumRegistros = 96,
                    DiaNumero = i
                });
            }
            return lista;
        }

        private static List<AlertaItem> GetAlertasMock()
        {
            return new List<AlertaItem>
            {
                new AlertaItem { Tipo="warning", Titulo="Pico crítico de consumo",   Descripcion="Aire Acond. Mod-25 — Registrado: 3819W / Normal: 1628W (234%)", Hora="01:15", Fecha="01/04/2026", Equipo="Aire Acond.",    Espacio="Salón A",        Resuelta=false },
                new AlertaItem { Tipo="info",    Titulo="Voltaje inestable",          Descripcion="Refrigerador Mod-68 — Voltaje 108V detectado",                  Hora="09:30", Fecha="01/04/2026", Equipo="Refrigerador",   Espacio="Casa Principal", Resuelta=false },
                new AlertaItem { Tipo="warning", Titulo="Sobreconsumo detectado",     Descripcion="Refrigerador Mod-61 — Registrado: 1326W / Normal: 351W (378%)", Hora="10:00", Fecha="01/04/2026", Equipo="Refrigerador",   Espacio="Casa Principal", Resuelta=false },
                new AlertaItem { Tipo="success", Titulo="Consumo normalizado",        Descripcion="Servidor Local — Volvió a rango normal",                        Hora="22:30", Fecha="01/04/2026", Equipo="Servidor Local", Espacio="Lab IoT",        Resuelta=true  }
            };
        }

        private static List<PrediccionItem> GetPrediccionesMock()
        {
            return new List<PrediccionItem>
            {
                new PrediccionItem { Periodo="Mañana",                 ConsumoEsperadoKWh=19.5,  PorcentajeCambio=4.3,  Tendencia="Subida",  Color="#F97316", Icono="📈", Confianza=72, Mensaje="⚠️ Se espera un incremento del 4.3% en consumo mañana."             },
                new PrediccionItem { Periodo="Próxima semana",         ConsumoEsperadoKWh=138.2, PorcentajeCambio=12.1, Tendencia="Subida",  Color="#EF4444", Icono="📈", Confianza=61, Mensaje="⚠️ La tendencia indica un incremento del 12.1% la próxima semana." },
                new PrediccionItem { Periodo="Promedio móvil (7 días)",ConsumoEsperadoKWh=18.7,  PorcentajeCambio=1.2,  Tendencia="Estable", Color="#3498DB", Icono="📊", Confianza=80, Mensaje="Promedio diario de los últimos 7 días: 18.7 kWh/día."              }
            };
        }
    }
}