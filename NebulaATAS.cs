﻿namespace ATAS.Indicators.Technical
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Drawing;
    using ATAS.Indicators;
    using ATAS.Indicators.Drawing;
    using ATAS.Indicators.Technical.Properties;
    using OFT.Attributes.Editors;
    using OFT.Rendering.Context;
    using OFT.Rendering.Tools;
    using static ATAS.Indicators.Technical.SampleProperties;

    using Color = System.Drawing.Color;
    using MColor = System.Windows.Media.Color;
    using MColors = System.Windows.Media.Colors;
    using Pen = System.Drawing.Pen;
    using String = String;
    using System.Globalization;
    using OFT.Rendering.Settings;

    [DisplayName("Nebula")]
    public class Nebula : Indicator
    {
        #region PRIVATE FIELDS

        private struct bars
        {
            public String s;
            public int bar;
            public bool top;
        }

        private RenderStringFormat _format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        private decimal prevADX;
        private decimal pprevADX;
        private decimal prevSTR;
        private decimal prevsMDI;
        private decimal prevsPDI;
        private decimal prevVarMA;

        private const String sVersion = "1.0";
        private int iJunk = 0;
        private List<bars> lsBar = new List<bars>();
        private readonly PaintbarsDataSeries _paintBars = new("Paint bars");

        private int _lastBar = -1;
        private bool _lastBarCounted;

        private bool bVolumeImbalances = true;
        private bool bShowUp = true;
        private bool bShowDown = true;
        private bool bShowCloud = false;
        private int iOffset = 1;
        private int iFontSize = 10;
        private int iWaddaSensitivity = 150;
        private int iMACDSensitivity = 120;
        private int CandleColoring = 0;

        #endregion

        #region CONSTRUCTOR

        public Nebula() :
            base(true)
        {
            EnableCustomDrawing = true;
            DenyToChangePanel = true;
            SubscribeToDrawingEvents(DrawingLayouts.Historical);

            DataSeries[0] = _posSeries;
            DataSeries.Add(_negSeries);
            DataSeries.Add(_upCloud);
            DataSeries.Add(_dnCloud);
            DataSeries.Add(_paintBars);
            DataSeries.Add(_fanVMA);

            Add(_kama9);
            Add(_kama21);
            Add(_sq);
            Add(_psar);
            Add(_st);
            Add(_atr);
        }

        #endregion

        #region INDICATORS

        private readonly EMA McGinleyDynamic = new EMA() { Period = 14 };
        private readonly SMA sma = new SMA() { Period = 6 };
        private readonly EMA _short = new() { Period = 3 };
        private readonly EMA _long = new() { Period = 10 };
        private readonly EMA _signal = new() { Period = 16 };
        private readonly RSI _rsi = new() { Period = 14 };
        private readonly ATR _atr = new() { Period = 14 };
        private readonly AwesomeOscillator _ao = new AwesomeOscillator();
        private readonly ParabolicSAR _psar = new ParabolicSAR();
        private readonly EMA fastEma = new EMA() { Period = 20 };
        private readonly EMA slowEma = new EMA() { Period = 40 };
        private readonly SuperTrend _st = new SuperTrend() { Period = 10, Multiplier = 1m };
        private readonly BollingerBands _bb = new BollingerBands() { Period = 20, Shift = 0, Width = 2 };
        private readonly KAMA _kama9 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 9 };
        private readonly KAMA _kama21 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 21 };
        private readonly SqueezeMomentum _sq = new SqueezeMomentum() { BBPeriod = 20, BBMultFactor = 2, KCPeriod = 20, KCMultFactor = 1.5m, UseTrueRange = false };

        #endregion

        #region RENDER CONTEXT

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (ChartInfo is null || InstrumentInfo is null)
                return;

            FontSetting Font = new("Arial", iFontSize);
            var renderString = "Howdy";
            var stringSize = context.MeasureString(renderString, Font.RenderObject);
            int x4 = 0;
            int y4 = 0;

            for (var bar = FirstVisibleBarNumber; bar <= LastVisibleBarNumber; bar++)
            {
                renderString = bar.ToString(CultureInfo.InvariantCulture);
                stringSize = context.MeasureString(renderString, Font.RenderObject);

                foreach (bars ix in lsBar)
                {
                    if (ix.bar == bar)
                    {
                        renderString = ix.s.ToString(CultureInfo.InvariantCulture);
                        stringSize = context.MeasureString(renderString, Font.RenderObject);
                        x4 = ChartInfo.GetXByBar(bar, false);
                        y4 = Offset;
                        if (ix.top)
                        {
                            var high = GetCandle(bar).High;
                            y4 += ChartInfo.GetYByPrice(high + (InstrumentInfo.TickSize * Offset) * 2, false);
                            context.DrawString(renderString, Font.RenderObject, Color.Orange, x4, y4, _format);
                        }
                        else
                        {
                            var low = GetCandle(bar).Low;
                            y4 += ChartInfo.GetYByPrice(low - InstrumentInfo.TickSize * Offset, false);
                            context.DrawString(renderString, Font.RenderObject, Color.Lime, x4, y4, _format);
                        }
                        break;
                    }
                }
            }
        }

        #endregion

        #region DATA SERIES

        [Display(Name = "Font Size", GroupName = "Drawing", Order = int.MaxValue)]
        [Range(1, 90)]
        public int TextFont { get => iFontSize; set { iFontSize = value; RecalculateValues(); } }

        [Display(Name = "Text Offset", GroupName = "Drawing", Order = int.MaxValue)]
        [Range(0, 900)]
        public int Offset { get => iOffset; set { iOffset = value; RecalculateValues(); } }

        private ValueDataSeries _fanVMA = new("Fantail VMA") { VisualType = VisualMode.Line, Color = DefaultColors.Yellow.Convert(), Width = 3 };
        private readonly ValueDataSeries _posSeries = new("Regular Buy Signal") { Color = MColor.FromArgb(255, 0, 255, 0), VisualType = VisualMode.UpArrow, Width = 2 };
        private readonly ValueDataSeries _negSeries = new("Regular Sell Signal") { Color = MColor.FromArgb(255, 255, 0, 0), VisualType = VisualMode.DownArrow, Width = 2 };
        private RangeDataSeries _upCloud = new("Up Cloud") { RangeColor = MColor.FromArgb(73, 0, 255, 0), DrawAbovePrice = false };
        private RangeDataSeries _dnCloud = new("Down Cloud") { RangeColor = MColor.FromArgb(73, 255, 0, 0), DrawAbovePrice = false };

        #endregion

        #region SETTINGS

        private class candleColor : Collection<Entity>
        {
            public candleColor()
                : base(new[]
                {
                    new Entity { Value = 1, Name = "None" },
                    new Entity { Value = 2, Name = "Waddah Explosion" },
                    new Entity { Value = 3, Name = "Linda MACD" },
                    new Entity { Value = 4, Name = "Squeeze" },
                    new Entity { Value = 5, Name = "Delta" }
                })
            { }
        }
        [Display(Name = "Candle Color", GroupName = "Colored Candles")]
        [ComboBoxEditor(typeof(candleColor), DisplayMember = nameof(Entity.Name), ValueMember = nameof(Entity.Value))]
        public int canColor
        {
            get => CandleColoring; set { if (value < 0) return; CandleColoring = value; RecalculateValues(); }
        }

        [Display(GroupName = "Colored Candles", Name = "Waddah Sensitivity")]
        [Range(0, 9000)]
        public int WaddaSensitivity { get => iWaddaSensitivity; set { if (value < 0) return; iWaddaSensitivity = value;RecalculateValues(); } }
        [Display(GroupName = "Colored Candles", Name = "MACD Sensitivity")]
        [Range(0, 9000)]
        public int MACDSensitivity { get => iMACDSensitivity; set { if (value < 0) return; iMACDSensitivity = value; RecalculateValues(); } }

        [Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "UseAlerts")]
        public bool UseAlerts { get; set; }
        [Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "AlertFile")]
        public string AlertFile { get; set; } = "alert1";

        [Display(GroupName = "Extras", Name = "Show Nebula Cloud", Description = "Show cloud containing KAMA 9 and 21")]
        public bool Use_Cloud { get => bShowCloud; set { bShowCloud = value; RecalculateValues(); } }

        #endregion

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                DataSeries.ForEach(x => x.Clear());
                HorizontalLinesTillTouch.Clear();
                _lastBarCounted = false;
                return;
            }
            if (bar < 6)
                return;

            #region CANDLE CALCULATIONS

            var candle = GetCandle(bar - 1);
            var pbar = bar - 1;
            value = candle.Close;
            var chT = ChartInfo.ChartType;

            bShowDown = true;
            bShowUp = true;

            decimal _tick = ChartInfo.PriceChartContainer.Step;
            var p1C = GetCandle(pbar - 1);
            var p2C = GetCandle(pbar - 2);
            var p3C = GetCandle(pbar - 3);
            var p4C = GetCandle(pbar - 4);

            var red = candle.Close < candle.Open;
            var green = candle.Close > candle.Open;
            var c0G = candle.Open < candle.Close;
            var c0R = candle.Open > candle.Close;
            var c1G = p1C.Open < p1C.Close;
            var c1R = p1C.Open > p1C.Close;
            var c2G = p2C.Open < p2C.Close;
            var c2R = p2C.Open > p2C.Close;
            var c3G = p3C.Open < p3C.Close;
            var c3R = p3C.Open > p3C.Close;
            var c4G = p4C.Open < p4C.Close;
            var c4R = p4C.Open > p4C.Close;

            var c0Body = Math.Abs(candle.Close - candle.Open);
            var c1Body = Math.Abs(p1C.Close - p1C.Open);
            var c2Body = Math.Abs(p2C.Close - p2C.Open);
            var c3Body = Math.Abs(p3C.Close - p3C.Open);
            var c4Body = Math.Abs(p4C.Close - p4C.Open);

            var upWickLarger = c0R && Math.Abs(candle.High - candle.Open) > Math.Abs(candle.Low - candle.Close);
            var downWickLarger = c0G && Math.Abs(candle.Low - candle.Open) > Math.Abs(candle.Close - candle.High);

            if (bVolumeImbalances)
            {
                var highPen = new Pen(new SolidBrush(Color.RebeccaPurple)) { Width = 2 };
                if (green && c1G && candle.Open > p1C.Close)
                {
                    HorizontalLinesTillTouch.Add(new LineTillTouch(pbar, candle.Open, highPen));
                }
                if (red && c1R && candle.Open < p1C.Close)
                {
                    HorizontalLinesTillTouch.Add(new LineTillTouch(pbar, candle.Open, highPen));
                }
            }

            #endregion

            #region INDICATORS CALCULATE

            fastEma.Calculate(pbar, value);
            slowEma.Calculate(pbar, value);
            _bb.Calculate(pbar, value);
            _rsi.Calculate(pbar, value);

            var ao = ((ValueDataSeries)_ao.DataSeries[0])[pbar];
            var kama9 = ((ValueDataSeries)_kama9.DataSeries[0])[pbar];
            var kama21 = ((ValueDataSeries)_kama9.DataSeries[0])[pbar];
            var fast = ((ValueDataSeries)fastEma.DataSeries[0])[pbar];
            var fastM = ((ValueDataSeries)fastEma.DataSeries[0])[pbar - 1];
            var slow = ((ValueDataSeries)slowEma.DataSeries[0])[pbar];
            var slowM = ((ValueDataSeries)slowEma.DataSeries[0])[pbar - 1];
            var sq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar];
            var sq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar];
            var psq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar - 1];
            var psq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar - 1];
            var ppsq1 = ((ValueDataSeries)_sq.DataSeries[0])[pbar - 2];
            var ppsq2 = ((ValueDataSeries)_sq.DataSeries[1])[pbar - 2];
            var psar = ((ValueDataSeries)_psar.DataSeries[0])[pbar];
            var bb_mid = ((ValueDataSeries)_bb.DataSeries[0])[pbar]; // mid
            var bb_top = ((ValueDataSeries)_bb.DataSeries[1])[pbar]; // top
            var bb_bottom = ((ValueDataSeries)_bb.DataSeries[2])[pbar]; // bottom
            var rsi = ((ValueDataSeries)_rsi.DataSeries[0])[pbar];
            var rsi1 = ((ValueDataSeries)_rsi.DataSeries[0])[pbar - 1];
            var rsi2 = ((ValueDataSeries)_rsi.DataSeries[0])[pbar - 2];

            var psarBuy = (psar < candle.Close);
            var psarSell = (psar > candle.Close);

            #endregion

            var t1 = ((fast - slow) - (fastM - slowM)) * iWaddaSensitivity;

            if (_lastBar != bar)
            {
                if (_lastBarCounted && UseAlerts)
                {
                    if (bVolumeImbalances)
                        if ((green && c1G && candle.Open > p1C.Close) || (red && c1R && candle.Open < p1C.Close))
                            AddAlert(AlertFile, "Volume Imbalance");

                    if (bShowUp)
                        AddAlert(AlertFile, "BUY Signal");
                    else if (bShowDown)
                        AddAlert(AlertFile, "BUY Signal");
                }
                _lastBar = bar;
            }
            else
            {
                if (!_lastBarCounted)
                    _lastBarCounted = true;
            }

            // LINDA MACD
            var macd = _short.Calculate(bar, value) - _long.Calculate(bar, value);
            var signal = _signal.Calculate(bar, macd);
            var linda = macd - signal;

            var waddah = Math.Min(Math.Abs(t1) + 70, 255);
            if (canColor == 2)
                _paintBars[pbar] = t1 > 0 ? MColor.FromArgb(255, 0, (byte)waddah, 0) : MColor.FromArgb(255, (byte)waddah, 0, 0);

            var filteredLindaMACD = Math.Min(Math.Abs(linda * iMACDSensitivity), 255);
            if (canColor == 3)
                _paintBars[pbar] = linda > 0 ? MColor.FromArgb(255, 0, (byte)filteredLindaMACD, 0) : MColor.FromArgb(255, (byte)filteredLindaMACD, 0, 0);

            var filteredSQ = Math.Min(Math.Abs(sq1 * 25), 255);
            if (canColor == 4)
                _paintBars[pbar] = sq1 > 0 ? MColor.FromArgb(255, 0, (byte)filteredSQ, 0) : MColor.FromArgb(255, (byte)filteredSQ, 0, 0);

            var filteredDelta = Math.Min(Math.Abs(candle.Delta), 255);
            if (canColor == 5)
                _paintBars[pbar] = candle.Delta > 0 ? MColor.FromArgb(255, 0, (byte)filteredDelta, 0) : MColor.FromArgb(255, (byte)filteredDelta, 0, 0);

            // Nebula cloud
            if (bShowCloud)
                if (_kama9[pbar] > _kama21[pbar])
                {
                    _upCloud[pbar].Upper = _kama9[pbar];
                    _upCloud[pbar].Lower = _kama21[pbar];
                }
                else
                {
                    _dnCloud[pbar].Upper = _kama21[pbar];
                    _dnCloud[pbar].Lower = _kama9[pbar];
                }

            #region FANTAIL VMA

            var VMA = candle.Close;
            var VarMA = candle.Close;
            var MA = candle.Close;
            var STR = candle.High - candle.Low;
            decimal sPDI = 0.0m;
            decimal sMDI = 0.0m;
            decimal ADX = 0.0m;
            var ADXR = 0.0;

            var Hi = candle.High;
            var Hi1 = p1C.High;
            var Lo = candle.Low;
            var Lo1 = p1C.Low;
            var Close1 = p1C.Close;

            var Bulls1 = 0.5m * (Math.Abs(Hi - Hi1) + (Hi - Hi1));
            var Bears1 = 0.5m * (Math.Abs(Lo1 - Lo) + (Lo1 - Lo));

            var Bears = Bulls1 > Bears1 ? 0 : (Bulls1 == Bears1 ? 0 : Bears1);
            var Bulls = Bulls1 < Bears1 ? 0 : (Bulls1 == Bears1 ? 0 : Bulls1);

            if (bar > 0)
            {
                sPDI = (10.0m * prevsPDI + Bulls) / (10.0m + 1);
                sMDI = (10.0m * prevsMDI + Bears) / (10.0m + 1);
            }

            var TR = Math.Max(Hi - Lo, Hi - Close1);
            if (bar > 0)
                STR = (10.0m * prevSTR + TR) / (10.0m + 1);

            var PDI = STR > 0 ? sPDI / STR : 0;
            var MDI = STR > 0 ? sMDI / STR : 0;
            var DX = (PDI + MDI) > 0 ? Math.Abs(PDI - MDI) / (PDI + MDI) : 0;
            if (bar > 0)
                ADX = (10.0m * prevADX + DX) / (10.0m + 1);
            var vADX = ADX;

            var adxlow = pprevADX < prevADX ? pprevADX : prevADX;
            var adxmax = pprevADX > prevADX ? pprevADX : prevADX;
            var ADXmin = Math.Min(1000000.0m, adxlow);
            var ADXmax = Math.Max(-1.0m, adxmax);
            var Diff = ADXmax - ADXmin;
            var Const = Diff > 0 ? (vADX - ADXmin) / Diff : 0;
            if (bar > 0)
                VarMA = ((2 - Const) * p1C.Close + Const * candle.Close) / 2;

            sma.Calculate(bar, VarMA);
            var FanVMA = ((ValueDataSeries)sma.DataSeries[0])[bar];
            //_fanVMA[bar] = FanVMA;

            prevVarMA = VarMA;
            pprevADX = prevADX;
            prevADX = ADX;
            prevSTR = STR;
            prevsMDI = sMDI;
            prevsPDI = sPDI;

            #endregion

//            double md = this.GetPrice(this.SourcePrice);
//            double value = this.ema.GetValue(1);
//            md = value + (md - value) / (this.TrackingFactor * Math.Pow(md / value, 4));

            decimal md = candle.Close;
            McGinleyDynamic.Calculate(pbar, value);
            var val = ((ValueDataSeries)McGinleyDynamic.DataSeries[0])[pbar-1];
            decimal mdDivVal = (md / val);
            decimal mdMinusValPlusVal = val + (md - val);
            double mdMinusValPlusVal1 = Convert.ToDouble(mdMinusValPlusVal);
            double eat = mdMinusValPlusVal1 / (2 * Math.Pow((double)mdDivVal, 4));

            _fanVMA[bar] = (decimal)eat;

        }

    }
}
