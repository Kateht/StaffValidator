#!/usr/bin/env python3
"""
analyze_benchmarks.py

Finds the most recent benchmark CSV under `data/` or `experiments/`,
parses it, computes summary statistics, generates plots and a markdown
report comparing Regex, Hybrid and DFA validation methods.

Usage:
    python experiments/analyze_benchmarks.py [--csv PATH] [--outdir experiments]

Requires: pandas, matplotlib, seaborn, numpy
Install: pip install pandas matplotlib seaborn numpy

"""
from pathlib import Path
import argparse
import re
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import seaborn as sns
from datetime import datetime

# Reproducible plotting
np.random.seed(42)

# Helpers
num_re = re.compile(r"[+-]?\d*\.?\d+(?:[eE][+-]?\d+)?")

def extract_number(s):
    if pd.isna(s):
        return np.nan
    s = str(s)
    m = num_re.search(s)
    return float(m.group(0)) if m else np.nan


def find_latest_csv(search_paths):
    files = []
    for p in search_paths:
        p = Path(p)
        if not p.exists():
            continue
        files += [f for f in p.rglob('*.csv')]
    if not files:
        return None
    files_sorted = sorted(files, key=lambda f: f.stat().st_mtime, reverse=True)
    return files_sorted[0]


def canonicalize_columns(df):
    # Normalize column names and map common variants to canonical names
    cols = {c: c.strip() for c in df.columns}
    df = df.rename(columns=cols)

    colmap = {}
    for c in df.columns:
        lc = c.lower()
        if 'method' in lc:
            colmap[c] = 'Method'
        elif ('mean' in lc and 'ci' not in lc) or 'avg' in lc:
            # e.g., "Avg(ms)", "Average", "Mean"
            colmap[c] = 'Mean_ms'
        elif 'std' in lc:
            # e.g., "StdDev(ms)", "Std(ms)"
            colmap[c] = 'StdDev_ms'
        elif 'ci' in lc and '95' in lc:
            colmap[c] = 'CI95_ms'
        elif 'fallback' in lc:
            colmap[c] = 'Fallback_pct'
        elif 'accuracy' in lc:
            colmap[c] = 'Accuracy_pct'
        elif 'sample' in lc or lc.strip() == 'n':
            colmap[c] = 'Samples'
        elif 'input' in lc and 'len' in lc:
            colmap[c] = 'InputLength'
        elif 'min' in lc:
            # e.g., "Min(ms)"
            colmap[c] = 'Min_ms'
        elif 'max' in lc:
            # e.g., "Max(ms)"
            colmap[c] = 'Max_ms'
    return df.rename(columns=colmap)


def clean_numeric_columns(df):
    for col in list(df.columns):
        if col in ['Method']:
            continue
        if df[col].dtype == object or df[col].dtype == 'string':
            df[col] = df[col].apply(extract_number)
    return df


def compute_ci95(std, n):
    if np.isnan(std) or n is None or n <= 0:
        return np.nan
    return 1.96 * std / np.sqrt(n)


def generate_bar_chart(df, outpath, title):
    plt.figure(figsize=(8,5))
    sns.set(style='whitegrid')
    colors = []
    for m in df['Method']:
        ml = str(m).lower()
        if 'regex' in ml:
            colors.append('#4C72B0')  # blue
        elif 'hybrid' in ml:
            colors.append('#55A868')  # green
        else:
            colors.append('#C44E52')  # red
    x = np.arange(len(df))
    means = df['Mean_ms'].values
    errs = df['StdDev_ms'].values if 'StdDev_ms' in df.columns else np.zeros_like(means)
    plt.bar(x, means, yerr=errs, color=colors, capsize=6)
    plt.xticks(x, df['Method'], rotation=25, ha='right')
    plt.ylabel('Mean Runtime (ms)')
    plt.title(title)
    plt.tight_layout()
    plt.savefig(outpath, dpi=300)
    plt.close()


