import formatDateTime from "../../utils/formatDatetime";
import { getStatusColor, getStatusLabel } from "../../utils/productKeyHepler";

export default function ViewKeysModal({
  isOpen,
  onClose,
  packageKeys,
  loading,
}) {
  if (!isOpen) return null;

  return (
    <div
      style={{
        position: "fixed",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        background: "rgba(0, 0, 0, 0.5)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 9999,
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: "white",
          borderRadius: "8px",
          padding: "24px",
          maxWidth: "800px",
          width: "90%",
          maxHeight: "80vh",
          overflow: "auto",
          boxShadow: "0 4px 6px rgba(0, 0, 0, 0.1)",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: "16px",
          }}
        >
          <h2 style={{ margin: 0 }}>Chi tiết License Keys</h2>
          <button
            className="btn"
            onClick={onClose}
            style={{ padding: "4px 12px" }}
          >
            Đóng
          </button>
        </div>

        {loading ? (
          <p style={{ textAlign: "center", padding: "20px" }}>Đang tải...</p>
        ) : packageKeys ? (
          <>
            <div
              style={{
                marginBottom: "16px",
                padding: "12px",
                background: "#f8fafc",
                borderRadius: "4px",
              }}
            >
              <p style={{ margin: "0 0 4px", fontSize: "14px" }}>
                <strong>Sản phẩm:</strong> {packageKeys.productName}
              </p>
              <p style={{ margin: "0 0 4px", fontSize: "14px" }}>
                <strong>Nhà cung cấp:</strong> {packageKeys.supplierName}
              </p>
              <p style={{ margin: "0", fontSize: "14px" }}>
                <strong>Tổng số keys:</strong> {packageKeys.totalKeys}
              </p>
            </div>

            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>License Key</th>
                    <th>Trạng thái</th>
                    <th>Ngày nhập</th>
                    <th>Người nhập</th>
                  </tr>
                </thead>
                <tbody>
                  {packageKeys.keys.length === 0 ? (
                    <tr>
                      <td
                        colSpan="5"
                        style={{ textAlign: "center", padding: "20px" }}
                      >
                        Chưa có key nào
                      </td>
                    </tr>
                  ) : (
                    packageKeys.keys.map((key, index) => (
                      <tr key={key.keyId}>
                        <td>{index + 1}</td>
                        <td
                          style={{
                            fontFamily: "monospace",
                            fontSize: "12px",
                          }}
                        >
                          {key.keyString}
                        </td>
                        <td>
                          <span
                            style={{
                              display: "inline-block",
                              padding: "2px 8px",
                              borderRadius: "4px",
                              fontSize: "12px",
                              background: getStatusColor(key.status).bg,
                              color: getStatusColor(key.status).color,
                            }}
                          >
                            {getStatusLabel(key.status)}
                          </span>
                        </td>
                        <td>{formatDateTime(key.importedAt)}</td>
                        <td>{key.importedByEmail || "-"}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </>
        ) : null}
      </div>
    </div>
  );
}
