namespace EcoVolt.Models
{
    /// <summary>
    /// Resultado de predicción generado por promedio móvil y regresión lineal.
    /// </summary>
    public class PrediccionItem
    {
        /// <summary>Etiqueta del período predicho (ej: "Mañana", "Próxima semana")</summary>
        public string Periodo { get; set; }

        /// <summary>Consumo esperado en kWh</summary>
        public double ConsumoEsperadoKWh { get; set; }

        /// <summary>Porcentaje de cambio respecto al período anterior (+/-)</summary>
        public double PorcentajeCambio { get; set; }

        /// <summary>Tendencia: "Subida", "Bajada", "Estable"</summary>
        public string Tendencia { get; set; }

        /// <summary>Color para visualización en la UI</summary>
        public string Color { get; set; }

        /// <summary>Icono emoji para la UI</summary>
        public string Icono { get; set; }

        /// <summary>Mensaje descriptivo para el usuario</summary>
        public string Mensaje { get; set; }

        /// <summary>Nivel de confianza de la predicción (0-100)</summary>
        public double Confianza { get; set; }
    }

    /// <summary>
    /// Punto de dato diario para alimentar el algoritmo de predicción.
    /// </summary>
    public class DatoDiario
    {
        public System.DateTime Fecha { get; set; }
        public double ConsumoKWh { get; set; }
        public double PromedioWatts { get; set; }
        public int NumRegistros { get; set; }
        public int DiaNumero { get; set; }   // Para regresión lineal (eje X)
    }
}