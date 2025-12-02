# Use Case: Provide Additional Text Input to the Model

## Summary
Enable the interviewee to discreetly request clarifications or pose questions to the AI without the interviewer hearing this. The local text input is sent alongside live audio transcription to refine responses, inject domain knowledge, or direct style, without disrupting the meeting flow.

## Application Note
Application captures microphone audio or loopback audio and routes transcription with the additional text input to the model.

## Normal Workflow
- Loopback audio is used to capture the interviewer’s speech from the meeting application (e.g., Teams, Zoom). 
- The application continuously transcribes loopback audio to detect questions or imperative statements from the interviewer. 
- The interviewee provides private text input to guide the AI’s responses to those detected questions.
- Microphone input is not used to capture the interviewer in the normal workflow.

## Goals
- Provide a private channel for the interviewee to communicate with the AI.
- Ensure the interviewer is unaware of the local guidance and queries.
- Improve AI response relevance by combining transcription (primarily loopback) with private text input.

## Actors
- Assistant User (Interviewee): the person receiving help during the interview or meeting.
- AI Model: consumes audio transcription plus the supplemental text input.
- Application: captures audio (loopback preferred; microphone available for other use cases), transcribes speech, and routes both transcription and text input to the model.

## Preconditions
- Application is running and audio capture/transcription is active (loopback preferred).
- Network connectivity available for model API calls.
- The text input pane is visible and enabled.

## Triggers
- Interviewee types or pastes text into the additional input field and submits it.
- Interviewee updates or clears the text input during the session.

## Main Flow
1. Application captures loopback audio from the meeting app and starts transcription.
2. Interviewee opens the additional text input pane.
3. Interviewee enters guidance (e.g., context, constraints, glossary, preferred tone) or asks a private question.
4. On submit, the application:
   - Validates the text (non-empty, length within limits).
   - Associates the text with the current session.
   - Sends the text as an additional input channel to the model, alongside the latest loopback audio transcription.
5. The AI Model generates responses that incorporate both transcription and the supplemental text.
6. The application displays:
   - Transcribed speech in the transcript window.
   - Detected questions/imperatives in the detection window.
   - AI responses influenced by the additional text input.
7. Interviewee can edit, replace, or clear the text input; subsequent responses reflect the latest input.

## Alternate Flows
- Microphone Use Cases (separate): In dedicated scenarios (e.g., local dictation, personal notes, or off-meeting practice), microphone input may be used as the primary capture source.
- Capture Fallback: If loopback capture is unavailable, the system may optionally switch to microphone capture depending on the configured policy for the specific use case.
- Empty Input: If the text input is empty, only audio transcription is used.
- Large Input: If the text exceeds the configured maximum, the app prompts to trim or chunk the input.
- Expired Context: If session resets, text input must be re-submitted to re-establish context.
- Safety/Policy Filters: If the text violates policies, it is blocked with a warning and not sent.

## Postconditions
- The session maintains the latest submitted text input as active guidance until cleared or the session ends.
- Logged events (if enabled) note text input changes without storing sensitive content unless explicitly configured.

## UX Notes
- Indicator when additional text input is active (e.g., “Context applied”).
- Quick actions: apply, edit, clear, and optional history of recent inputs.
- Show character count and limits.
- Explicit submission (button or keybinding) to avoid accidental sends.
- Local-only UI for private input; avoid audio feedback or screen prompts visible to interviewer.

## Privacy & Transparency
- The interviewer should not be aware of monitoring or private input; ensure local-only UI and no outbound signals to the meeting platform beyond standard model calls.
- Do not persist sensitive text input by default; add a setting to opt-in with clear consent.
- Respect organizational policies and applicable regulations.

## Non-Functional Requirements
- Low-latency merging of audio transcription and text input (<300 ms added processing).
- Robustness to rapid updates (debounce or versioning to avoid race conditions).
- Configurable max input length and sanitization.
- Observability: metrics for input usage and impact on response quality (optional).

## Acceptance Criteria
- With loopback capture active, interviewer questions are transcribed and surfaced; AI responses reflect private text input.
- Microphone-based scenarios function as defined in their separate use cases without exposing private input to the interviewer.
- Optional capture fallback policy behaves as configured when loopback is unavailable.
- Clearing the text input removes its influence immediately.
- Input length limits and policy filters are enforced with clear user feedback.
- No persistence of the text input by default unless explicitly enabled.
