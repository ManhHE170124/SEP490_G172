/**
 * Static mock data for order history and order detail pages.
 * The shape follows the backend DTO contracts so that the UI can
 * later be wired to real APIs with minimal changes.
 */

export const ORDER_DETAIL_MOCK = {
  "f4b2e8c0-8f1e-4c1d-9a34-2c7adb51f201": {
    orderId: "f4b2e8c0-8f1e-4c1d-9a34-2c7adb51f201",
    orderNumber: "ORD-20250115-F4B2",
    userId: "7b624aa4-5a2c-4ce8-9d6f-2cde2f01a111",
    userName: "Nguyễn Minh An",
    userEmail: "annguyen@example.com",
    userPhone: "0981 234 567",
    totalAmount: 1450000,
    discountAmount: 130000,
    finalAmount: 1320000,
    status: "Completed",
    paymentStatus: "Paid",
    createdAt: "2025-01-15T09:30:00Z",
    orderDetails: [
      {
        orderDetailId: 101,
        productId: "a6a265a2-6d42-4c15-b8f9-098a6eb000aa",
        productName: "Microsoft Office 365 Personal (12 tháng)",
        productCode: "O365-12M-PER",
        productType: "Software",
        thumbnailUrl:
          "https://images.unsplash.com/photo-1580894906472-1e16c1f03f17?auto=format&fit=crop&w=200&q=60",
        quantity: 1,
        unitPrice: 890000,
        keyString: null,
        accountEmail: "delannah.williams@student.uagc.edu",
        accountPassword: "Divine4091!",
        subTotal: 890000,
      },
      {
        orderDetailId: 102,
        productId: "20ffa7fb-0f6b-4b64-9064-24bb50b320f1",
        productName: "Windows 11 Pro Retail Key",
        productCode: "WIN11-PRO",
        productType: "OperatingSystem",
        thumbnailUrl:
          "https://images.unsplash.com/photo-1587613865363-4b8b0b3f0a9d?auto=format&fit=crop&w=200&q=60",
        quantity: 1,
        unitPrice: 560000,
        keyString: "WX3P-LL9Q-UD2A-JK88",
        subTotal: 560000,
      },
    ],
    payments: [
      {
        paymentId: "f27415d7-7f38-401a-b8d5-c2e52187683b",
        amount: 1320000,
        status: "Completed",
        paymentMethod: "Bank Transfer",
        createdAt: "2025-01-15T09:45:00Z",
      },
    ],
  },
  "3d8a7f94-fc1a-4cc0-9c0f-2e5f9f5c77b2": {
    orderId: "3d8a7f94-fc1a-4cc0-9c0f-2e5f9f5c77b2",
    orderNumber: "ORD-20250111-3D8A",
    userId: "b5a49555-f34f-4a68-9b91-6468345f0b22",
    userName: "Trần Việt Khoa",
    userEmail: "vietkhoa@example.com",
    userPhone: "0902 556 788",
    totalAmount: 980000,
    discountAmount: 80000,
    finalAmount: 900000,
    status: "Processing",
    paymentStatus: "Partial",
    createdAt: "2025-01-11T13:05:00Z",
    orderDetails: [
      {
        orderDetailId: 201,
        productId: "70c3b01a-b9f5-42e1-9a9d-cb90c78d8ad0",
        productName: "Adobe Creative Cloud Photography (12 tháng)",
        productCode: "ADOBE-PHOTO",
        productType: "Software",
        thumbnailUrl:
          "https://images.unsplash.com/photo-1503602642458-232111445657?auto=format&fit=crop&w=200&q=60",
        quantity: 1,
        unitPrice: 650000,
        keyString: "ADOB-8XCM-22KQ-PHOTO",
        subTotal: 650000,
      },
      {
        orderDetailId: 202,
        productId: "f0a4eb1e-2285-4df2-8e35-77f728b9f7c6",
        productName: "Canva Pro Team (3 tháng)",
        productCode: "CANVA-TEAM",
        productType: "Service",
        thumbnailUrl:
          "https://images.unsplash.com/photo-1498050108023-c5249f4df085?auto=format&fit=crop&w=200&q=60",
        quantity: 1,
        unitPrice: 330000,
        keyString: null,
        subTotal: 330000,
      },
    ],
    payments: [
      {
        paymentId: "d77e2cc7-3fa8-4398-9fe8-19a9dcabf431",
        amount: 500000,
        status: "Completed",
        paymentMethod: "Credit Card",
        createdAt: "2025-01-11T13:20:00Z",
      },
      {
        paymentId: "9d5f8d46-4979-4cf4-9abe-d4048ab150ea",
        amount: 400000,
        status: "Pending",
        paymentMethod: "Credit Card",
        createdAt: "2025-01-12T08:15:00Z",
      },
    ],
  },
  "92d7d85a-d552-4a9f-9e90-31f1b1e82f75": {
    orderId: "92d7d85a-d552-4a9f-9e90-31f1b1e82f75",
    orderNumber: "ORD-20250104-92D7",
    userId: "197ce1af-d4a2-435e-9bc0-0cd9cde3880a",
    userName: "Phạm Thu Hà",
    userEmail: "thuha@example.com",
    userPhone: "0974 112 345",
    totalAmount: 450000,
    discountAmount: 0,
    finalAmount: 450000,
    status: "Pending",
    paymentStatus: "Unpaid",
    createdAt: "2025-01-04T07:55:00Z",
    orderDetails: [
      {
        orderDetailId: 301,
        productId: "6cb8685c-6a69-4a58-9f2a-91599544c6f1",
        productName: "Udemy: Khóa học Excel nâng cao",
        productCode: "UDEMY-EXCEL-ADV",
        productType: "Course",
        thumbnailUrl:
          "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=200&q=60",
        quantity: 1,
        unitPrice: 450000,
        keyString: null,
        subTotal: 450000,
      },
    ],
    payments: [],
  },
};

export const ORDER_HISTORY_MOCK = Object.values(ORDER_DETAIL_MOCK).map(
  ({
    orderId,
    orderNumber,
    createdAt,
    totalAmount,
    finalAmount,
    status,
    paymentStatus,
    orderDetails,
  }) => ({
    orderId,
    orderNumber,
    createdAt,
    totalAmount,
    finalAmount,
    status,
    paymentStatus,
    itemCount: orderDetails.length,
    productNames: orderDetails.map((detail) => detail.productName),
    thumbnailUrl: orderDetails[0]?.thumbnailUrl ?? "",
  })
);

