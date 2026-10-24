# Dialogue module

NPCs talk in speech bubbles. An NPC module publishes its lines one at a time as `SayLine`; this
module shows the line in a cute rounded bubble at the bottom of the screen, types it out letter by
letter with a blip per letter, and lets the player continue with Interact.

- `Contract/` — `Fungtopia.Dialogue.Contract` (namespace `Fungtopia.Dialogue`):
  - `SayLine(int SpeakerId, string Speaker, string Text, int Voice, bool Last)`: published by NPC
    modules, one line at a time. `Voice`: 0 deadpan, 1 bubbly, 2 kind, 3 neutral (constants in
    `Voices`; anything else is neutral). A new line replaces the shown one (also a new talk).
  - `DialogueAdvanced(int SpeakerId)`: the player pressed Interact on a fully shown line; the
    speaker answers with its next `SayLine` (right away or on a later tick).
  - `DialogueState(bool Open, int SpeakerId)`: every tick; `SpeakerId` is -1 while closed.
  - `DialogueClosed(int SpeakerId)`: once, after the player dismisses a `Last` line (published
    right after that line's `DialogueAdvanced`).
- `Core/` — `Fungtopia.Dialogue.Core`: `DialogueModule`. It listens to `SayLine` and
  `Fungtopia.Input.PlayerInput.Interact` and acts on the press at its next tick:
  - while the text is still typing out (`LettersPerSecond` = 40), Interact completes it;
  - on a fully shown line, Interact publishes `DialogueAdvanced`; after a `Last` line it closes and
    publishes `DialogueClosed`, otherwise it waits (`Waiting`, no arrow) for the next line.
  - A press counts only once a line has been shown for a tick, so the press that starts a talk
    does not skip its first line. Presses while closed or waiting do nothing.
  - The view reads `Open`, `Speaker`, `Text`, `Voice`, `VisibleLetters`, `Typing`,
    `CanContinue` and `LineCount` (changes with every new line).
- `Godot/` — `Main.tscn` with `DialogueView` (a `CanvasLayer` on layer 12): `SpeechBubble`
  (drawn in code: a cream rounded bubble with an ink outline and soft shadow, the speaker's name on
  a spotted pink mushroom-cap tab, a pink arrow bouncing when you can continue), the text typing
  out through `Label.VisibleCharacters`, and `Blips`: four short synthesized blips, one per voice
  (deadpan a low flat buzz, bubbly a high chirp sliding up, kind a soft warm triangle, neutral a
  plain mid tone), with a small per-letter pitch wobble; spaces and punctuation are silent.
  `Preview.tscn` shows it over a backdrop with a scripted line from Millie, for screenshots:
  `SCREENSHOT_FRAMES=200 ./screenshot.sh modules/Dialogue/Godot/Preview.tscn`, or half typed with
  `DIALOGUE_PREVIEW=typing`.
- `Tests/` — `DialogueModuleTests.cs`: xUnit tests of `DialogueModule` with a fake `IMessageBus`
  and a fake NPC: line flow, completing the typing vs advancing, the last line closing, states.

Test it with `./test.sh Dialogue` from the repository root.
