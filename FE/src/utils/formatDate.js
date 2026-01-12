export const formatDate = (dateString) => {
  if (!dateString) return "-";
  const date = new Date(dateString);
  return date.toLocaleDateString("vi-VN");
};

export const formatVietnameseDate = (dateString) => {
  if (!dateString) return "-";
  // Parse YYYY-MM-DD format directly to avoid timezone issues
  const [year, month, day] = dateString.split("T")[0].split("-");
  return `${day}/${month}/${year}`;
};
