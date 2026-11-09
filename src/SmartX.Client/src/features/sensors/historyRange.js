// Facility time is fixed SAST (UTC+02:00), independent of browser timezone.
export const HISTORY_TIME_ZONE = "Africa/Johannesburg";

export function sastInputToUtc(value) {
  if (!value) return undefined;
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(value);
  if (!match) throw new TypeError("Enter a complete date and time.");
  const [, year, month, day, hour, minute, second = "00"] = match;
  const date = new Date(`${year}-${month}-${day}T${hour}:${minute}:${second}+02:00`);
  const local = new Date(date.getTime() + 2 * 60 * 60 * 1000);
  const canonical = `${year}-${month}-${day}T${hour}:${minute}:${second}`;
  if (Number(year) < 1000 || Number.isNaN(date.getTime()) || local.toISOString().slice(0, 19) !== canonical)
    throw new TypeError("Enter a valid date and time.");
  return date.toISOString();
}

export function buildHistoryRange(fromLocal = "", toLocal = "") {
  try {
    const fromUtc = sastInputToUtc(fromLocal);
    const toUtc = sastInputToUtc(toLocal);
    if (fromUtc && toUtc && fromUtc > toUtc)
      return { error: "The start date/time cannot be after the end date/time." };
    return { fromUtc, toUtc, error: null };
  } catch (error) {
    return { error: error.message };
  }
}
