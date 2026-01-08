"""
Generate Interview Guide PDF for Interest Rate Swap Pricing System
"""
from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        if self.page_no() > 1:
            self.set_font('Helvetica', 'B', 10)
            self.cell(0, 10, 'Interest Rate Swap Pricing - Interview Guide', border=0, align='C')
            self.ln(5)

    def footer(self):
        self.set_y(-15)
        self.set_font('Helvetica', 'I', 8)
        self.cell(0, 10, f'Page {self.page_no()}', border=0, align='C')

    def chapter_title(self, title):
        self.set_font('Helvetica', 'B', 14)
        self.set_fill_color(230, 230, 230)
        self.cell(0, 10, title, border=0, fill=True)
        self.ln(12)

    def section_title(self, title):
        self.set_font('Helvetica', 'B', 11)
        self.cell(0, 8, title, border=0)
        self.ln(8)

    def body_text(self, text):
        self.set_font('Helvetica', '', 10)
        self.multi_cell(0, 5, text, new_x="LMARGIN", new_y="NEXT")
        self.ln(3)

    def code_block(self, code):
        self.set_font('Courier', '', 8)
        self.set_fill_color(245, 245, 245)
        self.multi_cell(0, 4, code, border=0, fill=True, new_x="LMARGIN", new_y="NEXT")
        self.ln(3)

    def qa_block(self, question, answer):
        self.set_font('Helvetica', 'B', 10)
        self.set_text_color(0, 80, 0)
        self.multi_cell(0, 5, f"Q: {question}", new_x="LMARGIN", new_y="NEXT")
        self.set_text_color(0, 0, 0)
        self.set_font('Helvetica', '', 10)
        self.multi_cell(0, 5, f"A: {answer}", new_x="LMARGIN", new_y="NEXT")
        self.ln(3)

    def formula(self, formula):
        self.set_font('Courier', 'B', 9)
        self.set_fill_color(255, 255, 220)
        self.cell(0, 7, f"  {formula}", border=0, fill=True)
        self.ln(8)

# Create PDF
pdf = PDF()
pdf.set_auto_page_break(auto=True, margin=15)
pdf.set_left_margin(15)
pdf.set_right_margin(15)
pdf.add_page()

# Title Page
pdf.set_font('Helvetica', 'B', 28)
pdf.ln(50)
pdf.cell(0, 15, 'Interest Rate Swap', border=0, align='C')
pdf.ln(15)
pdf.cell(0, 15, 'Pricing System', border=0, align='C')
pdf.ln(20)
pdf.set_font('Helvetica', 'B', 20)
pdf.cell(0, 10, 'Interview Guide', border=0, align='C')
pdf.ln(30)
pdf.set_font('Helvetica', '', 14)
pdf.cell(0, 8, 'AVM Programming Exercise', border=0, align='C')
pdf.ln(10)
pdf.cell(0, 8, 'C# Implementation', border=0, align='C')

# Section 1: Architecture
pdf.add_page()
pdf.chapter_title('1. Architecture Overview')

pdf.body_text('''The system is organized into a clean layered architecture:

SwapPricer/
  Program.cs - Entry point, orchestrates all questions
  Models/
    Curve.cs - Zero rate curve with interpolation
    Swap.cs - Swap definition + cash flow generation
  Services/
    CurveBootstrapper.cs - Bootstrap zero curves from par rates
    SwapPricerService.cs - PV calculations, par rate, accruals
    RiskCalculator.cs - DV01, Gamma calculations
  Interpolation/
    LinearInterpolator.cs - Linear interpolation (Q1-Q3)
    CubicSplineInterpolator.cs - Natural cubic spline (Q4)
  Utils/
    DateUtils.cs - Actual/Actual day count convention''')

# Section 2: Key Concepts
pdf.add_page()
pdf.chapter_title('2. Key Concepts')

pdf.section_title('What is an Interest Rate Swap?')
pdf.body_text('''An interest rate swap exchanges fixed rate payments for floating rate payments:

- Fixed Leg: Pay/receive a fixed rate annually
- Float Leg: Pay/receive IBOR rate semi-annually
- Receiver Swap: Receive fixed, pay float (our convention)
- Notional: $1,000,000 (not exchanged, just for calculating payments)''')

