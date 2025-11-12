# ADR-005: Adoption of Response-only Model for Interview Q&A Assistant

**Status:** Approved  
**Date:** 2025-11-11  
**Owner:** Stephen Edwards  
**Version:** 1.1  

---

## Context

The Interview Answer Assistant requires **near real-time responses (<1 second)** during live or recorded interviews.  
Each interaction consists of a user question (or detected speech segment) followed by a short AI-generated answer and optional follow-up.  
While answers must be contextually aware (recent Q&A memory, preamble instructions), the system must prioritize **low latency**, **scalability**, and **statelessness**.

Two OpenAI model families were evaluated:

| Model Type | Example | Key Features | Typical Use |
|-------------|----------|---------------|--------------|
| **Chat models** | `gpt-5`, `gpt-4o` | Multi-turn context, structured messages (`system`, `user`, `assistant`) | Conversational assistants, long-form dialogue |
| **Response-only models** | `gpt-5-mini`, `o1-mini` | Stateless, fast completions, single text `input` | Low-latency inference, short Q&A, batch workloads |

Chat models provide natural role handling and conversational continuity, but they introduce extra latency due to message serialization and contextual processing.  
Response-only models are faster but require manual prompt assembly.

Given the real-time nature of the use case, **latency and responsiveness outweigh the need for automatic multi-turn context.**

---

## Decision

Use a **Response-only OpenAI model** (`gpt-5-mini`) for live interview question–answering and coaching scenarios.

The model will be called via the `/responses` API, using a **manually constructed prompt string** containing:
- a static **preamble** describing desired tone and response style, and  
- a short **rolling memory buffer** containing recent Q&A turns, the most recent preamble or contextual snippet, and (optionally) the last one or two final exchanges.

The prompt is rebuilt for each request:
```
"You are an AI interviewer coach. Respond concisely.\n\nContext: [latest preamble or retrieved snippet]\n\nRecent conversation:\nUser: [last question]\nAssistant: [last answer]\nUser: [new question]\nAssistant:"
```

This design ensures sub-second response times and supports scalable, stateless processing across concurrent sessions.

---

## Justification

**Advantages**
- ✅ **Low latency** — response times typically <500 ms using streaming.
- ✅ **Stateless architecture** — no session memory needed; easy to parallelize and scale.
- ✅ **Simpler server-side design** — single-string input per inference call.
- ✅ **Cost-efficient** — lower per-token pricing compared to full chat models.

**Trade-offs**
- ⚠️ Manual management of conversation context (rolling memory).
- ⚠️ No structured message roles (system/user separation flattened).
- ⚠️ Slightly higher development effort to maintain context consistency.

**Mitigation**
- Implement a lightweight prompt assembler to inject preamble + recent Q&A + optional context.
- Limit retained history to the last 2–3 interactions for relevance and token efficiency.

---

## Alternatives Considered

| Option | Description | Reason Rejected |
|---------|--------------|----------------|
| **Chat model (`gpt-5`)** | Supports multi-turn memory natively. | Increased latency and cost unsuitable for real-time interaction. |
| **Local small model (Whisper/LLM hybrid)** | On-device inference using quantized models. | Inferior quality, limited context capacity, and complex deployment. |

---

## Consequences

- The app must explicitly manage prompt composition, including contextual snippets or preambles.
- Model upgrades (e.g., to future `gpt-5-mini-fast`) will be seamless.
- Switching to a chat model remains possible if conversational persistence becomes more important than speed.

---

## Related Artifacts

- **PAD:** Interview Assistant Architecture v1.2  
- **ADR-004:** Model Selection Criteria for Realtime AI Services  
- **ADR-006 (Planned):** Streaming Response Handling and Error Resilience  

---

**Approved by:**  
- Stephen Edwards – Product Architect  
- Jim Taliadoros – Lead Developer  
- Manuel Hassler – AI Systems Advisor  

---
