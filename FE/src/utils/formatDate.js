export const formatDate = (dateString) => {
  if (!dateString) return "-";
  const date = new Date(dateString);
  return date.toLocaleDateString("vi-VN");
};

export const formatVietnameseDate = (dateString) => {
  if (!dateString) return "-";
  const date = new Date(dateString.split("T")[0]);
  return `${date.getDate()+1}/${(date.getMonth() + 1)}/${date.getFullYear()}`;
};
