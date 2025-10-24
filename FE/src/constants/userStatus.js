/**
 * File: userStatus.js
 * Layer: UI Constants
 * Purpose: Define user statuses for UI and filter dropdown options.
 */

export const USER_STATUS = Object.freeze({
  Active: "Active",
  Locked: "Locked",
  Disabled: "Disabled",
});

export const USER_STATUS_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: USER_STATUS.Active, label: "Đang hoạt động" },
  { value: USER_STATUS.Locked, label: "Đã khóa" },
  { value: USER_STATUS.Disabled, label: "Ngừng hoạt động" },
];
