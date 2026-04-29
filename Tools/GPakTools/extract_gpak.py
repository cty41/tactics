import os
import struct
import sys
from pathlib import Path
from datetime import datetime

GPAK_PATH = r"D:\SteamLibrary\steamapps\common\Mewgenics\resources.gpak"
OUTPUT_DIR = r"d:\codes\mewgenics_assets"

def extract_gpak(gpak_path, output_dir):
    gpak_path = Path(gpak_path)
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    total_size = gpak_path.stat().st_size
    file_count = 0
    extracted_count = 0
    extracted_bytes = 0
    errors = []

    with open(gpak_path, "rb") as f:
        count = struct.unpack("<i", f.read(4))[0]
        file_count = count
        print(f"[{datetime.now().strftime('%H:%M:%S')}] GPAK file count: {count}")

        entries = []
        for i in range(count):
            text_len = struct.unpack("<h", f.read(2))[0]
            path_bytes = f.read(text_len)
            path_str = path_bytes.decode("utf-8", errors="replace")
            length = struct.unpack("<i", f.read(4))[0]
            entries.append((path_str, length))

        data_start = f.tell()
        print(f"[{datetime.now().strftime('%H:%M:%S')}] Header read complete. Data starts at offset {data_start}")

        for idx, (rel_path, length) in enumerate(entries):
            try:
                out_path = output_dir / rel_path.replace("/", os.sep)
                out_path.parent.mkdir(parents=True, exist_ok=True)

                # Read and write in chunks to handle large files gracefully
                remaining = length
                chunk_size = 1024 * 1024  # 1MB

                with open(out_path, "wb") as out_f:
                    while remaining > 0:
                        to_read = min(chunk_size, remaining)
                        chunk = f.read(to_read)
                        if not chunk:
                            raise IOError(f"Unexpected EOF reading {rel_path}")
                        out_f.write(chunk)
                        remaining -= len(chunk)

                extracted_count += 1
                extracted_bytes += length

                if extracted_count % 500 == 0 or extracted_count == count:
                    pct = extracted_count / count * 100
                    print(f"[{datetime.now().strftime('%H:%M:%S')}] Progress: {extracted_count}/{count} ({pct:.1f}%) - {rel_path}")

            except Exception as e:
                errors.append((rel_path, str(e)))
                print(f"ERROR extracting {rel_path}: {e}")
                # Try to skip forward to next file
                try:
                    current = f.tell()
                    target = current + length
                    if target > current:
                        f.seek(target)
                except Exception:
                    pass

    print(f"\n[{datetime.now().strftime('%H:%M:%S')}] Extraction complete!")
    print(f"Total files in archive: {file_count}")
    print(f"Successfully extracted: {extracted_count}")
    print(f"Errors: {len(errors)}")
    print(f"Total bytes written: {extracted_bytes:,} ({extracted_bytes/1024/1024:.2f} MB)")

    if errors:
        error_log = output_dir / "_extraction_errors.log"
        with open(error_log, "w", encoding="utf-8") as ef:
            for p, e in errors:
                ef.write(f"{p}: {e}\n")
        print(f"Error log written to: {error_log}")

    return extracted_count, file_count, extracted_bytes, errors

if __name__ == "__main__":
    extract_gpak(GPAK_PATH, OUTPUT_DIR)
