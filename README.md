# AVM Swap Exercise

Interest rate swap pricing implementation in C# for AVM programming exercise.

## Requirements

- .NET 6.0 SDK or later

## Building and Running

```bash
cd SwapPricer
dotnet build
dotnet run
```

## Project Structure

```
SwapPricer/
├── Program.cs                      # Main entry point
├── Models/
│   ├── Curve.cs                    # Zero rate curve with interpolation
│   └── Swap.cs                     # Swap definition and cash flows
├── Services/
│   ├── CurveBootstrapper.cs        # Bootstraps curves from par rates
│   ├── SwapPricerService.cs        # PV calculations
│   └── RiskCalculator.cs           # DV01 and Gamma calculations
├── Interpolation/
│   ├── IInterpolator.cs            # Interpolation interface
│   ├── LinearInterpolator.cs       # Linear interpolation
│   └── CubicSplineInterpolator.cs  # Cubic spline interpolation
└── Utils/
    └── DateUtils.cs                # Actual/Actual day count
```

## Features

### Question 1: Curve Construction
- Bootstraps IBOR zero curve from 6M fixing and par swap rates
- Creates discount curve with -38 bps spread
- Conventions: semi-annual float, annual fixed, Actual/Actual, no business day adjustment

### Question 2: 9Y Par Swap Pricing
- Calculates par swap rate
- Computes DV01 (dollar value of 1 basis point)
- Computes Gamma (second derivative)

### Question 3: 3-Month Forward Valuation
- Calculates fixed and floating leg accruals
- Computes clean and dirty PV

### Question 4: Cubic Spline Interpolation
- Natural cubic spline with boundary conditions:
  - f(0) = f(6M)
  - f''(0) = f''(10Y) = 0
- Recalculates Q3 metrics with spline interpolation

## Market Data

| Tenor | Rate (%) | Type |
|-------|----------|------|
| 6M    | 4.11     | IBOR Fixing |
| 1Y    | 4.14     | Par Swap Rate |
| 2Y    | 3.73     | Par Swap Rate |
| 3Y    | 3.48     | Par Swap Rate |
| 5Y    | 3.21     | Par Swap Rate |
| 7Y    | 3.11     | Par Swap Rate |
| 10Y   | 3.08     | Par Swap Rate |
