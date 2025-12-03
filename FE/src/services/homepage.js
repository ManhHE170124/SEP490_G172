import axiosClient from "../api/axiosClient";

export const homepageApi = {
  getSummary: () => axiosClient.get("homepage"),
};

export default homepageApi;
