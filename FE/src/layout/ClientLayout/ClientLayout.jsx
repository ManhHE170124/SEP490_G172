import React from "react";
import Header from "../ClientLayout/PublicHeader.jsx";
import Footer from "../ClientLayout/PublicFooter.jsx";


const ClientLayout = ({ children }) => {
    return (
        <div >
            <Header />
            <main className="al-admin-main">
                {children}
            </main>
            <Footer />
        </div>
    );
};

export default ClientLayout;
