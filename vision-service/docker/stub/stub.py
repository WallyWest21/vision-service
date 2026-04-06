"""Minimal OpenAI-compatible stub for Qwen-VL — CPU/no-GPU development only."""
import time

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

app = FastAPI(title="Qwen-VL CPU Stub")

_STUB_CONTENT = "[CPU stub] Image analysis not available without GPU."


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/v1/chat/completions")
async def chat(request: Request):
    return JSONResponse(
        {
            "id": "stub-0",
            "object": "chat.completion",
            "created": int(time.time()),
            "model": "stub",
            "choices": [
                {
                    "index": 0,
                    "message": {"role": "assistant", "content": _STUB_CONTENT},
                    "finish_reason": "stop",
                }
            ],
            "usage": {"prompt_tokens": 0, "completion_tokens": 0, "total_tokens": 0},
        }
    )


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8000)