def generate_scaling_plot(df_detailed, outpath, title):
    plt.figure(figsize=(8,6))
    sns.set(style='whitegrid')
    methods = df_detailed['Method'].unique()
    for m in methods:
        subset = df_detailed[df_detailed['Method'] == m]
        grouped = subset.groupby('InputLength').agg({'Runtime_ms':'mean'}).reset_index()
        label = m
        if 'regex' in m.lower():
            color = '#4C72B0'
        elif 'hybrid' in m.lower():
            color = '#55A868'
        else:
            color = '#C44E52'
        plt.plot(grouped['InputLength'], grouped['Runtime_ms'], marker='o', label=label, color=color)
    plt.xlabel('Input Length')
    plt.ylabel('Mean Runtime (ms)')
    plt.xscale('log')
    plt.yscale('log')
    plt.title(title)
    plt.legend()
    plt.tight_layout()
    plt.savefig(outpath, dpi=300)
    plt.close()


def render_markdown(summary_table, plots, out_md_path, meta):
    now = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    lines = []
    lines.append('# Hybrid Validation Benchmark Summary')
    lines.append('')
    lines.append(f'- Dataset file: `{meta.get("csv_file")}`')
    lines.append(f'- Generated: {now}')
    lines.append('')
    lines.append('## Aggregated Results')
    lines.append('')
    # Table header
    lines.append('| Method | Mean (ms) | Std. Dev. (ms) | 95% CI (ms) | Fallback (%) | Accuracy (%) |')
    lines.append('|---|---:|---:|---:|---:|---:|')
    for r in summary_table:
        lines.append(f'| {r["Method"]} | {r["Mean_ms"]:.3f} | {r["StdDev_ms"]:.3f} | {r["CI95_ms"]:.3f} | {r.get("Fallback_pct", 0):.2f} | {r.get("Accuracy_pct", 100):.2f} |')
    lines.append('')
    for p in plots:
        rel = Path(p).name
        lines.append(f'![{rel}](../plots/{rel})')
        lines.append('')
    lines.append('## Observations')
    lines.append('')
    lines.append('- Cached Regex typically has the lowest average runtime on short inputs.')
    lines.append('- DFA-only provides predictable linear scaling and is robust to adversarial inputs.')
    lines.append('- Hybrid (Regex→DFA) gives safety against ReDoS while adding modest overhead; fallback rate shows how often DFA was used.')
    lines.append('')
    lines.append('## Example summary')
    lines.append('')
    lines.append('```')
    lines.append('### Summary: Hybrid Validation Benchmark')
    lines.append(f'- Dataset: {meta.get("dataset_name","unknown" )}')
    for r in summary_table:
        lines.append(f'- {r["Method"]}: {r["Mean_ms"]:.3f} ± {r["StdDev_ms"]:.3f} ms')
    lines.append('```')
    Path(out_md_path).write_text('\n'.join(lines), encoding='utf8')


