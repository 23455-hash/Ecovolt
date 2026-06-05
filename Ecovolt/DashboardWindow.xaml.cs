// ═══════════════════════════════════════════════════════════════════════════
//  EcoVolt — DashboardWindow.xaml.cs  (VERSIÓN CORREGIDA)
//  Ubicación: DashboardWindow.xaml.cs  (raíz del proyecto)
//  Correcciones:
//    • switch expression → if/else (C# 7.3 compatible)
//    • new Thickness(x) → new Thickness(x, x, x, x)
//    • CharacterSpacing eliminado (no existe en WPF .NET Framework)
//    • Imports depurados
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using EcoVolt.Models;

namespace EcoVolt
{
    public partial class DashboardWindow : Window
    {
        // ─── ESTADO INTERNO ──────────────────────────────────────────────
        private System.Windows.Threading.DispatcherTimer _timer;
        private int _timerTick = 0;

        private List<DeviceConsumption> _devices = new List<DeviceConsumption>();
        private List<HourlyData> _hourly = new List<HourlyData>();
        private List<AlertaItem> _alertas = new List<AlertaItem>();
        private List<RegistroConsumoRow> _registros = new List<RegistroConsumoRow>();
        private List<EquipoRow> _equiposData = new List<EquipoRow>();
        private List<PrediccionItem> _predicciones = new List<PrediccionItem>();
        private List<DatoDiario> _datosHistoricos = new List<DatoDiario>();
        private KpiData _kpi = new KpiData();

        // ─── CONSTRUCTOR ─────────────────────────────────────────────────
        public DashboardWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            InitializeAsyncWebView();
        }

        // ─── WEBVIEW2 (Power BI) ─────────────────────────────────────────
        private async void InitializeAsyncWebView()
        {
            try
            {
                await pbiViewer.EnsureCoreWebView2Async(null);
                // Reemplaza esta URL con la de tu informe publicado en Power BI Service
                // Formato: https://app.powerbi.com/groups/me/reports/XXXX
                string linkPowerBI =
                    "https://app.powerbi.com/groups/me/reports/6fcabc3e-e708-493e-b062-785390b0b51a/" +
                    "15f93fd07f83b722c5ba?experience=power-bi&navContentPaneEnabled=false";
                pbiViewer.Source = new Uri(linkPowerBI);
            }
            catch
            {
                // WebView2 puede no estar instalado — el resto del Dashboard funciona igual
            }
        }

        // ─── ON LOADED ───────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            string name = UserStore.CurrentUser != null
                ? UserStore.CurrentUser.Name
                : "Usuario";
            lblGreeting.Text = "Hola, " + name.Split(' ')[0];

            bool dbOk;
            string dbErr;
            dbOk = DatabaseService.TestConexion(out dbErr);

