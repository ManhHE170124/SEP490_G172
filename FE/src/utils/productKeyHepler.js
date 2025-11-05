export const getStatusLabel = (status) => {
  switch (status) {
    case "Available":
      return "Còn";
    case "Sold":
      return "Đã bán";
    case "Error":
      return "Lỗi";
    case "Recalled":
      return "Thu hồi";
    case "Expired":
      return "Hết hạn";
    default:
      return status;
  }
};

export const getStatusColor = (status) => {
  switch (status) {
    case "Available":
      return { bg: "#d1fae5", color: "#065f46" };
    case "Sold":
      return { bg: "#dbeafe", color: "#1e40af" };
    case "Error":
      return { bg: "#fee2e2", color: "#991b1b" };
    case "Recalled":
      return { bg: "#fef3c7", color: "#92400e" };
    case "Expired":
      return { bg: "#fecaca", color: "#7f1d1d" };
    default:
      return { bg: "#f3f4f6", color: "#374151" };
  }
};
