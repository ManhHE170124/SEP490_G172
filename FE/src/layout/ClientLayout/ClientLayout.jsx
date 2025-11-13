import React from "react";
import PublicHeader from "./PublicHeader.jsx";
import PublicFooter from "./PublicFooter.jsx";


const ClientLayout = ({ children }) => {
    return (
        <div >
            <PublicHeader />
            <main className="al-admin-main">
                {children}
            </main>
            <PublicFooter />
        </div>
    );
};

export default ClientLayout;
