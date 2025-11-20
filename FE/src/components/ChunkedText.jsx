import React from "react";
import chunkText from "../utils/chunkText";

export default function ChunkedText({
  value,
  fallback = "-",
  chunkSize = 24,
  className = "",
  style,
}) {
  const resolvedValue =
    value === 0 || value === "0"
      ? "0"
      : typeof value === "string"
      ? value
      : value
      ? String(value)
      : "";

  const baseText = resolvedValue.trim() || fallback;
  const chunks = chunkText(baseText, chunkSize);

  return (
    <span
      className={["chunk-text", className].filter(Boolean).join(" ")}
      style={style}
    >
      {chunks.map((chunk, index) => (
        <React.Fragment key={`${chunk}-${index}`}>
          {chunk}
          {index < chunks.length - 1 && <br />}
        </React.Fragment>
      ))}
    </span>
  );
}
