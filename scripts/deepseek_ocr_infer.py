import argparse
import os
import sys
import time
from PIL import Image

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--image", required=True)
    parser.add_argument("--mode", default="small", choices=["tiny","small","base","large","gundam"])
    parser.add_argument("--size", type=int, default=None)
    parser.add_argument("--max-chars", type=int, default=4000)
    args = parser.parse_args()

    try:
        import torch
        from transformers import AutoModel, AutoTokenizer, AutoModelForCausalLM
        import tempfile, os, glob, json
    except Exception as e:
        print("", end="")
        return

    attn_impl = "eager"
    try:
        import flash_attn  # noqa
        attn_impl = "flash_attention_2"
    except Exception:
        pass

    model_name = "deepseek-ai/DeepSeek-OCR"
    # 计时：模型加载（含分词器、权重下载/加载）
    t_load_start = time.perf_counter()
    try:
        tokenizer = AutoTokenizer.from_pretrained(model_name, trust_remote_code=True)
        model = AutoModel.from_pretrained(
            model_name,
            trust_remote_code=True,
            use_safetensors=True,
            _attn_implementation=attn_impl
        )
        # 兜底：若模型没有 infer 方法，则尝试使用 CausalLM 版本
        if not hasattr(model, "infer"):
            model = AutoModelForCausalLM.from_pretrained(
                model_name,
                trust_remote_code=True,
                use_safetensors=True,
                _attn_implementation=attn_impl
            )
    except Exception:
        print("", end="")
        return

    device = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.bfloat16 if device == "cuda" else torch.float32
    try:
        model = model.eval().to(dtype)
        if device == "cuda":
            model = model.cuda()
    except Exception:
        print("", end="")
        return
    # 打印模型加载耗时到标准错误，不干扰识别文本
    t_load_end = time.perf_counter()
    load_ms = (t_load_end - t_load_start) * 1000.0
    try:
        sys.stderr.write(f"[time] load={load_ms:.0f}ms device={device} attn={attn_impl}\n")
        sys.stderr.flush()
    except Exception:
        pass

    base_size, image_size, crop_mode = 640, 640, False
    if args.mode == "tiny":
        base_size, image_size, crop_mode = 512, 512, False
    elif args.mode == "base":
        base_size, image_size, crop_mode = 1024, 1024, False
    elif args.mode == "large":
        base_size, image_size, crop_mode = 1280, 1280, False
    elif args.mode == "gundam":
        base_size, image_size, crop_mode = 1024, 640, True

    # 若显式指定了尺寸，则覆盖模式默认尺寸
    if isinstance(args.size, int) and args.size > 0:
        base_size = args.size
        image_size = args.size

    try:
        img = Image.open(args.image).convert("RGB")
    except Exception:
        print("", end="")
        return

    prompt = "<image>\nFree OCR."
    # 创建临时输出目录，强制保存结果文件以便兜底读取
    tmp_dir = tempfile.mkdtemp(prefix="deepseek_ocr_")
    debug = os.environ.get("DEEPSEEK_OCR_DEBUG") == "1"

    try:
        # 规范化为绝对路径，避免下游代码对相对路径解析失败
        image_path = os.path.abspath(args.image)
        # 优先尝试多种参数名，兼容不同 infer 实现
        res = None
        infer_kwargs = dict(
            prompt=prompt,
            output_path=tmp_dir,
            base_size=base_size,
            image_size=image_size,
            crop_mode=crop_mode,
            save_results=True,
            test_compress=False
        )
        for key, val in [("images", [image_path]), ("image", image_path), ("image_file", image_path)]:
            try:
                t_infer_start = time.perf_counter()
                call_kwargs = {"tokenizer": tokenizer, **infer_kwargs, key: val}
                res = model.infer(**call_kwargs)
                t_infer_end = time.perf_counter()
                infer_ms = (t_infer_end - t_infer_start) * 1000.0
                try:
                    sys.stderr.write(f"[time] infer={infer_ms:.0f}ms mode={args.mode} image='{image_path}'\n")
                    sys.stderr.flush()
                except Exception:
                    pass
                break
            except TypeError as te:
                if debug:
                    sys.stderr.write(f"[infer-arg-mismatch] tried {key}: {te}\n")
                    sys.stderr.flush()
                continue
    except Exception as e:
        if debug:
            sys.stderr.write(f"[infer-exception] {e}\n")
            sys.stderr.flush()
        res = None  # 保留后续归一化处理

    try:
        def normalize(x):
            if x is None:
                return ""
            if isinstance(x, str):
                return x
            if isinstance(x, list):
                items = [normalize(i) for i in x]
                return next((i for i in items if i), " ".join([i for i in items if i]))
            if isinstance(x, dict):
                for k in ("output_text", "text", "output", "result", "prediction", "pred", "data"):
                    v = x.get(k)
                    s = normalize(v)
                    if s:
                        return s
                return str(x)
            return str(x)

        text = normalize(res)

        # 若模型返回为空，尝试从保存的结果文件兜底读取
        if not text:
            candidates = []
            candidates += glob.glob(os.path.join(tmp_dir, "*.txt"))
            candidates += glob.glob(os.path.join(tmp_dir, "*.json"))
            preferred = [
                os.path.join(tmp_dir, "infer_output.txt"),
                os.path.join(tmp_dir, "result.txt"),
                os.path.join(tmp_dir, "output.txt"),
                os.path.join(tmp_dir, "infer_output.json"),
                os.path.join(tmp_dir, "result.json"),
                os.path.join(tmp_dir, "output.json")
            ]
            for p in preferred + candidates:
                if os.path.isfile(p):
                    try:
                        if p.endswith(".txt"):
                            with open(p, "r", encoding="utf-8") as f:
                                t = f.read().strip()
                                if t:
                                    text = t
                                    break
                        else:
                            with open(p, "r", encoding="utf-8") as f:
                                data = json.load(f)
                            t = normalize(data)
                            if t:
                                text = t
                                break
                    except Exception as fe:
                        if debug:
                            sys.stderr.write(f"[file-read-exception] {p}: {fe}\n")
                            sys.stderr.flush()
                        continue

        if debug and not text:
            # 打印返回对象类型与片段，便于定位
            sys.stderr.write(f"[res-type] {type(res).__name__}\n")
            sys.stderr.write(f"[tmp-dir] {tmp_dir}\n")
            sys.stderr.flush()

        if len(text) > args.max_chars:
            text = text[:args.max_chars]
        # 添加结尾换行，便于 C# 端 OutputDataReceived 按行捕获
        try:
            sys.stdout.write((text or "") + "\n")
            sys.stdout.flush()
        except Exception:
            pass
    except Exception as e:
        if debug:
            sys.stderr.write(f"[normalize-exception] {e}\n")
            sys.stderr.flush()
        print("", end="")
        return

if __name__ == "__main__":
    main()