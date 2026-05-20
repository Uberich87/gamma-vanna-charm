#region Using declarations
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.Options;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

public partial class GammaVannaCharmExposure
{
    [NinjaScriptProperty]
    [Display(Name = "Exposure metric", Description = "Card view and option-chain exposure metric. Overall uses Gamma for the histogram.", GroupName = "Exposure Profile", Order = 0)]
    public OptionExposureMetric ExposureMetric { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Use 0DTE options", Description = "Filters options to today's expiration when supported by the data source.", GroupName = "Exposure Profile", Order = 1)]
    public bool UseZeroDteOptions { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Card X offset pixels", Description = "Horizontal exposure-card offset from the fixed bottom-left corner. Positive values move right.", GroupName = "Exposure Card", Order = 0)]
    public int TableXOffsetPixels { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Card Y offset pixels", Description = "Vertical exposure-card offset from the fixed bottom-left corner. Positive values move down.", GroupName = "Exposure Card", Order = 1)]
    public int TableYOffsetPixels { get; set; }

    [NinjaScriptProperty]
    [Range(0.25, double.MaxValue)]
    [Display(Name = "Min row height ticks", Description = "Minimum histogram row height. SPX rows use 5-point strike buckets when that maps wider than this setting.", GroupName = "Histogram", Order = 0)]
    public double HeatmapBandTicks { get; set; }

    [NinjaScriptProperty]
    [Range(1, 100)]
    [Display(Name = "Max opacity", GroupName = "Histogram", Order = 1)]
    public int HeatmapMaxOpacity { get; set; }

    [NinjaScriptProperty]
    [Range(1, 100)]
    [Display(Name = "Max width %", Description = "Maximum horizontal exposure histogram width as a percentage of the visible price panel.", GroupName = "Histogram", Order = 2)]
    public int HeatmapMaxWidthPercent { get; set; }

    [XmlIgnore]
    [Display(Name = "Gamma positive", GroupName = "Histogram Colors", Order = 0)]
    public Brush GammaPositiveExposureBrush { get; set; }

    [Browsable(false)]
    public string GammaPositiveExposureBrushSerializable
    {
        get { return BrushToString(GammaPositiveExposureBrush); }
        set { GammaPositiveExposureBrush = StringToBrush(value); }
    }

    [XmlIgnore]
    [Display(Name = "Gamma negative", GroupName = "Histogram Colors", Order = 1)]
    public Brush GammaNegativeExposureBrush { get; set; }

    [Browsable(false)]
    public string GammaNegativeExposureBrushSerializable
    {
        get { return BrushToString(GammaNegativeExposureBrush); }
        set { GammaNegativeExposureBrush = StringToBrush(value); }
    }

    [XmlIgnore]
    [Display(Name = "Vanna positive", GroupName = "Histogram Colors", Order = 2)]
    public Brush VannaPositiveExposureBrush { get; set; }

    [Browsable(false)]
    public string VannaPositiveExposureBrushSerializable
    {
        get { return BrushToString(VannaPositiveExposureBrush); }
        set { VannaPositiveExposureBrush = StringToBrush(value); }
    }

    [XmlIgnore]
    [Display(Name = "Vanna negative", GroupName = "Histogram Colors", Order = 3)]
    public Brush VannaNegativeExposureBrush { get; set; }

    [Browsable(false)]
    public string VannaNegativeExposureBrushSerializable
    {
        get { return BrushToString(VannaNegativeExposureBrush); }
        set { VannaNegativeExposureBrush = StringToBrush(value); }
    }

    [XmlIgnore]
    [Display(Name = "Charm positive", GroupName = "Histogram Colors", Order = 4)]
    public Brush CharmPositiveExposureBrush { get; set; }

    [Browsable(false)]
    public string CharmPositiveExposureBrushSerializable
    {
        get { return BrushToString(CharmPositiveExposureBrush); }
        set { CharmPositiveExposureBrush = StringToBrush(value); }
    }

    [XmlIgnore]
    [Display(Name = "Charm negative", GroupName = "Histogram Colors", Order = 5)]
    public Brush CharmNegativeExposureBrush { get; set; }

    [Browsable(false)]
    public string CharmNegativeExposureBrushSerializable
    {
        get { return BrushToString(CharmNegativeExposureBrush); }
        set { CharmNegativeExposureBrush = StringToBrush(value); }
    }

    [NinjaScriptProperty]
    [Display(Name = "Proxy symbol", Description = "Auto selects SPX for ES/MES and QQQ for NQ/MNQ.", GroupName = "Options Data", Order = 0)]
    public SpotProxySymbol ExposureProxySymbol { get; set; }

    [NinjaScriptProperty]
    [Range(15, int.MaxValue)]
    [Display(Name = "Poll seconds", GroupName = "Options Data", Order = 1)]
    public int OptionsPollSeconds { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Options endpoint base", Description = "Options endpoint base URL used by the selected options-data provider.", GroupName = "Options Data", Order = 5)]
    public string OptionsEndpointBase { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Implied data directory", Description = "Directory for the durable SQLite seeded-flow exposure history database. Defaults to C:\\gvc.", GroupName = "Options Data", Order = 6)]
    public string ImpliedDataDirectory { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "SPY options symbol", Description = "Options symbol passed to the selected data source. The returned underlying price is used for mapping.", GroupName = "Options Data", Order = 2)]
    public string SpyOptionsSymbol { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "SPX options symbol", Description = "Options symbol passed to the selected data source. The returned underlying price is used for mapping.", GroupName = "Options Data", Order = 3)]
    public string SpxOptionsSymbol { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "QQQ options symbol", Description = "Options symbol passed to the selected data source. The returned underlying price is used for mapping.", GroupName = "Options Data", Order = 4)]
    public string QqqOptionsSymbol { get; set; }

    [NinjaScriptProperty]
    [Range(0.0, double.MaxValue)]
    [Display(Name = "SPY to ES multiplier", Description = "Converts SPY strike distance to ES-equivalent distance.", GroupName = "Futures Mapping", Order = 0)]
    public double SpyToEsMultiplier { get; set; }

    [NinjaScriptProperty]
    [Range(0.0, double.MaxValue)]
    [Display(Name = "SPX to ES multiplier", Description = "Converts SPX strike distance to ES-equivalent distance.", GroupName = "Futures Mapping", Order = 1)]
    public double SpxToEsMultiplier { get; set; }

    [NinjaScriptProperty]
    [Range(0.0, double.MaxValue)]
    [Display(Name = "QQQ to NQ multiplier", Description = "Converts QQQ strike distance to NQ-equivalent distance.", GroupName = "Futures Mapping", Order = 2)]
    public double QqqToNqMultiplier { get; set; }

    private static string BrushToString(Brush brush)
    {
        if (brush == null)
            return null;

        return new BrushConverter().ConvertToString(brush);
    }

    private static Brush StringToBrush(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var brush = new BrushConverter().ConvertFromString(value) as Brush;
        if (brush != null && brush.CanFreeze)
            brush.Freeze();

        return brush;
    }

    [Browsable(false)]
    [XmlIgnore]
    public string SpotPriceStatus
    {
        get
        {
            if (_spotPriceStatus != null)
                return _spotPriceStatus;
            return _optionExposureDataSource != null ? _optionExposureDataSource.LastError : null;
        }
    }

}
