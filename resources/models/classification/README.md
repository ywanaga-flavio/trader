# News Analysis Model — mDeBERTa-v3-base-mnli-xnli (ONNX INT8)

Multilingual zero-shot NLI model for **topic classification** and **sentiment analysis** of news articles.  
Supports Spanish + English. Runs entirely on-device via ONNX Runtime (no external API).

## Hardware requirements

| Resource | Minimum |
|----------|---------|
| RAM      | 4 GB    |
| Storage  | ~400 MB |
| GPU      | Optional — ONNX Runtime uses CPU by default; GTX 1660 SUPER is supported via CUDA provider |

## Download

> Binary model files are excluded from version control (see `.gitignore`).  
> Run the commands below **once** to populate this directory.

### Option A — PowerShell (Windows)

```powershell
$dir = "$PSScriptRoot"

# Model (INT8 quantized, ~180 MB)
Invoke-WebRequest `
  -Uri "https://huggingface.co/MoritzLaurer/mDeBERTa-v3-base-mnli-xnli/resolve/main/onnx/model_optimized_int8.onnx" `
  -OutFile "$dir\model_optimized_int8.onnx"

# SentencePiece tokenizer (~900 KB)
Invoke-WebRequest `
  -Uri "https://huggingface.co/MoritzLaurer/mDeBERTa-v3-base-mnli-xnli/resolve/main/sentencepiece.bpe.model" `
  -OutFile "$dir\sentencepiece.bpe.model"

# Tokenizer config (optional — for reference)
Invoke-WebRequest `
  -Uri "https://huggingface.co/MoritzLaurer/mDeBERTa-v3-base-mnli-xnli/resolve/main/tokenizer_config.json" `
  -OutFile "$dir\tokenizer_config.json"
```

### Option B — Python / huggingface_hub

```bash
pip install huggingface_hub
python -c "
from huggingface_hub import hf_hub_download
import shutil, os

repo = 'MoritzLaurer/mDeBERTa-v3-base-mnli-xnli'
dest = os.path.dirname(os.path.abspath('resources/models/classification/'))

for filename in ['onnx/model_optimized_int8.onnx', 'sentencepiece.bpe.model']:
    path = hf_hub_download(repo_id=repo, filename=filename)
    shutil.copy(path, dest)
print('Done.')
"
```

## Directory layout after download

```
resources/models/classification/
  .gitignore                   ← versioned — excludes binary files
  README.md                    ← versioned — this file
  model-config.json            ← versioned — model metadata
  model_optimized_int8.onnx    ← NOT versioned — download above (~180 MB)
  sentencepiece.bpe.model      ← NOT versioned — download above (~900 KB)
```

## How it works

The `OnnxNliAnalysisService` uses **zero-shot Natural Language Inference (NLI)**:

1. For each candidate label, it builds the input pair: `[CLS] article_text [SEP] hypothesis [SEP]`
2. Runs a forward pass through the model and reads the **entailment** logit score
3. The label with the highest entailment score wins

Both **topic classification** (6 categories from `NewsClassification`) and **sentiment** (3 values from `NewsValuation`) are resolved this way using Spanish-language hypotheses optimised for Argentine financial news.

## Model path configuration

By default the worker resolves the path relative to the project root.  
Override via `appsettings.json` / environment variable:

```json
"NewsAnalysis": {
  "ModelPath": "C:/absolute/path/to/resources/models/classification"
}
```