            Color statusColor = dbOk
                ? (Color)ColorConverter.ConvertFromString("#32A76B")
                : (Color)ColorConverter.ConvertFromString("#EF4444");
            elDB.Fill = new SolidColorBrush(statusColor);
            lblDBStatus.Text = dbOk ? "SQL Conectado" : "Modo Demo";

            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, a) =>
            {
                lblDateTime.Text = DateTime.Now.ToString(
                    "dddd, d 'de' MMMM yyyy  —  HH:mm:ss",
                    new System.Globalization.CultureInfo("es-CO"));
                _timerTick++;
                if (_timerTick >= 30)
                {
                    _timerTick = 0;
                    LoadAllData();
                }
            };
            _timer.Start();

            LoadAllData();
        }

        // ═══════════════════════════════════════════════════════════════════
        // CARGA DE DATOS PRINCIPAL
        // ═══════════════════════════════════════════════════════════════════
        private void LoadAllData()
        {
            string periodo = GetPeriodoSeleccionado();

            _kpi = DatabaseService.GetKpis(periodo);
            _devices = DatabaseService.GetConsumoDispositivos(periodo);
            _hourly = DatabaseService.GetConsumoHorario();
            _alertas = DatabaseService.GetAlertas(periodo);
            _registros = DatabaseService.GetRegistroConsumo(periodo);
            _equiposData = DatabaseService.GetEquipos();
            _predicciones = DatabaseService.GenerarPredicciones();
            _datosHistoricos = DatabaseService.GetDatosHistoricosPrediccion(30);

            // ── KPIs ─────────────────────────────────────────────────────
            lblPotencia.Text = string.Format("{0:0.0} kW", _kpi.PotenciaKW);
            lblConsumo.Text = string.Format("{0:0.0} kWh", _kpi.ConsumoKWh);
            lblCosto.Text = string.Format("$ {0:N0}", _kpi.CostoMesCOP);
            lblPromedio.Text = string.Format("{0:N0} W", _kpi.PromedioWatts);
            lblDispositivos.Text = _kpi.DispositivosActivos.ToString();

            lblEstadoGeneral.Text = _kpi.EstadoGeneral;
            lblEstadoGeneral.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_kpi.ColorEstado));
            lblAlertasActivas.Text = _kpi.AlertasActivas + " alertas activas";

            // ── Badge alertas sidebar ─────────────────────────────────────
            int warns = 0;
            foreach (var a in _alertas)
                if (a.Tipo == "warning") warns++;
            lblBadgeCount.Text = warns.ToString();
            badgeAlertas.Visibility = warns > 0 ? Visibility.Visible : Visibility.Collapsed;

            // ── Renderizar paneles ────────────────────────────────────────
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    BuildTopDispositivos();
                    BuildAlertasResumen();
                    BuildPrediccionResumen();
                    BuildAlertasDetail();
                    BuildConsumoTable();
                    BuildHistorialTable();
                    BuildPrediccionesPage();
                    BuildDevicesDetail();
                    BuildHistorialSummary();
                    BuildAlertasSummary();
                    BuildDispositivosSummary();
                }));
        }

        // ─── SELECTOR DE PERÍODO ─────────────────────────────────────────
        private string GetPeriodoSeleccionado()
        {
            if (cbPeriodo == null) return "semana";
            int idx = cbPeriodo.SelectedIndex;
            if (idx == 0) return "hoy";
            if (idx == 1) return "semana";
            if (idx == 2) return "mes";
            if (idx == 3) return "anio";
            return "semana";
        }

        // ═══════════════════════════════════════════════════════════════════
        // TOP DISPOSITIVOS — Dashboard inicio
        // ═══════════════════════════════════════════════════════════════════
        private void BuildTopDispositivos()
        {
            topDispositivosPanel.Children.Clear();
            if (_devices == null || _devices.Count == 0) return;

            double maxKwh = 0;
            foreach (var d in _devices)
                if (d.ConsumoKWh > maxKwh) maxKwh = d.ConsumoKWh;

            int count = 0;
            foreach (var d in _devices)
            {
                if (count >= 5) break;
                count++;

                var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lblNombre = new TextBlock
                {
                    Text = d.Dispositivo,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8CCCA")),
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(lblNombre, 0);

                double pct = maxKwh > 0 ? d.ConsumoKWh / maxKwh * 100 : 0;
                var pb = new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = pct,
                    Height = 8,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C2128")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(d.Color)),
                    BorderThickness = new Thickness(0, 0, 0, 0),
                    Margin = new Thickness(8, 0, 8, 0)
                };
                Grid.SetColumn(pb, 1);

                var lblVal = new TextBlock
                {
                    Text = string.Format("{0:0.0} kWh", d.ConsumoKWh),
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A6055")),
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 75,
                    TextAlignment = TextAlignment.Right
                };
                Grid.SetColumn(lblVal, 2);

                row.Children.Add(lblNombre);
                row.Children.Add(pb);
                row.Children.Add(lblVal);
                topDispositivosPanel.Children.Add(row);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // RESUMEN ALERTAS — Panel lateral en inicio
        // ═══════════════════════════════════════════════════════════════════
        private void BuildAlertasResumen()
        {
            alertasResumenPanel.Children.Clear();
            if (_alertas == null) return;

            int count = 0;
            foreach (var a in _alertas)
            {
                if (count >= 4) break;
                count++;

                string dot;
                if (a.Tipo == "warning") dot = "#EF4444";
                else if (a.Tipo == "info") dot = "#F97316";
                else dot = "#32A76B";

                var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var elDot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dot)),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(elDot, 0);

                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = a.Titulo,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dot)),
                    FontFamily = new FontFamily("Segoe UI"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                sp.Children.Add(new TextBlock
                {
                    Text = a.Equipo,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x60, 0x55)),
                    FontFamily = new FontFamily("Segoe UI")
                });
                Grid.SetColumn(sp, 1);

                var lblHora = new TextBlock
                {
                    Text = a.Hora,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x35)),
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                Grid.SetColumn(lblHora, 2);

                row.Children.Add(elDot);
                row.Children.Add(sp);
                row.Children.Add(lblHora);
                alertasResumenPanel.Children.Add(row);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PREDICCIÓN RÁPIDA — Mini-tarjetas en inicio
        // ═══════════════════════════════════════════════════════════════════
        private void BuildPrediccionResumen()
        {
            prediccionResumenPanel.Children.Clear();
            if (_predicciones == null || _predicciones.Count == 0) return;

            foreach (var p in _predicciones)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#161B22")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C2128")),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 12, 0),
                    MinWidth = 200
                };

                var sp = new StackPanel();
                sp.Children.Add(new TextBlock
                {
                    Text = p.Icono + "  " + p.Periodo.ToUpper(),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x60, 0x55)),
                    FontFamily = new FontFamily("Segoe UI"),
                    Margin = new Thickness(0, 0, 0, 6)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = string.Format("{0:0.0} kWh", p.ConsumoEsperadoKWh),
                    FontSize = 22,
                    FontWeight = FontWeights.Black,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p.Color)),
                    FontFamily = new FontFamily("Segoe UI Black"),
                    Margin = new Thickness(0, 0, 0, 4)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = p.Mensaje,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x60, 0x55)),
                    FontFamily = new FontFamily("Segoe UI"),
                    TextWrapping = TextWrapping.Wrap
                });
                card.Child = sp;
                prediccionResumenPanel.Children.Add(card);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // CONSUMO — Tabla
        // ═══════════════════════════════════════════════════════════════════
        private void BuildConsumoTable()
        {
            if (_registros == null) return;
            dgConsumo.ItemsSource = _registros;
        }

        private void TxtFiltroConsumo_Changed(object sender, TextChangedEventArgs e)
        {
            if (_registros == null || !IsLoaded) return;
            string filtro = txtFiltroConsumo.Text != null
                ? txtFiltroConsumo.Text.Trim().ToLower() : "";
            if (string.IsNullOrEmpty(filtro))
            {
                dgConsumo.ItemsSource = _registros;
            }
            else
            {
                dgConsumo.ItemsSource = _registros.Where(r =>
                    r.Equipo.ToLower().Contains(filtro) ||
                    r.Espacio.ToLower().Contains(filtro) ||
                    r.EstadoConsumo.ToLower().Contains(filtro)).ToList();
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // HISTORIAL — Tabla + resumen
        // ═══════════════════════════════════════════════════════════════════
        private void BuildHistorialTable()
        {
            if (_registros == null) return;
            dgHistorial.ItemsSource = _registros;
        }

        private void BuildHistorialSummary()
        {
            if (_registros == null) return;
            lblHistTotalReg.Text = _registros.Count.ToString("N0");
            int criticos = 0, advertencias = 0;
            foreach (var r in _registros)
            {
                if (r.EstadoConsumo == "Crítico") criticos++;
                if (r.EstadoConsumo == "Advertencia") advertencias++;
            }
            lblHistCriticos.Text = criticos.ToString("N0");
            lblHistAdvert.Text = advertencias.ToString("N0");
        }

        private void TxtFiltroHistorial_Changed(object sender, TextChangedEventArgs e)
        {
            if (_registros == null || !IsLoaded) return;
            string filtro = txtFiltroHistorial.Text != null
                ? txtFiltroHistorial.Text.Trim().ToLower() : "";
            if (string.IsNullOrEmpty(filtro))
            {
                dgHistorial.ItemsSource = _registros;
            }
            else
            {
                dgHistorial.ItemsSource = _registros.Where(r =>
                    r.Equipo.ToLower().Contains(filtro) ||
                    r.Espacio.ToLower().Contains(filtro) ||
                    r.Fecha.Contains(filtro) ||
                    r.EstadoConsumo.ToLower().Contains(filtro)).ToList();
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ALERTAS — Tarjetas + resumen
        // ═══════════════════════════════════════════════════════════════════
        private void BuildAlertasSummary()
        {
            if (_alertas == null) return;
            int criticas = 0, medias = 0, informativas = 0;
            foreach (var a in _alertas)
            {
                if (a.Tipo == "warning" && !a.Resuelta) criticas++;
                else if (a.Tipo == "info") medias++;
                else informativas++;
            }
            lblAlertasCriticas.Text = criticas.ToString();
            lblAlertasMedia.Text = medias.ToString();
            lblAlertasInfo.Text = informativas.ToString();
        }

        private void BuildAlertasDetail()
        {
            alertasDetailPanel.Children.Clear();
            if (_alertas == null) return;
            BuildAlertsInPanel(_alertas, alertasDetailPanel);
        }

        private void BuildAlertsInPanel(List<AlertaItem> alertas, Panel panel)
        {
            panel.Children.Clear();
            foreach (var a in alertas)
            {
                bool isWarn = a.Tipo == "warning";
                bool isSucc = a.Tipo == "success";
                bool isCrit = a.Tipo == "critical";

                string bgClr, bdClr, titleClr, icon;

                if (isCrit)
                {
                    bgClr = "#1A0808"; bdClr = "#3A1010"; titleClr = "#EF4444"; icon = "🚨";
                }
                else if (isWarn)
                {
                    bgClr = "#1A1000"; bdClr = "#3A2500"; titleClr = "#F97316"; icon = "⚠️";
                }
                else if (isSucc)
                {
                    bgClr = "#0A1A14"; bdClr = "#1A3A2A"; titleClr = "#32A76B"; icon = "✅";
                }
                else
                {
                    bgClr = "#0D1422"; bdClr = "#1A2A3A"; titleClr = "#3498DB"; icon = "ℹ️";
                }

                var card = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgClr)),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bdClr)),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icTb = new TextBlock
                {
                    Text = icon,
                    FontSize = 18,
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(icTb, 0);

                var textSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textSp.Children.Add(new TextBlock
                {
                    Text = a.Titulo,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(titleClr)),
                    FontFamily = new FontFamily("Segoe UI")
                });
                textSp.Children.Add(new TextBlock
                {
                    Text = a.Descripcion,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x60, 0x55)),
                    FontFamily = new FontFamily("Segoe UI"),
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
                textSp.Children.Add(new TextBlock
                {
                    Text = a.Equipo + "  ·  " + a.Espacio,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x35)),
                    FontFamily = new FontFamily("Segoe UI"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(textSp, 1);

                var fechaTb = new TextBlock
                {
                    Text = a.Fecha,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x35)),
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                Grid.SetColumn(fechaTb, 2);

                var horaTb = new TextBlock
                {
                    Text = a.Hora,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x60, 0x55)),
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    Width = 45
                };
                Grid.SetColumn(horaTb, 3);

                g.Children.Add(icTb);
                g.Children.Add(textSp);
                g.Children.Add(fechaTb);
                g.Children.Add(horaTb);
                card.Child = g;
                panel.Children.Add(card);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PREDICCIONES — Tarjetas y tabla histórica
        // ═══════════════════════════════════════════════════════════════════
        private void BuildPrediccionesPage()
        {
            if (_predicciones == null || _predicciones.Count == 0) return;

            // Tarjeta MAÑANA
            if (_predicciones.Count > 0)
            {
                var p0 = _predicciones[0];
                lblPredIconMañana.Text = p0.Icono;
                lblPredKwhMañana.Text = string.Format("{0:0.0}", p0.ConsumoEsperadoKWh);
                lblPredTendMañana.Text = p0.Tendencia;
                lblPredTendMañana.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(p0.Color));
                string signoMañana = p0.PorcentajeCambio >= 0 ? "+" : "";
                lblPredPctMañana.Text = "  " + signoMañana + string.Format("{0:0.0}%", p0.PorcentajeCambio);
                lblPredMsgMañana.Text = p0.Mensaje;
                lblPredConfMañana.Text = string.Format("{0:0}%", p0.Confianza);
            }

            // Tarjeta SEMANA
            if (_predicciones.Count > 1)
            {
                var p1 = _predicciones[1];
                lblPredIconSemana.Text = p1.Icono;
                lblPredKwhSemana.Text = string.Format("{0:0.0}", p1.ConsumoEsperadoKWh);
                lblPredTendSemana.Text = p1.Tendencia;
                lblPredTendSemana.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(p1.Color));
                string signoSemana = p1.PorcentajeCambio >= 0 ? "+" : "";
                lblPredPctSemana.Text = "  " + signoSemana + string.Format("{0:0.0}%", p1.PorcentajeCambio);
                lblPredMsgSemana.Text = p1.Mensaje;
                lblPredConfSemana.Text = string.Format("{0:0}%", p1.Confianza);
            }

            // Tarjeta PROMEDIO MÓVIL
            if (_predicciones.Count > 2)
            {
                var p2 = _predicciones[2];
                lblPredIconMovil.Text = p2.Icono;
                lblPredKwhMovil.Text = string.Format("{0:0.0}", p2.ConsumoEsperadoKWh);
                lblPredTendMovil.Text = p2.Tendencia;
                lblPredTendMovil.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(p2.Color));
                string signoMovil = p2.PorcentajeCambio >= 0 ? "+" : "";
                lblPredPctMovil.Text = "  " + signoMovil + string.Format("{0:0.0}%", p2.PorcentajeCambio);
                lblPredMsgMovil.Text = p2.Mensaje;
                lblPredConfMovil.Text = string.Format("{0:0}%", p2.Confianza);
            }

            // Texto informativo
            if (_datosHistoricos != null && _datosHistoricos.Count > 0)
            {
                var primero = _datosHistoricos[0].Fecha;
                var ultimo = _datosHistoricos[_datosHistoricos.Count - 1].Fecha;
                lblPrediccionInfo.Text = string.Format(
                    "Modelo entrenado con {0} días de datos ({1:dd/MM/yyyy} – {2:dd/MM/yyyy}). " +
                    "Algoritmos: Regresión lineal por mínimos cuadrados + Promedio móvil 7 días.",
                    _datosHistoricos.Count, primero, ultimo);
            }

            // Tabla datos históricos
            if (_datosHistoricos != null)
                dgHistoricoPrediccion.ItemsSource = _datosHistoricos;
        }

        // ═══════════════════════════════════════════════════════════════════
        // DISPOSITIVOS — Tabla + resumen
        // ═══════════════════════════════════════════════════════════════════
        private void BuildDevicesDetail()
        {
            if (_equiposData == null) return;
            dgEquipos.ItemsSource = _equiposData;
        }

        private void BuildDispositivosSummary()
        {
            if (_equiposData == null) return;
            int activos = 0, mant = 0;
            foreach (var eq in _equiposData)
            {
                if (eq.Estado == "Activo") activos++;
                if (eq.Estado == "Mantenimiento") mant++;
            }
            lblTotalEquipos.Text = _equiposData.Count.ToString();
            lblEquiposActivos.Text = activos.ToString();
            lblEquiposMant.Text = mant.ToString();
        }

        // ═══════════════════════════════════════════════════════════════════
        // NAVEGACIÓN
        // ═══════════════════════════════════════════════════════════════════
        private void ShowPage(UIElement page)
        {
            pageDashboard.Visibility = Visibility.Collapsed;
            pageConsumo.Visibility = Visibility.Collapsed;
            pageHistorial.Visibility = Visibility.Collapsed;
            pageAlertas.Visibility = Visibility.Collapsed;
            pagePredicciones.Visibility = Visibility.Collapsed;
            pageReportes.Visibility = Visibility.Collapsed;
            pageDispositivos.Visibility = Visibility.Collapsed;
            pageConfig.Visibility = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
        }

        private void SetNavActive(Button active)
        {
            btnNavDashboard.Style = (Style)FindResource("BtnNav");
            btnNavConsumo.Style = (Style)FindResource("BtnNav");
            btnNavHistorial.Style = (Style)FindResource("BtnNav");
            btnNavAlertas.Style = (Style)FindResource("BtnNav");
            btnNavPredicciones.Style = (Style)FindResource("BtnNav");
            btnNavReportes.Style = (Style)FindResource("BtnNav");
            btnNavDispositivos.Style = (Style)FindResource("BtnNav");
            btnNavConfig.Style = (Style)FindResource("BtnNav");
            active.Style = (Style)FindResource("BtnNavActive");
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            SetNavActive(btnNavDashboard);
            ShowPage(pageDashboard);
            lblPageTitle.Text = "Panel de análisis";
        }

        private void NavConsumo_Click(object sender, RoutedEventArgs e)
        {
            SetNavActive(btnNavConsumo);
            ShowPage(pageConsumo);
            lblPageTitle.Text = "Consumo Detallado";
        }

        private void NavHistorial_Click(object sender, RoutedEventArgs e)
        {
            SetNavActive(btnNavHistorial);
            ShowPage(pageHistorial);
            lblPageTitle.Text = "Historial de Consumo";
        }

        private void NavAlertas_Click(object sender, RoutedEventArgs e)
        {
            SetNavActive(btnNavAlertas);
            ShowPage(pageAlertas);
            lblPageTitle.Text = "Alertas Inteligentes";
        }

        private void NavPredicciones_Click(object sender, RoutedEventArgs e)
        {
            SetNavActive(btnNavPredicciones);
            ShowPage(pagePredicciones);
            lblPageTitle.Text = "Predicciones";
        }

        private void NavReportes_Click(object sender, RoutedEventArgs e)
        {
            SetNavActive(btnNavReportes);
            ShowPage(pageReportes);
            lblPageTitle.Text = "Reportes · Power BI";
        }

        private void NavDispositivos_Click(object sender, RoutedEventArgs e)
        {
            SetNavActive(btnNavDispositivos);
            ShowPage(pageDispositivos);
            lblPageTitle.Text = "Dispositivos";
        }

        private void NavConfig_Click(object sender, RoutedEventArgs e)
        {
            SetNavActive(btnNavConfig);
            ShowPage(pageConfig);
            lblPageTitle.Text = "Configuración";
        }

        // ═══════════════════════════════════════════════════════════════════
        // EVENTOS GENERALES
        // ═══════════════════════════════════════════════════════════════════
        private void CbPeriodo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            LoadAllData();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAllData();
            try { pbiViewer?.Reload(); } catch { }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            Application.Current.Shutdown();
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            UserStore.CurrentUser = null;
            new LoginWindow().Show();
            Close();
        }

        // ─── Configuración: Test conexión ────────────────────────────────
        private void BtnTestConexion_Click(object sender, RoutedEventArgs e)
        {
            string err;
            bool ok = DatabaseService.TestConexion(out err);
            if (ok)
            {
                lblConfigResult.Text = "✅ Conexión exitosa con SQL Server — Consumo_Electrico_EcoVolt";
                lblConfigResult.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#32A76B"));
                elDB.Fill = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#32A76B"));
                lblDBStatus.Text = "SQL Conectado";
            }
            else
            {
                lblConfigResult.Text = "❌ Error de conexión: " + err;
                lblConfigResult.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#EF4444"));
                elDB.Fill = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#EF4444"));
                lblDBStatus.Text = "Sin conexión";
            }
        }

        // ─── Configuración: Generar alertas ─────────────────────────────
        private void BtnGenerarAlertas_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int generadas;
                string err;
                bool ok = DatabaseService.EjecutarGenerarAlertas(out generadas, out err);
                if (ok)
                {
                    lblConfigResult.Text = string.Format(
                        "✅ sp_GenerarAlertasInteligentes ejecutado. Nuevas alertas: {0}", generadas);
                    lblConfigResult.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#32A76B"));
                    LoadAllData();
                }
                else
                {
                    lblConfigResult.Text = "❌ Error al generar alertas: " + err;
                    lblConfigResult.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#EF4444"));
                }
            }
            catch (Exception ex)
            {
                lblConfigResult.Text = "❌ Excepción: " + ex.Message;
                lblConfigResult.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#EF4444"));
            }
        }
    }
}