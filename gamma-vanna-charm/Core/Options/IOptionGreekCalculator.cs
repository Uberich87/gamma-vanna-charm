namespace GammaVannaCharm.Core.Options;

public interface IOptionGreekCalculator
{
    OptionGreeks Calculate(OptionGreekInput input);
}