pdf.section_title('Dual-Curve Framework (Post-2008)')
pdf.body_text('''After the 2008 financial crisis, the market moved to dual-curve pricing:

- IBOR Curve: Used for projecting forward rates
- Discount Curve: Used for discounting cash flows to present value

In our implementation: Discount Rate = IBOR Rate - 38 basis points

This spread reflects the credit/liquidity difference between IBOR and OIS rates.''')

pdf.section_title('Continuous Compounding')
pdf.body_text('We use continuous compounding. The discount factor formula is:')
pdf.formula('DF(t) = exp(-r(t) * t)')

# Section 3: Bootstrapping
pdf.add_page()
pdf.chapter_title('3. Curve Bootstrapping (Question 1)')

pdf.section_title('The Problem')
pdf.body_text('''We have market data:
- 6M IBOR fixing: 4.11% (direct observation)
- Par swap rates: 1Y=4.14%, 2Y=3.73%, 3Y=3.48%, 5Y=3.21%, 7Y=3.11%, 10Y=3.08%

We need to find zero rates that make each par swap have NPV = 0.''')

pdf.section_title('The Algorithm')
pdf.body_text('''1. Start with 6M fixing - use directly as zero rate at t=0.5

2. For each tenor (1Y, 2Y, 3Y, 5Y, 7Y, 10Y):
   a. Guess the IBOR zero rate
   b. Calculate Float Leg PV using forward rates
   c. Calculate Fixed Leg PV using the par rate
   d. Use Newton-Raphson to adjust until Float PV = Fixed PV
   e. Store the solved zero rate''')

pdf.section_title('Key Formulas')
pdf.body_text('Float Leg PV:')
pdf.formula('Float PV = Sum[ forward_rate * tau * DF(t_pay) ]')

pdf.body_text('Fixed Leg PV:')
pdf.formula('Fixed PV = par_rate * Sum[ tau * DF(t_pay) ]')

pdf.body_text('Forward Rate from discount factors:')
pdf.formula('F(t1, t2) = (DF(t1) / DF(t2) - 1) / tau')

pdf.qa_block(
    "Why use Newton-Raphson instead of bisection?",
    "Newton-Raphson converges quadratically (much faster), typically 3-5 iterations vs 50+ for bisection."
)

# Section 4: Swap Pricing
pdf.add_page()
pdf.chapter_title('4. Swap Pricing (Question 2)')

pdf.section_title('Par Rate Calculation')
pdf.body_text('The par rate is the fixed rate that makes the swap NPV = 0:')
pdf.formula('Par Rate = Float PV / (Notional * Annuity)')

pdf.body_text('Where Annuity is the sum of discounted day fractions:')
pdf.formula('Annuity = Sum[ DF(t_i) * tau_i ]')

pdf.section_title('DV01 Calculation')
pdf.body_text('''DV01 measures the change in PV for a 1 basis point move in rates.

Our methodology (industry standard):
1. Bump ALL par swap rates by +1bp
2. Keep the 6M fixing unchanged
3. Re-bootstrap the curve from shocked par rates
4. Calculate new PV
5. DV01 = PV(shocked) - PV(base)''')

pdf.formula('DV01 = PV(+1bp) - PV(base)')

pdf.qa_block(
    "Why re-bootstrap instead of shifting the zero curve?",
    "Par rates are the traded instruments. Re-bootstrapping captures how forward rates change realistically. This is what Bloomberg and Murex do."
)

pdf.section_title('Gamma Calculation')
pdf.body_text('Gamma measures convexity (second derivative):')
pdf.formula('Gamma = PV(+1bp) - 2*PV(0) + PV(-1bp)')

# Section 5: Forward Valuation
pdf.add_page()
pdf.chapter_title('5. Forward Valuation (Question 3)')

pdf.section_title('Valuation at T+3 Months')
pdf.body_text('''At T+3M, we need to:
1. Calculate accrued interest on both legs
2. Calculate the remaining cash flow PVs
3. Compute Clean PV = Dirty PV - Net Accrual''')