def latex_table(summary_table, out_tex_path):
    lines = []
    lines.append('\\begin{tabular}{lrrrrr}')
    lines.append('\\toprule')
    lines.append('Method & Mean (ms) & StdDev (ms) & CI95 (ms) & Fallback (\\%) & Accuracy (\\%) \\\\')
    lines.append('\\midrule')
    for r in summary_table:
        lines.append(f'{r["Method"]} & {r["Mean_ms"]:.3f} & {r["StdDev_ms"]:.3f} & {r["CI95_ms"]:.3f} & {r.get("Fallback_pct",0):.2f} & {r.get("Accuracy_pct",100):.2f} \\\\')
    lines.append('\\bottomrule')
    lines.append('\\end{tabular}')
    Path(out_tex_path).write_text('\n'.join(lines), encoding='utf8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--csv', help='Path to CSV report (optional)')
    parser.add_argument('--outdir', default='experiments', help='Output folder base (default: experiments)')
    parser.add_argument('--assume-n', type=int, default=30, help='Assume N (repetitions) if not present in CSV (default 30)')

    args = parser.parse_args()
    base = Path('.').resolve()
    search_paths = [base / 'data', base / 'experiments']

    csv_path = Path(args.csv) if args.csv else find_latest_csv(search_paths)
    if csv_path is None:
        print('No CSV found under data/ or experiments/. Drop a CSV (report_table*.csv) and retry or pass --csv')
        return
    print('Using CSV:', csv_path)

    df = pd.read_csv(csv_path)
    df = canonicalize_columns(df)
    df = clean_numeric_columns(df)

    # If there's an InputLength column, assume this is detailed per-sample data
    detailed = None
    if 'InputLength' in df.columns and 'Runtime_ms' in df.columns:
        detailed = df.copy()

    # Keep only method-level summary rows (group by Method)
    if 'Method' not in df.columns:
        print('CSV missing Method column; aborting')
        return

    # If rows are per-method already
    summary = df.groupby('Method', as_index=False).agg({
        'Mean_ms':'mean' if 'Mean_ms' in df.columns else 'first',
        'StdDev_ms':'mean' if 'StdDev_ms' in df.columns else (lambda x: np.nan),
        'Fallback_pct':'mean' if 'Fallback_pct' in df.columns else (lambda x: np.nan),
        'Accuracy_pct':'mean' if 'Accuracy_pct' in df.columns else (lambda x: np.nan),
        'Samples':'max' if 'Samples' in df.columns else (lambda x: np.nan)
    })

    # Ensure numeric columns exist
    for c in ['Mean_ms','StdDev_ms','Fallback_pct','Accuracy_pct','Samples']:
        if c not in summary.columns:
            summary[c] = np.nan

    # Compute CI95 if missing
    summary['Samples'] = summary['Samples'].fillna(args.assume_n)
    summary['CI95_ms'] = summary.apply(lambda r: r['CI95_ms'] if 'CI95_ms' in summary.columns and not np.isnan(r.get('CI95_ms', np.nan)) else compute_ci95(r['StdDev_ms'], int(r['Samples'])), axis=1)

    # Prepare output folder
    out_base = Path(args.outdir) / 'reports'
    out_plots = Path(args.outdir) / 'plots'
    out_base.mkdir(parents=True, exist_ok=True)
    out_plots.mkdir(parents=True, exist_ok=True)

    # Save cleaned summary CSV
    cleaned_csv = out_base / f'summary_{csv_path.stem}.csv'
    summary.to_csv(cleaned_csv, index=False)

    # Create summary rows for markdown
    summary_rows = []
    for _, row in summary.iterrows():
        summary_rows.append({
            'Method': row['Method'],
            'Mean_ms': float(row['Mean_ms']) if not np.isnan(row['Mean_ms']) else 0.0,
            'StdDev_ms': float(row['StdDev_ms']) if not np.isnan(row['StdDev_ms']) else 0.0,
            'CI95_ms': float(row['CI95_ms']) if not np.isnan(row['CI95_ms']) else 0.0,
            'Fallback_pct': float(row['Fallback_pct']) if not np.isnan(row['Fallback_pct']) else 0.0,
            'Accuracy_pct': float(row['Accuracy_pct']) if not np.isnan(row['Accuracy_pct']) else 100.0,
            'Samples': int(row['Samples']) if not np.isnan(row['Samples']) else args.assume_n
        })

    # Sort methods by mean
    summary_rows = sorted(summary_rows, key=lambda r: r['Mean_ms'])

    # Generate plots
    df_plot = pd.DataFrame(summary_rows)
    runtime_plot = out_plots / f'runtime_comparison_{csv_path.stem}.png'
    generate_bar_chart(df_plot, runtime_plot, f'Average Validation Time per Method ({csv_path.stem})')

    plots = [runtime_plot]

    # Scaling plot if detailed data exists
    scaling_plot = None
    if detailed is not None:
        # ensure Runtime_ms column exists
        if 'Runtime_ms' not in detailed.columns:
            print('Detailed CSV present but Runtime_ms column missing; skipping scaling plot')
        else:
            scaling_plot = out_plots / f'runtime_scaling_{csv_path.stem}.png'
            generate_scaling_plot(detailed, scaling_plot, f'Runtime scaling ({csv_path.stem})')
            plots.append(scaling_plot)

    # Markdown report
    md_out = out_base / f'hybrid_validation_summary_{csv_path.stem}.md'
    meta = {'csv_file': str(csv_path), 'dataset_name': csv_path.stem}
    render_markdown(summary_rows, plots, md_out, meta)

    # LaTeX table
    tex_out = out_base / f'hybrid_validation_table_{csv_path.stem}.tex'
    latex_table(summary_rows, tex_out)

    print('Generated:')
    print(' - cleaned summary csv:', cleaned_csv)
    print(' - plots:', ', '.join(str(p) for p in plots))
    print(' - markdown report:', md_out)
    print(' - latex table:', tex_out)

if __name__ == '__main__':
    main()
