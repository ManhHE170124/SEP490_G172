import axiosClient from "../api/axiosClient";

const storefrontBannerService = {
    getPublicByPlacement: (placement) =>
        axiosClient.get("banners/public", { params: { placement } }),
};

export default storefrontBannerService;
