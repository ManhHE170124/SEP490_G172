export default function chunkText(value, chunkSize = 24) {
  if (value === null || value === undefined) {
    return [];
  }

  const safeValue = String(value);
  if (!safeValue.length) {
    return [];
  }

  if (safeValue.length <= chunkSize) {
    return [safeValue];
  }

  const regex = new RegExp(`.{1,${chunkSize}}`, "g");
  return safeValue.match(regex) || [safeValue];
}
