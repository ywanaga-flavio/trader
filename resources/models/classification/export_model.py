#!/usr/bin/env python3
"""Download mDeBERTa-v3-base-mnli-xnli (pre-built quantized ONNX) from HuggingFace.

The HuggingFace repo ships pre-quantized ONNX files; no local PyTorch export needed.

HF repo files used:
    onnx/model_quantized.onnx  →  model_optimized_int8.onnx  (~180 MB)
    spm.model                  →  sentencepiece.bpe.model     (~900 KB)

Hardware target : Intel i7-4790 (Haswell, AVX2), GTX 1660 SUPER, 16 GB RAM
ONNX Runtime   : Microsoft.ML.OnnxRuntime (CPU provider by default)

Usage:
    python export_model.py
"""

import sys
import shutil
from pathlib import Path
from huggingface_hub import hf_hub_download

MODEL_ID = "MoritzLaurer/mDeBERTa-v3-base-mnli-xnli"
OUTPUT_DIR = Path(__file__).parent  # resources/models/classification/

DOWNLOADS = [
    # (hf_filename,                 local_filename)
    ("onnx/model_quantized.onnx",  "model_optimized_int8.onnx"),
    ("spm.model",                  "sentencepiece.bpe.model"),
]


def _size_mb(path: Path) -> str:
    return f"{path.stat().st_size / 1_048_576:.1f} MB"


def main() -> None:
    print("=" * 60)
    print("  mDeBERTa-v3-base-mnli-xnli — download pre-built ONNX INT8")
    print(f"  Output dir: {OUTPUT_DIR}")
    print("=" * 60)

    already_done = all((OUTPUT_DIR / local).exists() for _, local in DOWNLOADS)
    if already_done:
        print("\n⚠  All files already present:")
        for _, local in DOWNLOADS:
            p = OUTPUT_DIR / local
            print(f"   {local}  ({_size_mb(p)})")
        print("\nDelete them and re-run to force a fresh download.")
        sys.exit(0)

    for i, (hf_file, local_name) in enumerate(DOWNLOADS, 1):
        dest = OUTPUT_DIR / local_name
        if dest.exists():
            print(f"\n[{i}/{len(DOWNLOADS)}] {local_name} already exists — skipping.")
            continue

        print(f"\n[{i}/{len(DOWNLOADS)}] Downloading {hf_file} ...", flush=True)
        cached = hf_hub_download(
            repo_id=MODEL_ID,
            filename=hf_file,
        )
        shutil.copy2(cached, dest)
        print(f"     ✓  {local_name}  ({_size_mb(dest)})", flush=True)

    print("\n" + "=" * 60)
    print("  ✅  Download complete!")
    for _, local in DOWNLOADS:
        p = OUTPUT_DIR / local
        print(f"      {p.name}  ({_size_mb(p)})")
    print("=" * 60)


if __name__ == "__main__":
    main()

