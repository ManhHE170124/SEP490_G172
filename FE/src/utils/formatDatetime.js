const formatDateTime = (dateString) => {
  if (!dateString) return "-";
  // Ensure the date string is treated as UTC by appending 'Z' if not present
  const utcDateString = dateString.endsWith("Z")
    ? dateString
    : `${dateString}Z`;
  const date = new Date(utcDateString);
  return date.toLocaleString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });
};

export default formatDateTime;
