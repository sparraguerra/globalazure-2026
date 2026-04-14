"""Uvicorn launcher for Aspire local orchestration.

Aspire injects PORT via WithHttpEndpoint(env: 'PORT').
Falls back to 8001 for standalone execution.
"""
import os
import uvicorn

if __name__ == "__main__":
    port = int(os.getenv("PORT", "8001"))
    uvicorn.run("main:app", host="0.0.0.0", port=port)