pdf.section_title('Accrual Calculation')
pdf.body_text('''Fixed Accrual = Fixed Rate * Notional * (days accrued / days in period)
Float Accrual = Float Rate * Notional * (days accrued / days in period)

For the first floating period, we use the known 6M fixing rate (4.11%).''')

pdf.section_title('Clean vs Dirty PV')
pdf.formula('Clean PV = Dirty PV - Fixed Accrual + Float Accrual')

pdf.qa_block(
    "Why subtract fixed accrual and add float accrual?",
    "For a receiver swap: We receive fixed accrual at next payment (remove from today). We pay float accrual at next payment (add back)."
)

# Section 6: Cubic Spline
pdf.add_page()
pdf.chapter_title('6. Cubic Spline Interpolation (Question 4)')

pdf.section_title('Boundary Conditions')
pdf.body_text('''Natural cubic spline with three constraints:

1. f(0) = f(6M) - Rate at t=0 equals rate at 6M (4.11%)
2. f''(0) = 0 - Zero second derivative at left boundary
3. f''(10Y) = 0 - Zero second derivative at right boundary''')

pdf.section_title('Spline Formula')
pdf.body_text('On each interval, the spline is a cubic polynomial:')
pdf.formula('S(x) = a + b(x-xi) + c(x-xi)^2 + d(x-xi)^3')

pdf.qa_block(
    "Why does f(0.25) not equal 4.11%?",
    "The spline must maintain C2 continuity at each knot. To smoothly transition to the next segment where rates drop, the cubic polynomial curves slightly between endpoints."
)

pdf.section_title('Impact on PV')
pdf.body_text('''Linear Clean PV: -$368.65
Spline Clean PV: +$157.85
Difference: $526.50''')

# Section 7: Day Count
pdf.add_page()
pdf.chapter_title('7. Day Count Convention')

pdf.section_title('Actual/Actual (ISDA)')
pdf.body_text('''We use Actual/Actual (ISDA) day count convention:

- Count actual days in each calendar year
- Divide by 365 (non-leap) or 366 (leap year)
- Sum across years if period spans multiple years

Example: Jan 7 to Apr 7, 2026
Days = 90, Year Fraction = 90/365 = 0.2466''')

pdf.qa_block(
    "Why use Actual/Actual instead of 30/360?",
    "More accurate for actual cash flows. Standard for ISDA documentation in the swap market."
)

# Section 8: Key Formulas
pdf.add_page()
pdf.chapter_title('8. Formula Summary')

pdf.set_font('Helvetica', '', 10)
formulas = [
    ("Discount Factor", "DF(t) = exp(-r(t) * t)"),
    ("Forward Rate", "F(t1,t2) = (DF1/DF2 - 1) / tau"),
    ("Par Swap Rate", "Par Rate = Float PV / (N * Annuity)"),
    ("Annuity", "Annuity = Sum[ DF(ti) * tau_i ]"),
    ("Swap PV (Receiver)", "PV = Fixed PV - Float PV"),
    ("DV01", "DV01 = PV(+1bp) - PV(base)"),
    ("Gamma", "Gamma = PV(+1bp) - 2*PV(0) + PV(-1bp)"),
    ("Clean PV", "Clean = Dirty - FixAccr + FloatAccr"),
    ("Discount Spread", "Disc_Rate = IBOR_Rate - 38bps"),
]

for name, formula in formulas:
    pdf.set_font('Helvetica', 'B', 10)
    pdf.cell(55, 7, name + ":")
    pdf.set_font('Courier', '', 9)
    pdf.cell(0, 7, formula)
    pdf.ln(7)

# Section 9: Assumptions & Design Decisions
pdf.add_page()
pdf.chapter_title('9. Assumptions & Design Decisions')

pdf.section_title('Compounding Convention')
pdf.body_text('Decision: Continuous compounding throughout. Rationale: Mathematically convenient for derivatives pricing. Discount factors multiply cleanly.')

