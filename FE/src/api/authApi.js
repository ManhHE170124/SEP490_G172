import axiosClient from "./axiosClient";

const END_POINT = {
  LOGIN: "auth/login",
  REGISTER: "auth/register",
};

export const authApi = {
  login: (data) => axiosClient.post(END_POINT.LOGIN, data),
  register: (data) => axiosClient.post(END_POINT.REGISTER, data),
};
