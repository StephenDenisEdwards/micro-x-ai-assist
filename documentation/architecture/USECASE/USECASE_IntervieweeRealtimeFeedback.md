# Use Case: Interviewee Realtime Feedback with Loopback Interviewer Capture

## Summary
In the normal workflow, interviewer audio is captured via loopback from the meeting app for transcription and question detection. Simultaneously, the microphone captures the interviewee’s audio and sends it to the AI for realtime analysis and feedback. Ideally, the AI also receives as text the question shown to the interviewee and the AI’s suggested answer, enabling richer context, logging, and UI rendering.

## Normal Workflow
- Loopback audio captures interviewer speech from the meeting application (e.g., Teams, Zoom).
- The system transcribes loopback audio and detects interviewer questions or imperative statements.
- Microphone audio captures the interviewee’s speech and streams it to the AI for realtime analysis (e.g., clarity, correctness, tone, timing).
- The AI produces guidance for the interviewee (e.g., suggested answer, key points, cautions).
- The application provides the AI with text representations of:
  - The detected interviewer question(s).
  - The AI’s suggested answer(s) presented to the interviewee.
- The UI surfaces guidance to the interviewee privately.

## Actors
- Interviewee: receives realtime guidance.
- Interviewer: speaks via a meeting app; captured via loopback.
- AI Model: consumes interviewee microphone audio, detected interviewer questions (text), and generates suggested answers (text/audio feedback).
- Application: captures loopback and microphone audio, performs transcription, orchestrates AI calls, and renders private guidance.

## Preconditions
- Loopback capture configured and active for the meeting application.
- Microphone capture configured and active for the interviewee.
- Model endpoints available with low-latency streaming support.
- Private UI panel available for guidance.

## Triggers
- Interviewer speaks a question on the meeting app.
- Interviewee speaks into the microphone.

## Main Flow
1. Application captures and transcribes loopback audio, extracting interviewer questions.
2. Application streams microphone audio from the interviewee to the AI.
3. Application sends to the AI:
   - Text of the detected interviewer question(s).
   - The AI’s previous suggested answer(s) (when available) for context continuity.
4. AI analyzes interviewee speech in realtime and generates feedback:
   - Suggested answer or improvements.
   - Delivery tips (pace, tone, keywords).
   - Corrections or clarifications.
5. Application displays private guidance to the interviewee:
   - Question (text) and suggested answer (text).
   - Optional realtime cues while speaking.
6. Interviewee adapts response; the system continues streaming and updating guidance.

## Alternate Flows
- No Question Detected: If loopback transcription doesn’t detect a clear question, system shows a "listening" state and general cues.
- Latency Spike: Fall back to non-streaming feedback or minimal prompts until conditions improve.
- Loopback Unavailable: If loopback is unavailable, pause question detection and continue microphone analysis only, with a clear indicator.

## Postconditions
- The interviewee receives realtime guidance informed by both their microphone audio and the detected interviewer question.
- The AI has access to text for the question and the suggested answer to maintain context.

## UX Notes
- Private-only guidance panel; avoid any cues audible to interviewer.
- Clear visualization of current question and suggested answer.
- Minimal visual noise; emphasize key points and next actions.

## Privacy & Transparency
- Do not reveal guidance to the interviewer.
- Do not persist sensitive content by default; provide an opt-in setting with clear consent.

## Non-Functional Requirements
- End-to-end guidance latency under 300 ms where possible.
- Robust streaming; graceful degradation under network constraints.
- Configurable limits for text size and audio stream duration.

## Acceptance Criteria
- Interviewer questions captured via loopback are transcribed and shown to the interviewee.
- Interviewee microphone audio is streamed to the AI; realtime guidance is produced.
- The AI receives text of both the detected question and the suggested answer provided to the interviewee.
- Guidance remains private and is not exposed to the interviewer.
