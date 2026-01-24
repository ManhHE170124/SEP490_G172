// File: src/utils/formatDatetime.js
/**
 * Purpose: Format timestamps for UI display in UTC+7 (Asia/Bangkok).
 *
 * Rules:
 * - Backend + Database keep UTC.
 * - Frontend ONLY converts for display.
 *
 * Why this exists:
 * - .NET + SQL datetime2 often serialize as "YYYY-MM-DDTHH:mm:ss" (no 'Z').
 * - JavaScript parses that as LOCAL time, so the UI can show raw UTC after reload.
 * - We normalize no-timezone strings as UTC (append 'Z') then add +7 hours for display.
 */

const OFFSET_MS = 7 * 60 * 60 * 1000;

const pad2 = (n) => String(n).padStart(2, "0");

const hasTimezoneInfo = (s) => {
  // ends with Z or +hh:mm or +hhmm
  return /([zZ]|[+-]\d{2}:?\d{2})$/.test(s);
};

const normalizeUtcString = (raw) => {
  const s = String(raw || "").trim();
  if (!s) return "";

  // If already ISO with timezone (Z or +hh:mm), keep.
  if (hasTimezoneInfo(s)) return s;

  // If date-time has space between date and time: "YYYY-MM-DD HH:mm:ss"
  if (/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}/.test(s)) {
    return s.replace(" ", "T") + "Z";
  }

  // If looks like ISO without timezone: "YYYY-MM-DDTHH:mm:ss" or with milliseconds
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(s)) {
    return s + "Z";
  }

  // If date-only "YYYY-MM-DD" => treat as UTC midnight
  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) {
    return s + "T00:00:00Z";
  }

  return s;
};

/**
 * @param {string|Date|number|null|undefined} value
 * @param {boolean|{withSeconds?: boolean, dateOnly?: boolean}} [opts]
 * @returns {string}
 */
export default function formatDatetime(value, opts) {
  if (value === null || value === undefined || value === "") return "";

  // Back-compat: allow formatDatetime(value, true) to include seconds
  const options =
    typeof opts === "boolean" ? { withSeconds: opts } : (opts || {});

  let date;

  if (value instanceof Date) {
    date = value;
  } else if (typeof value === "number") {
    date = new Date(value);
  } else {
    const normalized = normalizeUtcString(value);
    date = new Date(normalized);
  }

  if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
    // If cannot parse, return as-is (avoid breaking UI)
    return String(value);
  }

  // Convert UTC -> UTC+7 for UI display
  const shifted = new Date(date.getTime() + OFFSET_MS);

  // Use UTC getters to avoid double-applying user's local timezone
  const dd = pad2(shifted.getUTCDate());
  const mm = pad2(shifted.getUTCMonth() + 1);
  const yyyy = shifted.getUTCFullYear();

  if (options.dateOnly) {
    return `${dd}/${mm}/${yyyy}`;
  }

  const HH = pad2(shifted.getUTCHours());
  const MM = pad2(shifted.getUTCMinutes());
  const SS = pad2(shifted.getUTCSeconds());

  return options.withSeconds
    ? `${dd}/${mm}/${yyyy} ${HH}:${MM}:${SS}`
    : `${dd}/${mm}/${yyyy} ${HH}:${MM}`;
}
