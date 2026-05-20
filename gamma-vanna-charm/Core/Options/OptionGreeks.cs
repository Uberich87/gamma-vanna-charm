namespace GammaVannaCharm.Core.Options;

public sealed class OptionGreeks
{
    public OptionGreeks(double gamma, double vanna, double charm)
    {
        Gamma = gamma;
        Vanna = vanna;
        Charm = charm;
    }

    public double Gamma { get; private set; }

    public double Vanna { get; private set; }

    public double Charm { get; private set; }
}
