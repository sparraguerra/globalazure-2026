"""Uvicorn launcher for Aspire local orchestration.

Aspire injects PORT via WithHttpEndpoint(env: 'PORT').
Falls back to 8004 for standalone execution.
Note: full XTTS-v2 inference requires GPU. In local dev without GPU,
set CONTENT_FACTORY_MODE=lab on agent-podcaster to use Azure OpenAI TTS.
"""
import os
import uvicorn

if __name__ == "__main__":
    port = int(os.getenv("PORT", "8004"))
    uvicorn.run("main:app", host="0.0.0.0", port=port, workers=1)
