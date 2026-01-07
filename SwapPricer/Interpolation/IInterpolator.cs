namespace SwapPricer.Interpolation;

/// <summary>
/// Interface for interpolation strategies.
/// </summary>
public interface IInterpolator
{
    double Interpolate(double t);
}
