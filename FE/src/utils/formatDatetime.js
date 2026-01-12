// File: src/utils/formatDatetime.js
// Utility to format datetimes for UI in UTC+7 consistently
// File: src/utils/formatDatetime.js
// Utility to format datetimes for UI in UTC+7 consistently
function formatDatetime(input) {
  if (!input) return "—";
  const d = new Date(input);
  if (Number.isNaN(d.getTime())) return "—";
  // shift +7 hours to display in UTC+7
  const shifted = new Date(d.getTime() + 7 * 3600 * 1000);
  const pad = (v) => String(v).padStart(2, "0");
  const dd = pad(shifted.getUTCDate());
  const mm = pad(shifted.getUTCMonth() + 1);
  const yyyy = shifted.getUTCFullYear();
  const hh = pad(shifted.getUTCHours());
  const mi = pad(shifted.getUTCMinutes());
  const ss = pad(shifted.getUTCSeconds());
  return `${hh}:${mi}:${ss} ${dd}/${mm}/${yyyy}`;
}

export default formatDatetime;
