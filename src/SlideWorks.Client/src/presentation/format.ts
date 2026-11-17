// Times shown by the presenter window and the phone (D7). Both come from numbers the server sent, advanced with
// performance.now(): no Date object is made or read.

const pad = (n: number) => String(n).padStart(2, '0');

// The timer: m:ss under an hour and h:mm:ss from an hour, so 760000 is "12:40". Whole seconds, rounded down. A
// negative value (time left once a talk runs over) gets a leading minus.
export function formatElapsed(ms: number): string {
  const totalSeconds = Math.floor(Math.abs(ms) / 1000);
  const sign = ms < 0 && totalSeconds > 0 ? '-' : '';
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return hours > 0
    ? `${sign}${hours}:${pad(minutes)}:${pad(seconds)}`
    : `${sign}${minutes}:${pad(seconds)}`;
}

// The clock: minutes since local midnight as a 24-hour hh:mm, so 872 is "14:32". It wraps at a day (1440 is "00:00").
export function formatClock(minutes: number): string {
  const day = 1440;
  const inDay = ((Math.floor(minutes) % day) + day) % day;
  return `${pad(Math.floor(inDay / 60))}:${pad(inDay % 60)}`;
}
