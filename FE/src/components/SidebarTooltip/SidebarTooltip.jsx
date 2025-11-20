import React, { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import "./SidebarTooltip.css";

const SidebarTooltip = ({
  label,
  placement = "right",
  offset = 14,
  disabled = false,
  children,
}) => {
  const triggerRef = useRef(null);
  const [coords, setCoords] = useState({ top: 0, left: 0 });
  const [isVisible, setIsVisible] = useState(false);
  const [isMounted, setIsMounted] = useState(false);
  const tooltipId = useId();

  const updateCoords = () => {
    if (!triggerRef.current) return;
    const rect = triggerRef.current.getBoundingClientRect();
    let top = rect.top + rect.height / 2;
    let left = rect.right + offset;

    if (placement === "left") {
      left = rect.left - offset;
    } else if (placement === "top") {
      top = rect.top - offset;
      left = rect.left + rect.width / 2;
    } else if (placement === "bottom") {
      top = rect.bottom + offset;
      left = rect.left + rect.width / 2;
    }

    setCoords({ top, left });
  };

  const showTooltip = () => {
    if (disabled) return;
    updateCoords();
    setIsVisible(true);
  };

  const hideTooltip = () => {
    if (disabled) return;
    setIsVisible(false);
  };

  useEffect(() => {
    if (isVisible && !disabled) {
      setIsMounted(true);
    } else {
      const timeout = setTimeout(() => {
        setIsMounted(false);
      }, 120);
      return () => clearTimeout(timeout);
    }
  }, [isVisible, disabled]);

  useEffect(() => {
    if (!isVisible || disabled) return;
    const handleReposition = () => updateCoords();
    window.addEventListener("resize", handleReposition);
    window.addEventListener("scroll", handleReposition, true);
    return () => {
      window.removeEventListener("resize", handleReposition);
      window.removeEventListener("scroll", handleReposition, true);
    };
  }, [isVisible, disabled]);

  const tooltipNode =
    isMounted && !disabled
      ? createPortal(
          <div
            className={`sb-tooltip sb-tooltip-${placement}${
              isVisible ? " sb-tooltip-visible" : ""
            }`}
            role="tooltip"
            id={tooltipId}
            style={{
              top: `${coords.top}px`,
              left: `${coords.left}px`,
            }}
          >
            <span>{label}</span>
          </div>,
          document.body
        )
      : null;

  return (
    <>
      <div
        className="sb-tooltip-trigger"
        ref={triggerRef}
        onMouseEnter={showTooltip}
        onMouseLeave={hideTooltip}
        onFocus={showTooltip}
        onBlur={hideTooltip}
        aria-describedby={!disabled ? tooltipId : undefined}
      >
        {children}
      </div>
      {tooltipNode}
    </>
  );
};

export default SidebarTooltip;

