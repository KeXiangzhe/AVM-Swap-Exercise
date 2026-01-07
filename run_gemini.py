import math
import numpy as np

def solve_questions():
    # --- 1. Corrected Bootstrap (Question 1) ---
    rates_input = {1: 0.0414, 2: 0.0373, 3: 0.0348, 5: 0.0321, 7: 0.0311, 10: 0.0308}
    fixing_6m = 0.0411
    spread_discount = -0.0038

    # Store Zero Rates (Continuous Compounding)
    zeros_ibor = {}
    zeros_disc = {}

    # 6M Point
    zeros_ibor[0.5] = fixing_6m
    zeros_disc[0.5] = fixing_6m + spread_discount

    # Helper: Get interpolated DF
    def get_df(t, zero_curve):
        times = sorted(zero_curve.keys())
        if t <= times[0]:
            r = zero_curve[times[0]]
        elif t >= times[-1]:
            r = zero_curve[times[-1]]
        elif t in zero_curve:
            r = zero_curve[t]
        else:
            t1 = max([x for x in times if x <= t])
            t2 = min([x for x in times if x > t])
            r1 = zero_curve[t1]
            r2 = zero_curve[t2]
            r = r1 + (r2 - r1) * (t - t1) / (t2 - t1)
        return math.exp(-r * t)

    # Bootstrap Loop
    for tenor in sorted(rates_input.keys()):
        swap_rate = rates_input[tenor]

        def objective(z_ibor_guess, tenor=tenor, swap_rate=swap_rate):
            temp_ibor = zeros_ibor.copy()
            temp_disc = zeros_disc.copy()
            temp_ibor[tenor] = z_ibor_guess
            temp_disc[tenor] = z_ibor_guess + spread_discount

            # PV Fixed (Annual)
            pv_fix = 0
            for t in range(1, tenor + 1):
                pv_fix += swap_rate * 1.0 * get_df(t, temp_disc)

            # PV Float (Semi)
            pv_float = 0
            pv_float += fixing_6m * 0.5 * get_df(0.5, temp_disc)

            for i in range(2, tenor * 2 + 1):
                t_curr = i * 0.5
                t_prev = (i - 1) * 0.5

                df_c_disc = get_df(t_curr, temp_disc)
                df_p_disc = get_df(t_prev, temp_disc)

                df_c_ibor = df_c_disc * math.exp(spread_discount * t_curr)
                df_p_ibor = df_p_disc * math.exp(spread_discount * t_prev)

                fwd = (df_p_ibor / df_c_ibor - 1) / 0.5
                pv_float += fwd * 0.5 * df_c_disc

            return pv_float - pv_fix

        # Solver (Bisection)
        low, high = 0.0, 0.10
        for _ in range(50):
            mid = (low + high) / 2
            if objective(mid) == 0: break
            if objective(low) * objective(mid) < 0: high = mid
            else: low = mid
        z_solved = (low + high) / 2

        zeros_ibor[tenor] = z_solved
        zeros_disc[tenor] = z_solved + spread_discount

    # Print Q1 Results
    print('=' * 70)
    print('QUESTION 1: Curve Construction (Gemini)')
    print('=' * 70)
    print(f"{'Tenor':<10} {'IBOR Zero %':<15} {'Disc Zero %':<15} {'IBOR DF':<15} {'Disc DF':<15}")
    print('-' * 70)
    for t in sorted(zeros_ibor.keys()):
        ibor_df = math.exp(-zeros_ibor[t] * t)
        disc_df = math.exp(-zeros_disc[t] * t)
        print(f'{t:<10.1f} {zeros_ibor[t]*100:<15.6f} {zeros_disc[t]*100:<15.6f} {ibor_df:<15.8f} {disc_df:<15.8f}')

    # --- Q2: 9Y Swap Rate, DV01, Gamma ---
    def price_swap_par_rate(maturity, curves):
        pv_float = 0
        pv_float += fixing_6m * 0.5 * get_df(0.5, curves['disc'])

        for i in range(2, int(maturity * 2) + 1):
            t_curr = i * 0.5
            t_prev = (i - 1) * 0.5
            df_c = get_df(t_curr, curves['disc'])
            df_p = get_df(t_prev, curves['disc'])
            df_c_i = df_c * math.exp(spread_discount * t_curr)
            df_p_i = df_p * math.exp(spread_discount * t_prev)
            fwd = (df_p_i / df_c_i - 1) / 0.5
            pv_float += fwd * 0.5 * df_c

        pv01 = 0
        for t in range(1, int(maturity) + 1):
            pv01 += 1.0 * get_df(t, curves['disc'])

        return pv_float / pv01

    rate_9y = price_swap_par_rate(9, {'ibor': zeros_ibor, 'disc': zeros_disc})

    def value_existing_swap(fixed_rate, maturity, curves):
        pv_fix = 0
        for t in range(1, int(maturity) + 1):
            pv_fix += fixed_rate * 1.0 * get_df(t, curves['disc'])

        pv_float = 0
        pv_float += fixing_6m * 0.5 * get_df(0.5, curves['disc'])
        for i in range(2, int(maturity * 2) + 1):
            t_curr = i * 0.5
            t_prev = (i - 1) * 0.5
            df_c = get_df(t_curr, curves['disc'])
            df_p = get_df(t_prev, curves['disc'])
            df_c_i = df_c * math.exp(spread_discount * t_curr)
            df_p_i = df_p * math.exp(spread_discount * t_prev)
            fwd = (df_p_i / df_c_i - 1) / 0.5
            pv_float += fwd * 0.5 * df_c

        # Receiver swap: receive fixed, pay float
        return pv_fix - pv_float

    val_base = value_existing_swap(rate_9y, 9, {'ibor': zeros_ibor, 'disc': zeros_disc})

    # Shocked Curves (+1bp to zero rates)
    zeros_ibor_up = {k: v + 0.0001 for k, v in zeros_ibor.items()}
    zeros_disc_up = {k: v + 0.0001 for k, v in zeros_disc.items()}
    val_up = value_existing_swap(rate_9y, 9, {'ibor': zeros_ibor_up, 'disc': zeros_disc_up})

    zeros_ibor_dn = {k: v - 0.0001 for k, v in zeros_ibor.items()}
    zeros_disc_dn = {k: v - 0.0001 for k, v in zeros_disc.items()}
    val_dn = value_existing_swap(rate_9y, 9, {'ibor': zeros_ibor_dn, 'disc': zeros_disc_dn})

    dv01_per_1m = (val_up - val_base) * 1_000_000
    gamma_val = (val_up - 2*val_base + val_dn) / (0.0001**2) * 1_000_000

    print()
    print('=' * 70)
    print('QUESTION 2: 9Y Par Swap Pricing (Gemini)')
    print('=' * 70)
    print(f'  Par Swap Rate: {rate_9y * 100:.6f}%')
    print(f'  DV01: {dv01_per_1m:.2f}')
    print(f'  Gamma: {gamma_val:.2f}')
    print(f'  Verification - Base PV: {val_base * 1_000_000:.2f}')

    # --- Q3: 3 Months Later ---
    def price_q3(fixed_coupon, curves):
        pv_fix = 0
        for t_orig in range(1, 10):
            t_new = t_orig - 0.25
            pv_fix += fixed_coupon * 1.0 * get_df(t_new, curves['disc'])

        pv_float = 0
        pv_float += fixing_6m * 0.5 * get_df(0.5 - 0.25, curves['disc'])

        for i in range(2, 19):
            t_orig = i * 0.5
            t_new = t_orig - 0.25
            t_start = t_new - 0.5
            t_end = t_new

            df_c = get_df(t_end, curves['disc'])
            df_p = get_df(t_start, curves['disc'])
            df_c_i = df_c * math.exp(spread_discount * t_end)
            df_p_i = df_p * math.exp(spread_discount * t_start)

            fwd = (df_p_i / df_c_i - 1) / 0.5
            pv_float += fwd * 0.5 * df_c

        # Receiver swap: receive fixed, pay float
        pv_dirty = (pv_fix - pv_float) * 1_000_000

        acc_fix = fixed_coupon * 1_000_000 * 0.25
        acc_float = fixing_6m * 1_000_000 * 0.25

        # Clean PV: remove accrued fixed (will receive), add accrued float (will pay)
        pv_clean = pv_dirty - acc_fix + acc_float

        return acc_fix, acc_float, pv_dirty, pv_clean

    acc_fix, acc_float, pv_dirty_q3, pv_clean_q3 = price_q3(rate_9y, {'ibor': zeros_ibor, 'disc': zeros_disc})

    print()
    print('=' * 70)
    print('QUESTION 3: Valuation 3 Months Later - Linear (Gemini)')
    print('=' * 70)
    print(f'  Fixed Leg Accrual: {acc_fix:.2f}')
    print(f'  Float Leg Accrual: {acc_float:.2f}')
    print(f'  Net Accrual (Fixed - Float): {acc_fix - acc_float:.2f}')
    print(f'  Dirty PV: {pv_dirty_q3:.2f}')
    print(f'  Clean PV: {pv_clean_q3:.2f}')

    # --- Q4: Cubic Spline ---
    knots = np.array([0.0, 0.5, 1.0, 2.0, 3.0, 5.0, 7.0, 10.0])
    y_vals = [fixing_6m]
    for t in knots[1:]:
        y_vals.append(zeros_ibor[t])
    y_vals = np.array(y_vals)

    n = len(knots) - 1
    h = np.diff(knots)

    mu = h[:-1] / (h[:-1] + h[1:])
    lam = h[1:] / (h[:-1] + h[1:])
    d = 6 * ((y_vals[2:] - y_vals[1:-1]) / h[1:] - (y_vals[1:-1] - y_vals[:-2]) / h[:-1]) / (h[:-1] + h[1:])

    dim = len(knots) - 2
    A = np.zeros((dim, dim))
    np.fill_diagonal(A, 2)
    if dim > 1:
        np.fill_diagonal(A[:-1, 1:], lam[:-1])
        np.fill_diagonal(A[1:, :-1], mu[1:])

    M_internal = np.linalg.solve(A, d)
    M = np.concatenate(([0], M_internal, [0]))

    def get_spline_rate(t):
        if t <= knots[0]: return y_vals[0]
        if t >= knots[-1]: return y_vals[-1]

        idx = np.searchsorted(knots, t) - 1
        idx = max(0, min(idx, n-1))

        xi = knots[idx]
        hi = h[idx]
        im1 = idx + 1

        a = y_vals[idx]
        b = (y_vals[im1] - y_vals[idx])/hi - hi*(2*M[idx] + M[im1])/6.0
        c = M[idx] / 2.0
        d_coef = (M[im1] - M[idx]) / (6.0*hi)

        dx = t - xi
        return a + b*dx + c*dx**2 + d_coef*dx**3

    def get_df_spline(t, spread=0):
        r_ibor = get_spline_rate(t)
        r_disc = r_ibor + spread
        return math.exp(-r_disc * t)

    # Q4 - 9Y rate with spline
    pv_float_s = 0
    pv_float_s += fixing_6m * 0.5 * get_df_spline(0.5, spread_discount)
    for i in range(2, 19):
        t_c = i * 0.5
        t_p = (i - 1) * 0.5
        df_c = get_df_spline(t_c, spread_discount)
        df_p = get_df_spline(t_p, spread_discount)
        df_c_i = df_c * math.exp(spread_discount * t_c)
        df_p_i = df_p * math.exp(spread_discount * t_p)
        fwd = (df_p_i / df_c_i - 1) / 0.5
        pv_float_s += fwd * 0.5 * df_c

    pv_fix_s = 0
    for t in range(1, 10):
        pv_fix_s += 1.0 * get_df_spline(t, spread_discount)

    rate_9y_spline = pv_float_s / pv_fix_s

    # Q4 - Clean PV with spline
    pv_fix_q3_s = 0
    for t_orig in range(1, 10):
        t_new = t_orig - 0.25
        pv_fix_q3_s += rate_9y_spline * 1.0 * get_df_spline(t_new, spread_discount)

    pv_float_q3_s = 0
    pv_float_q3_s += fixing_6m * 0.5 * get_df_spline(0.5-0.25, spread_discount)
    for i in range(2, 19):
        t_orig = i * 0.5
        t_new = t_orig - 0.25
        t_end = t_new
        t_start = t_new - 0.5

        df_c = get_df_spline(t_end, spread_discount)
        df_p = get_df_spline(t_start, spread_discount)
        df_c_i = df_c * math.exp(spread_discount * t_end)
        df_p_i = df_p * math.exp(spread_discount * t_start)
        fwd = (df_p_i / df_c_i - 1) / 0.5
        pv_float_q3_s += fwd * 0.5 * df_c

    # Receiver swap: receive fixed, pay float
    pv_dirty_s = (pv_fix_q3_s - pv_float_q3_s) * 1_000_000

    acc_fix_s = rate_9y_spline * 1_000_000 * 0.25
    acc_float_s = fixing_6m * 1_000_000 * 0.25

    # Clean PV: remove accrued fixed (will receive), add accrued float (will pay)
    pv_clean_q3_s = pv_dirty_s - acc_fix_s + acc_float_s

    print()
    print('=' * 70)
    print('QUESTION 4: Cubic Spline Interpolation (Gemini)')
    print('=' * 70)
    print(f'  Fixed Leg Accrual: {acc_fix_s:.2f}')
    print(f'  Float Leg Accrual: {acc_float_s:.2f}')
    print(f'  Net Accrual (Fixed - Float): {acc_fix_s - acc_float_s:.2f}')
    print(f'  Dirty PV: {pv_dirty_s:.2f}')
    print(f'  Clean PV: {pv_clean_q3_s:.2f}')

    print()
    print('=' * 70)
    print('Comparison: Linear vs Cubic Spline (Gemini)')
    print('=' * 70)
    print(f'  Clean PV (Linear):       {pv_clean_q3:.2f}')
    print(f'  Clean PV (Cubic Spline): {pv_clean_q3_s:.2f}')
    print(f'  Difference:              {pv_clean_q3_s - pv_clean_q3:.2f}')

if __name__ == "__main__":
    solve_questions()
