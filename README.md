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

## Assumptions and Design Decisions

### Compounding Convention
**Decision**: Continuous compounding throughout
**Rationale**: Mathematically convenient for derivatives pricing. Discount factors multiply cleanly: `DF(t) = exp(-r(t) * t)`

### Day Count Convention
**Decision**: Actual/Actual (ISDA)
**Rationale**: More accurate than fixed denominators (360, 365, 365.25). Counts actual days in each calendar year, divides by 365 or 366 for leap years. Standard for ISDA swap documentation.

### Dual-Curve Framework
**Decision**: Separate IBOR and discount curves with 38 bps spread
**Rationale**: Post-2008 market standard. IBOR curve projects forward rates, discount curve (OIS-based) discounts cash flows. The spread reflects credit/liquidity differences.

```
Discount Rate = IBOR Rate - 38 basis points
```

### 6M Point Treatment
**Decision**: Use 6M fixing directly as zero rate at t=0.5 years
**Rationale**: The 6M rate (4.11%) is a market fixing (actual observed IBOR rate), not a par swap rate. It requires no bootstrapping and represents the known rate for the first floating period.

### Swap Conventions
| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Receiver/Payer | Receiver | Receive fixed, pay floating |
| Fixed Frequency | Annual | Standard for EUR/GBP swaps |
| Float Frequency | Semi-annual | Standard IBOR payment frequency |
| Notional | $1,000,000 | Reference amount, not exchanged |
| Business Day Adj | None | Simplified for exercise |

### Bootstrapping Algorithm
**Decision**: Newton-Raphson iteration
**Rationale**: Quadratic convergence (3-5 iterations vs 50+ for bisection). For each tenor, solves for the zero rate that makes Float PV = Fixed PV.

### DV01 Methodology
**Decision**: Par rate bump + re-bootstrap (not zero rate shock)
**Rationale**: Industry standard used by Bloomberg, Murex, and major trading desks. Par rates are the traded instruments; re-bootstrapping captures how forward rates change realistically when market quotes move.

```
DV01 = PV(all par rates + 1bp) - PV(base)
```

Alternative (simpler but less accurate): Shift zero curve directly. This misses the non-linear relationship between par rates and zero rates.

### Gamma Calculation
**Decision**: Symmetric finite difference
**Rationale**: Standard second-derivative approximation:

```
Gamma = PV(+1bp) - 2*PV(base) + PV(-1bp)
```

### Cubic Spline Boundary Conditions
**Decision**: Natural spline with three constraints
**Rationale**: Ensures smooth curve behavior at endpoints and consistency at t=0.

| Constraint | Formula | Purpose |
|------------|---------|---------|
| Rate at t=0 | f(0) = f(6M) = 4.11% | Anchors short end |
| Left boundary | f''(0) = 0 | Natural spline condition |
| Right boundary | f''(10Y) = 0 | Natural spline condition |

**Note**: Due to C2 continuity requirements, interpolated rates between knots may deviate slightly from endpoint values (e.g., f(0.25) = 4.109% not exactly 4.11%).

### Clean vs Dirty PV
**Decision**: Clean PV excludes accrued interest
**Rationale**: For a receiver swap at T+3M:

```
Clean PV = Dirty PV - Fixed Accrual + Float Accrual
```

- Subtract fixed accrual: We receive this at next payment (remove from today's value)
- Add float accrual: We pay this at next payment (add back to today's value)

### Forward Rate Calculation
**Decision**: Simple compounding for forward rates
**Rationale**: Standard market convention for IBOR forwards:

```
F(t1, t2) = (DF(t1) / DF(t2) - 1) / tau
```

Where tau is the year fraction between t1 and t2.

## Key Formulas

| Formula | Expression |
|---------|------------|
| Discount Factor | `DF(t) = exp(-r(t) * t)` |
| Forward Rate | `F(t1,t2) = (DF1/DF2 - 1) / tau` |
| Par Swap Rate | `Par = Float_PV / (N * Annuity)` |
| Annuity | `Sum[ DF(ti) * tau_i ]` |
| Swap PV (Receiver) | `PV = Fixed_PV - Float_PV` |
| DV01 | `PV(+1bp) - PV(base)` |
| Gamma | `PV(+1bp) - 2*PV(0) + PV(-1bp)` |
