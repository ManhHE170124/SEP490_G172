import React from "react";
import PublicHeader from "./PublicHeader.jsx";
import PublicFooter from "./PublicFooter.jsx";

const ClientLayout = ({ children }) => {
  return (
    <div>
      <PublicHeader />
      {children}
      <PublicFooter />
    </div>
  );
};

export default ClientLayout;
