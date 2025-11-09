export const getAccountStatusLabel = (status) => {
  switch (status) {
    case "Active":
      return "Hoạt động";
    case "Full":
      return "Đầy";
    case "Expired":
      return "Hết hạn";
    case "Error":
      return "Lỗi";
    case "Inactive":
      return "Không hoạt động";
    default:
      return status;
  }
};

export const getAccountStatusColor = (status) => {
  switch (status) {
    case "Active":
      return { bg: "#d1fae5", color: "#065f46" }; // Green
    case "Full":
      return { bg: "#fef3c7", color: "#92400e" }; // Yellow
    case "Expired":
      return { bg: "#fecaca", color: "#7f1d1d" }; // Dark red
    case "Error":
      return { bg: "#fee2e2", color: "#991b1b" }; // Red
    case "Inactive":
      return { bg: "#f3f4f6", color: "#374151" }; // Gray
    default:
      return { bg: "#f3f4f6", color: "#374151" };
  }
};

export const maskPassword = (password) => {
  if (!password) return "";
  return "•".repeat(Math.min(password.length, 12));
};
