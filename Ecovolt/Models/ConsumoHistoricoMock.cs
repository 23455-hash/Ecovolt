using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EcoVolt.Models
{
    public class ConsumoHistoricoMock
    {
        public int RegistroID { get; set; }
        public string Dispositivo { get; set; }
        public string Espacio { get; set; }
        public string TipoEspacio { get; set; }
        public DateTime FechaHora { get; set; }
        public double ConsumoKWh { get; set; }
        public double ConsumoWatts { get; set; }
        public double Amperaje { get; set; }
        public double Voltaje { get; set; }
        public double ConsumoNormalWatts { get; set; }
        public double ConsumoMaxWatts { get; set; }

        public string EstadoConsumo
        {
            get
            {
                if (ConsumoWatts > ConsumoMaxWatts) return "Crítico";
                if (ConsumoWatts > ConsumoNormalWatts * 1.10) return "Advertencia";
                return "Normal";
            }
        }

        public string Fecha { get { return FechaHora.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture); } }
        public string Hora { get { return FechaHora.ToString("HH:mm", CultureInfo.InvariantCulture); } }
        public string FechaHoraTexto { get { return FechaHora.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture); } }
    }

    public class DeviceLiveReading
    {
        public string Dispositivo { get; set; }
        public double ConsumoKWh { get; set; }
        public double Watts { get; set; }
        public double Amperaje { get; set; }
        public double Voltaje { get; set; }
        public string Color { get; set; }
    }

    public class MultimeterReading
    {
        public double TotalWatts { get; set; }
        public double TotalAmperaje { get; set; }
        public double VoltajePromedio { get; set; }
        public double ConsumoActualKWh { get; set; }
        public double CostoEstimadoMesCOP { get; set; }
        public double ConsumoEstimadoMesKWh { get; set; }
        public List<DeviceLiveReading> Dispositivos { get; set; }
    }

    public static class MockEnergyRepository
    {
        public const double TarifaCopPorKWh = 800.0;

        private static readonly string[] Colors = new[]
        {
            "#308BB1", "#32A76B", "#F39C12", "#9B59B6", "#E91E63"
        };

        private static readonly List<EquipoRow> Equipos = new List<EquipoRow>
        {
            new EquipoRow { EquipoID = 1, Nombre = "Refrigerador", Espacio = "Cocina", TipoEspacio = "Casa", ConsumoNormalWatts = 320, ConsumoMaxWatts = 460, Estado = "Activo" },
            new EquipoRow { EquipoID = 2, Nombre = "Aire Acondicionado", Espacio = "Sala", TipoEspacio = "Casa", ConsumoNormalWatts = 1450, ConsumoMaxWatts = 2100, Estado = "Activo" },
            new EquipoRow { EquipoID = 3, Nombre = "Iluminación", Espacio = "Zonas comunes", TipoEspacio = "Casa", ConsumoNormalWatts = 180, ConsumoMaxWatts = 300, Estado = "Activo" },
            new EquipoRow { EquipoID = 4, Nombre = "Computadora", Espacio = "Oficina", TipoEspacio = "Casa", ConsumoNormalWatts = 260, ConsumoMaxWatts = 420, Estado = "Activo" },
            new EquipoRow { EquipoID = 5, Nombre = "Lavadora", Espacio = "Lavandería", TipoEspacio = "Casa", ConsumoNormalWatts = 520, ConsumoMaxWatts = 900, Estado = "Mantenimiento" }
        };

        private static readonly Lazy<List<ConsumoHistoricoMock>> Registros =
            new Lazy<List<ConsumoHistoricoMock>>(BuildRegistros);

        public static List<ConsumoHistoricoMock> GetHistorial()
        {
            return Registros.Value.ToList();
        }

        public static List<EquipoRow> GetEquipos()
        {
            return Equipos.Select(e => new EquipoRow
            {
                EquipoID = e.EquipoID,
                Nombre = e.Nombre,
                Espacio = e.Espacio,
                TipoEspacio = e.TipoEspacio,
                ConsumoNormalWatts = e.ConsumoNormalWatts,
                ConsumoMaxWatts = e.ConsumoMaxWatts,
                Estado = e.Estado
            }).ToList();
        }

        public static MultimeterReading GenerateLiveReading()
        {
            var rnd = new Random(Environment.TickCount);
            var historial = GetHistorial();
            var devices = historial
                .GroupBy(r => r.Dispositivo)
                .Select((g, index) =>
                {
                    double avgWatts = g.Average(x => x.ConsumoWatts);
                    double factor = 0.82 + rnd.NextDouble() * 0.38;
                    double watts = Math.Round(avgWatts * factor, 2);
                    double volts = Math.Round(116 + rnd.NextDouble() * 8, 1);
                    return new DeviceLiveReading
                    {
                        Dispositivo = g.Key,
                        Watts = watts,
                        Voltaje = volts,
                        Amperaje = Math.Round(watts / volts, 2),
                        ConsumoKWh = Math.Round(watts / 1000.0, 3),
                        Color = Colors[index % Colors.Length]
                    };
                })
                .OrderByDescending(d => d.Watts)
                .ToList();

            double totalWatts = devices.Sum(d => d.Watts);
            double avgDailyKWh = historial
                .GroupBy(r => r.FechaHora.Date)
                .Average(g => g.Sum(x => x.ConsumoKWh));
            double monthlyKWh = avgDailyKWh * DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month);

            return new MultimeterReading
            {
                Dispositivos = devices,
                TotalWatts = Math.Round(totalWatts, 2),
                TotalAmperaje = Math.Round(devices.Sum(d => d.Amperaje), 2),
                VoltajePromedio = Math.Round(devices.Average(d => d.Voltaje), 1),
                ConsumoActualKWh = Math.Round(totalWatts / 1000.0, 3),
                ConsumoEstimadoMesKWh = Math.Round(monthlyKWh, 2),
                CostoEstimadoMesCOP = Math.Round(monthlyKWh * TarifaCopPorKWh, 0)
            };
        }

        public static string BuildSqlSeedScript()
        {
            var lines = new List<string>
            {
                "CREATE TABLE dbo.HistorialConsumoElectrico (",
                "    RegistroID INT NOT NULL PRIMARY KEY,",
                "    Dispositivo NVARCHAR(80) NOT NULL,",
                "    Espacio NVARCHAR(80) NOT NULL,",
                "    TipoEspacio NVARCHAR(50) NOT NULL,",
                "    FechaHora DATETIME2 NOT NULL,",
                "    ConsumoKWh DECIMAL(10,3) NOT NULL,",
                "    Amperaje DECIMAL(10,2) NOT NULL,",
                "    Voltaje DECIMAL(10,1) NOT NULL,",
                "    ConsumoWatts DECIMAL(10,2) NOT NULL,",
                "    EstadoConsumo NVARCHAR(20) NOT NULL",
                ");",
                "",
                "INSERT INTO dbo.HistorialConsumoElectrico",
                "    (RegistroID, Dispositivo, Espacio, TipoEspacio, FechaHora, ConsumoKWh, Amperaje, Voltaje, ConsumoWatts, EstadoConsumo)",
                "VALUES"
            };

            var values = GetHistorial().Select(r => string.Format(
                CultureInfo.InvariantCulture,
                "({0}, N'{1}', N'{2}', N'{3}', '{4:yyyy-MM-dd HH:mm:ss}', {5:0.000}, {6:0.00}, {7:0.0}, {8:0.00}, N'{9}')",
                r.RegistroID,
                r.Dispositivo.Replace("'", "''"),
                r.Espacio.Replace("'", "''"),
                r.TipoEspacio.Replace("'", "''"),
                r.FechaHora,
                r.ConsumoKWh,
                r.Amperaje,
                r.Voltaje,
                r.ConsumoWatts,
                r.EstadoConsumo)).ToList();

            for (int i = 0; i < values.Count; i++)
                lines.Add(values[i] + (i == values.Count - 1 ? ";" : ","));

            return string.Join(Environment.NewLine, lines);
        }

        private static List<ConsumoHistoricoMock> BuildRegistros()
        {
            var result = new List<ConsumoHistoricoMock>();
            var rnd = new Random(20260605);
            var start = DateTime.Today.AddDays(-40).Date.AddHours(6);
            int id = 1;

            for (int day = 0; day < 40; day++)
            {
                foreach (var equipo in Equipos)
                {
                    int hour = GetPreferredHour(equipo.Nombre, day);
                    var fecha = start.AddDays(day).Date.AddHours(hour).AddMinutes((day * 17 + equipo.EquipoID * 11) % 60);
                    double volts = 116.5 + rnd.NextDouble() * 7.5;
                    double factor = GetLoadFactor(equipo.Nombre, hour) + (rnd.NextDouble() - 0.5) * 0.22;
                    double watts = Math.Max(25, equipo.ConsumoNormalWatts * factor);

                    result.Add(new ConsumoHistoricoMock
                    {
                        RegistroID = id++,
                        Dispositivo = equipo.Nombre,
                        Espacio = equipo.Espacio,
                        TipoEspacio = equipo.TipoEspacio,
                        FechaHora = fecha,
                        Voltaje = Math.Round(volts, 1),
                        ConsumoWatts = Math.Round(watts, 2),
                        ConsumoNormalWatts = equipo.ConsumoNormalWatts,
                        ConsumoMaxWatts = equipo.ConsumoMaxWatts ?? equipo.ConsumoNormalWatts * 1.3,
                        Amperaje = Math.Round(watts / volts, 2),
                        ConsumoKWh = Math.Round(watts / 1000.0, 3)
                    });
                }
            }

            return result.OrderByDescending(r => r.FechaHora).ToList();
        }

        private static int GetPreferredHour(string device, int day)
        {
            if (device == "Refrigerador") return (day * 3) % 24;
            if (device == "Aire Acondicionado") return 13 + (day % 6);
            if (device == "Iluminación") return day % 2 == 0 ? 19 : 6;
            if (device == "Computadora") return 8 + (day % 10);
            return day % 3 == 0 ? 10 : 18;
        }

        private static double GetLoadFactor(string device, int hour)
        {
            if (device == "Aire Acondicionado" && hour >= 13 && hour <= 18) return 1.18;
            if (device == "Iluminación" && (hour >= 18 || hour <= 6)) return 1.22;
            if (device == "Computadora" && hour >= 8 && hour <= 17) return 1.05;
            if (device == "Lavadora") return hour >= 10 && hour <= 19 ? 1.12 : 0.62;
            if (device == "Refrigerador") return 0.92;
            return 1.0;
        }
    }
}
