import os
import subprocess
import json
from pathlib import Path
from datetime import datetime

JAVA_PATH = r"C:\Program Files\Eclipse Adoptium\jre-17.0.18.8-hotspot\bin\java.exe"
FFDEC_JAR = r"C:\Program Files (x86)\FFDec\ffdec.jar"
SWF_DIR = Path(r"d:\codes\mewgenics_assets\swfs")
OUTPUT_BASE = Path(r"d:\codes\mewgenics_assets\swf_extracted")

def extract_swf(swf_path, output_dir):
    """Run FFDec to extract images and shapes from a single SWF."""
    output_dir.mkdir(parents=True, exist_ok=True)
    
    cmd = [
        JAVA_PATH,
        "-jar", FFDEC_JAR,
        "-export", "image,shape",
        str(output_dir),
        str(swf_path)
    ]
    
    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=300  # 5 minutes per SWF
        )
        
        # Parse stdout for counts
        stdout = result.stdout
        stderr = result.stderr
        
        # Count images exported
        img_count = stdout.count("Exported image")
        shape_count = stdout.count("Exported shape")
        
        # Also check actual files on disk
        img_dir = output_dir / "images"
        shape_dir = output_dir / "shapes"
        actual_images = len(list(img_dir.rglob("*"))) if img_dir.exists() else 0
        actual_shapes = len(list(shape_dir.rglob("*"))) if shape_dir.exists() else 0
        
        return {
            "success": True,
            "images": actual_images,
            "shapes": actual_shapes,
            "stdout_preview": stdout[:500],
            "stderr_preview": stderr[:500] if stderr else "",
            "returncode": result.returncode
        }
    except subprocess.TimeoutExpired:
        return {
            "success": False,
            "images": 0,
            "shapes": 0,
            "error": "Timeout (5 minutes exceeded)"
        }
    except Exception as e:
        return {
            "success": False,
            "images": 0,
            "shapes": 0,
            "error": str(e)
        }

def main():
    swf_files = sorted(SWF_DIR.rglob("*.swf"))
    total = len(swf_files)
    print(f"[{datetime.now().strftime('%H:%M:%S')}] Found {total} SWF files to process")
    
    results = []
    total_images = 0
    total_shapes = 0
    
    for idx, swf_path in enumerate(swf_files, 1):
        swf_name = swf_path.stem
        # Use relative path from SWF_DIR to avoid name collisions
        rel_parent = swf_path.parent.relative_to(SWF_DIR)
        if str(rel_parent) == '.':
            output_dir = OUTPUT_BASE / swf_name
        else:
            output_dir = OUTPUT_BASE / rel_parent / swf_name
        
        # Skip if already extracted
        if output_dir.exists() and any(output_dir.iterdir()):
            print(f"\n[{datetime.now().strftime('%H:%M:%S')}] [{idx}/{total}] Skipping (already exists): {swf_name}.swf")
            # Count existing files for report
            img_dir = output_dir / "images"
            shape_dir = output_dir / "shapes"
            actual_images = len(list(img_dir.rglob("*"))) if img_dir.exists() else 0
            actual_shapes = len(list(shape_dir.rglob("*"))) if shape_dir.exists() else 0
            result = {
                "success": True,
                "images": actual_images,
                "shapes": actual_shapes,
                "skipped": True
            }
        else:
            print(f"\n[{datetime.now().strftime('%H:%M:%S')}] [{idx}/{total}] Processing: {swf_name}.swf")
            result = extract_swf(swf_path, output_dir)
        result["swf_name"] = swf_name
        result["swf_size_mb"] = round(swf_path.stat().st_size / 1024 / 1024, 2)
        results.append(result)
        
        if result["success"]:
            total_images += result["images"]
            total_shapes += result["shapes"]
            print(f"  -> Images: {result['images']}, Shapes: {result['shapes']}, Exit: {result.get('returncode', 'N/A')}")
        else:
            print(f"  -> FAILED: {result.get('error', 'Unknown error')}")
    
    # Generate report
    report_lines = []
    report_lines.append("=" * 70)
    report_lines.append("SWF 素材提取报告")
    report_lines.append("=" * 70)
    report_lines.append(f"\n生成时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    report_lines.append(f"SWF 源目录: {SWF_DIR}")
    report_lines.append(f"输出目录: {OUTPUT_BASE}")
    report_lines.append(f"\n总体统计:")
    report_lines.append(f"  SWF 文件总数: {total}")
    report_lines.append(f"  成功提取: {sum(1 for r in results if r['success'])}")
    report_lines.append(f"  失败: {sum(1 for r in results if not r['success'])}")
    report_lines.append(f"  图片总数: {total_images}")
    report_lines.append(f"  矢量形状总数: {total_shapes}")
    
    report_lines.append(f"\n各 SWF 详细结果:")
    report_lines.append("-" * 70)
    report_lines.append(f"{'SWF 文件名':<30} {'大小(MB)':>10} {'图片':>8} {'矢量':>8} {'状态':>8}")
    report_lines.append("-" * 70)
    
    for r in results:
        status = "OK" if r["success"] else "FAIL"
        report_lines.append(
            f"{r['swf_name']:<30} {r['swf_size_mb']:>10.2f} {r['images']:>8} {r['shapes']:>8} {status:>8}"
        )
    
    report_lines.append("\n" + "=" * 70)
    
    report_text = "\n".join(report_lines)
    report_path = OUTPUT_BASE / "_extraction_report.txt"
    with open(report_path, "w", encoding="utf-8") as f:
        f.write(report_text)
    
    # JSON report
    json_path = OUTPUT_BASE / "_extraction_report.json"
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump({
            "timestamp": datetime.now().isoformat(),
            "total_swfs": total,
            "total_images": total_images,
            "total_shapes": total_shapes,
            "results": results
        }, f, indent=2, ensure_ascii=False)
    
    print(f"\n[{datetime.now().strftime('%H:%M:%S')}] All done!")
    print(f"Report saved to: {report_path}")
    print(f"JSON report: {json_path}")

if __name__ == "__main__":
    main()
