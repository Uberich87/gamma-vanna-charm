namespace GammaVannaCharm.Core.Presentation;

public sealed class HistogramRenderLevel
{
    public HistogramRenderLevel(double strike, double price, double priceBand, double metricValue, double intensity, double opacity)
    {
        Strike = strike;
        Price = price;
        PriceBand = priceBand;
        MetricValue = metricValue;
        Intensity = intensity;
        Opacity = opacity;
    }

    public double Strike { get; private set; }

    public double Price { get; private set; }

    public double PriceBand { get; private set; }

    public double MetricValue { get; private set; }

    public double Intensity { get; private set; }

    public double Opacity { get; private set; }
}
