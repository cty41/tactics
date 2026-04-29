import os
import json
from pathlib import Path
from collections import defaultdict

ASSETS_DIR = r"d:\codes\mewgenics_assets"
OUTPUT_DIR = Path(ASSETS_DIR)

def analyze():
    files = list(OUTPUT_DIR.rglob("*"))
    files = [f for f in files if f.is_file() and f.name != "_extraction_errors.log"]

    total_files = len(files)
    total_bytes = sum(f.stat().st_size for f in files)

    # Stats by extension
    ext_stats = defaultdict(lambda: {"count": 0, "bytes": 0})
    for f in files:
        ext = f.suffix.lower()
        if not ext:
            ext = "(no extension)"
        size = f.stat().st_size
        ext_stats[ext]["count"] += 1
        ext_stats[ext]["bytes"] += size

    # Sort by total bytes desc
    sorted_exts = sorted(ext_stats.items(), key=lambda x: x[1]["bytes"], reverse=True)

    # Top 20 largest files
    sorted_files = sorted(files, key=lambda f: f.stat().st_size, reverse=True)
    top20 = sorted_files[:20]

    # Build report text
    lines = []
    lines.append("=" * 70)
    lines.append("Mewgenics 资源统计报告")
    lines.append("=" * 70)
    lines.append(f"\n生成时间: {__import__('datetime').datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append(f"资源目录: {ASSETS_DIR}")
    lines.append(f"\n总体统计:")
    lines.append(f"  文件总数: {total_files:,}")
    lines.append(f"  总大小:   {total_bytes:,} bytes ({total_bytes / 1024 / 1024 / 1024:.2f} GB)")

    lines.append(f"\n按扩展名分布 (按大小降序):")
    lines.append("-" * 70)
    lines.append(f"{'扩展名':<20} {'文件数':>10} {'总大小 (MB)':>15} {'占比':>10}")
    lines.append("-" * 70)
    for ext, stats in sorted_exts:
        pct = stats['bytes'] / total_bytes * 100 if total_bytes else 0
        lines.append(f"{ext:<20} {stats['count']:>10,} {stats['bytes'] / 1024 / 1024:>15.2f} {pct:>9.2f}%")

    lines.append(f"\nTop 20 最大文件:")
    lines.append("-" * 70)
    lines.append(f"{'排名':<6} {'大小 (MB)':>12} {'路径'}")
    lines.append("-" * 70)
    for i, f in enumerate(top20, 1):
        size_mb = f.stat().st_size / 1024 / 1024
        rel = f.relative_to(OUTPUT_DIR)
        lines.append(f"{i:<6} {size_mb:>12.2f} {rel}")

    lines.append("\n" + "=" * 70)

    report_text = "\n".join(lines)
    report_txt_path = OUTPUT_DIR / "_report.txt"
    with open(report_txt_path, "w", encoding="utf-8") as f:
        f.write(report_text)

    # JSON report
    json_data = {
        "total_files": total_files,
        "total_bytes": total_bytes,
        "extensions": [
            {
                "extension": ext,
                "count": stats["count"],
                "bytes": stats["bytes"]
            }
            for ext, stats in sorted_exts
        ],
        "top_20_files": [
            {
                "path": str(f.relative_to(OUTPUT_DIR)).replace("\\", "/"),
                "size": f.stat().st_size
            }
            for f in top20
        ]
    }
    report_json_path = OUTPUT_DIR / "_report.json"
    with open(report_json_path, "w", encoding="utf-8") as f:
        json.dump(json_data, f, indent=2, ensure_ascii=False)

    print(report_text)
    print(f"\n报告已保存:")
    print(f"  TXT: {report_txt_path}")
    print(f"  JSON: {report_json_path}")

if __name__ == "__main__":
    analyze()