pdf.section_title('Day Count Convention')
pdf.body_text('Decision: Actual/Actual (ISDA). Rationale: More accurate than fixed denominators (360, 365, 365.25). Standard for ISDA swap documentation.')

pdf.section_title('Dual-Curve Framework')
pdf.body_text('Decision: Separate IBOR and discount curves with 38 bps spread. Rationale: Post-2008 market standard. IBOR curve projects forward rates, discount curve (OIS-based) discounts cash flows.')

pdf.section_title('6M Point Treatment')
pdf.body_text('Decision: Use 6M fixing directly as zero rate at t=0.5 years. Rationale: The 6M rate (4.11%) is a market fixing, not a par swap rate. No bootstrapping required.')

pdf.section_title('Bootstrapping Algorithm')
pdf.body_text('Decision: Newton-Raphson iteration. Rationale: Quadratic convergence (3-5 iterations vs 50+ for bisection). Solves for zero rate making Float PV = Fixed PV.')

pdf.section_title('DV01 Methodology')
pdf.body_text('Decision: Par rate bump + re-bootstrap (not zero rate shock). Rationale: Industry standard used by Bloomberg, Murex. Par rates are traded instruments; re-bootstrapping captures realistic forward rate changes.')

pdf.section_title('Cubic Spline Boundary Conditions')
pdf.body_text('''Decision: Natural spline with three constraints:
- f(0) = f(6M) = 4.11% (anchors short end)
- f''(0) = 0 (natural spline left boundary)
- f''(10Y) = 0 (natural spline right boundary)

Note: C2 continuity causes slight deviations between knots.''')

pdf.section_title('Swap Conventions')
pdf.body_text('''- Receiver Swap: Receive fixed, pay floating
- Fixed Frequency: Annual
- Float Frequency: Semi-annual
- Notional: $1,000,000 (not exchanged)
- Business Day Adjustment: None (simplified)''')

# Section 10: Interview Questions
pdf.add_page()
pdf.chapter_title('10. Common Interview Questions')

questions = [
    ("Why use continuous compounding?",
     "Mathematically convenient - DFs multiply nicely. Standard in derivatives pricing."),

    ("What happens to swap value when rates rise?",
     "Receiver swap loses value. The fixed rate becomes less attractive. DV01 is negative."),

    ("Why is the curve inverted?",
     "Short rates (4.11%) > long rates (3.08%). Market expects rate cuts."),

    ("What's the difference between IBOR and discount curves?",
     "IBOR: project future floating payments. Discount: calculate present values."),

    ("What does the 6M fixing represent?",
     "Known IBOR rate for first floating period. Rate is fixed, not projected."),
]

for q, a in questions:
    pdf.qa_block(q, a)

# Section 11: Results Summary
pdf.add_page()
pdf.chapter_title('11. Results Summary')

pdf.section_title('Question 1: Bootstrapped Curves')
pdf.code_block('''IBOR Curve:
Time(Y)  Zero Rate(%)  Discount Factor
0.50     4.110000      0.97965971
1.00     4.080910      0.96001238
2.00     3.669618      0.92923615
3.00     3.419598      0.90249878
5.00     3.145586      0.85446536
7.00     3.047966      0.80786719
10.00    3.021464      0.73922980''')

pdf.section_title('Question 2: 9Y Par Swap')
pdf.code_block('''Par Swap Rate: 3.088905%
DV01: -$784.28
Gamma: $0.73''')

pdf.section_title('Question 3: T+3M Valuation (Linear)')
pdf.code_block('''Fixed Leg Accrual: $7,616.48
Float Leg Accrual: $10,134.25
Net Accrual: -$2,517.77
Dirty PV: -$2,886.42
Clean PV: -$368.65''')

pdf.section_title('Question 4: T+3M Valuation (Spline)')
pdf.code_block('''Clean PV (Spline): +$157.85
Difference from Linear: $526.50''')

# Save
output_path = r'C:\Users\Miller\AVM-Swap-Exercise\Interview_Guide.pdf'
pdf.output(output_path)
print(f"PDF saved to: {output_path}")
