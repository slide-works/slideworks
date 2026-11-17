# Presenting

This page is for the person giving the talk: the presenter window on the laptop, the audience window on the projector, the phone remote, the keys, deep dives, the talk length and what happens when the Wi-Fi drops.

## Starting a talk

Only the deck's owner can present it. On the deck page, the arrow next to **Present** opens three ways in:

- **Present** (the main button) opens the deck's player as it always did.
- **Presenter view** opens the audience window in a new window (`/d/{id}/{slug}?present`) and the presenter window in this tab (`/decks/{id}/present`). Both open from the same click, so the browser's popup blocker lets them through.
- **Control from your phone** shows a QR code and a link. Scan it with your phone, or open the link there.

The audience window is the normal player. It follows the talk when the viewer owns the deck, and otherwise it shows the deck as usual. A window that opens without the owner signed in can't follow the talk; sign in as the owner on that machine instead.

Only the owner's signed-in sessions can drive a talk, on the laptop or from a phone. Anyone with the link can still view the deck.

## The presenter window

- **Now** shows the current slide, **Next** the next reveal, slide or the end of the deck, and **Notes** the speaker notes. Notes are visible only to the owner.
- The **timer** starts on the first move (a slide or a reveal), not when the window opens. The **Start timer** button or the phone starts it too. It counts up. With a talk length set, it shows the time left and turns amber at 5 minutes left and red once over. The pause button pauses and resumes it, and the reset button (or T) sets it back to zero. After a reset it stays at zero until the next move.
- Notes use a font size you can change; the size is kept in this browser only.
- Clicking a slide in the **Overview** jumps there. The overview groups slides by part.

### Keys

| Key | Does |
|---|---|
| → / PageDown / Space | Next |
| ← / PageUp | Previous |
| Home / End | First / last slide |
| A slide number, then Enter | Jump to that slide (up to 4 digits) |
| O | Overview |
| N | Notes |
| B or . | Blackout (the audience window goes black; press again to return) |
| D | Skip deep dives |
| T | Reset the timer |
| Esc | Close the overview or the open dialog |

The audience window takes the same movement keys (→, PageDown and Space for next, ← and PageUp for previous, Home and End), plus Shift+Space for previous and F for fullscreen. You can drive from its own screen if you have to.

## Deep dives

A slide marked as a deep dive is skipped while **Skip deep dives** is on (D, or the toolbar button). The next-slide panel names the deep dives it skips, so you know what you're passing over. Turning skipping off brings them back in order.

## Talk length

A deck can have an optional talk length in whole minutes (1 to 600). It is set by an agent with `set_talk_length`, or through `PUT /api/decks/{id}/talk-length` by the owner. Clearing it (`null`) removes the countdown; the timer then only counts up. Each change appears in the deck's activity.

## The phone remote

Open the phone remote from the Present menu's **Control from your phone** item, or go to `/remote` on the phone while signed in as the owner.

- **Pick the talk**: `/remote` lists your live talks. A talk that is live opens straight to its speaker mode when you arrive from its QR code.
- **Speaker mode** (`/remote/{id}`): the current slide, then its notes underneath, then Next and Previous. **Slides** opens a list of every slide to jump to; a tap jumps there and closes the list. Blackout and Skip deep dives are on the same screen.
- The phone shows a status line: **Live**, **Connecting** or **Reconnecting**. Tapping the timer starts, pauses or resumes it.
- The screen stays awake while the page is open where the phone browser supports it; otherwise it may dim.

The phone and the laptop share one state. Pressing a key or button on either one moves both windows. Every control waits for the server to confirm the change, so a button that fails shows a message and changes nothing.

## When the Wi-Fi drops

- The presenter window shows **Reconnecting · the audience window won't follow until it's back**. Until the connection is back, moves, blackout, skip and the timer do nothing there: only the server moves the talk. O, N, Esc and the Overview and Notes buttons still work.
- The audience window's own keys still move its local player while it is offline. Those moves don't skip deep dives, and they are dropped when it reconnects: the live state wins.
- When the connection returns, the presenter window and the audience window catch up to the latest state.
- The phone shows **Reconnecting** and rejoins when the page becomes visible again, for example after the screen was locked.
- If every window leaves while the server keeps running, the presentation is kept in memory for 2 hours. Reopening a window within that time resumes it where it was.
- If the server restarts, its memory is empty. The first window to rejoin recreates the presentation from its own last position and timer.

## Limits

- The presentation state lives in one server process and is not shared between instances. Running two instances needs a shared bridge first.
- Two very quick presses on two devices can collapse into one move, because each press sends the absolute position and the last one wins.
- The presenter window can't make the audience window fullscreen; press **F** in the audience window itself.
