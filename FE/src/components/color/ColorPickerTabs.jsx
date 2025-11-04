import React from "react";

/* ===================== Color utils ===================== */
function clamp(n, min, max) { return Math.min(max, Math.max(min, n)); }

function rgbToHex(r, g, b) {
  const toHex = (x) => x.toString(16).padStart(2, "0");
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`.toLowerCase();
}
function hexToRgb(hex) {
  const m = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex || "");
  if (!m) return { r: 30, g: 64, b: 175 }; // fallback #1e40af
  return { r: parseInt(m[1], 16), g: parseInt(m[2], 16), b: parseInt(m[3], 16) };
}
function hsvToRgb(h, s, v) {
  h = ((h % 360) + 360) % 360;
  const c = v * s;
  const x = c * (1 - Math.abs((h / 60) % 2 - 1));
  const m = v - c;
  let rp = 0, gp = 0, bp = 0;
  if (h < 60) { rp = c; gp = x; bp = 0; }
  else if (h < 120) { rp = x; gp = c; bp = 0; }
  else if (h < 180) { rp = 0; gp = c; bp = x; }
  else if (h < 240) { rp = 0; gp = x; bp = c; }
  else if (h < 300) { rp = x; gp = 0; bp = c; }
  else { rp = c; gp = 0; bp = x; }
  return { r: Math.round((rp + m) * 255), g: Math.round((gp + m) * 255), b: Math.round((bp + m) * 255) };
}
function rgbToHsv(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  const d = max - min;
  let h = 0;
  if (d === 0) h = 0;
  else if (max === r) h = 60 * (((g - b) / d) % 6);
  else if (max === g) h = 60 * (((b - r) / d) + 2);
  else h = 60 * (((r - g) / d) + 4);
  if (h < 0) h += 360;
  const s = max === 0 ? 0 : d / max;
  const v = max;
  return { h, s, v };
}
function hslToHex(h, s, l) {
  s /= 100; l /= 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const m = l - c / 2;
  let rp = 0, gp = 0, bp = 0;
  if (0 <= h && h < 60) { rp = c; gp = x; bp = 0; }
  else if (60 <= h && h < 120) { rp = x; gp = c; bp = 0; }
  else if (120 <= h && h < 180) { rp = 0; gp = c; bp = x; }
  else if (180 <= h && h < 240) { rp = 0; gp = x; bp = c; }
  else if (240 <= h && h < 300) { rp = x; gp = 0; bp = c; }
  else { rp = c; gp = 0; bp = x; }
  return rgbToHex(Math.round((rp + m) * 255), Math.round((gp + m) * 255), Math.round((bp + m) * 255));
}
export function bestTextColor(hex) {
  const { r, g, b } = hexToRgb(hex);
  const L = (0.2126 * (r / 255)) + (0.7152 * (g / 255)) + (0.0722 * (b / 255));
  return L > 0.6 ? "#111" : "#fff";
}

/* ===================== Big Spectrum Picker ===================== */
function SpectrumPicker({ value, onChange }) {
  const { r, g, b } = hexToRgb(value);
  const seed = rgbToHsv(r, g, b);
  const [h, setH] = React.useState(seed.h);
  const [s, setS] = React.useState(seed.s);
  const [v, setV] = React.useState(seed.v);

  React.useEffect(() => {
    const { r, g, b } = hexToRgb(value);
    const { h: hh, s: ss, v: vv } = rgbToHsv(r, g, b);
    setH(hh); setS(ss); setV(vv);
  }, [value]);

  const squareRef = React.useRef(null);
  const draggingRef = React.useRef(false);

  const updateFromPointer = (clientX, clientY) => {
    const el = squareRef.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const x = clamp(clientX - rect.left, 0, rect.width);
    const y = clamp(clientY - rect.top, 0, rect.height);
    const ns = clamp(x / rect.width, 0, 1);
    const nv = clamp(1 - (y / rect.height), 0, 1);
    setS(ns); setV(nv);
    const { r, g, b } = hsvToRgb(h, ns, nv);
    onChange(rgbToHex(r, g, b));
  };

  const onPointerDown = (e) => {
    draggingRef.current = true;
    updateFromPointer(e.clientX, e.clientY);
    window.addEventListener("pointermove", onPointerMove);
    window.addEventListener("pointerup", onPointerUp, { once: true });
  };
  const onPointerMove = (e) => { if (draggingRef.current) updateFromPointer(e.clientX, e.clientY); };
  const onPointerUp = () => {
    draggingRef.current = false;
    window.removeEventListener("pointermove", onPointerMove);
  };

  const onHueChange = (e) => {
    const nh = Number(e.target.value);
    setH(nh);
    const { r, g, b } = hsvToRgb(nh, s, v);
    onChange(rgbToHex(r, g, b));
  };

  const indicatorStyle = {
    position: "absolute",
    left: `calc(${(s * 100).toFixed(2)}% - 6px)`,
    top: `calc(${((1 - v) * 100).toFixed(2)}% - 6px)`,
    width: 12, height: 12, borderRadius: "50%",
    boxShadow: "0 0 0 2px #fff, 0 0 0 3px rgba(0,0,0,.35)",
    pointerEvents: "none"
  };

  return (
    <div style={{ display: "grid", gridTemplateRows: "auto auto", gap: 10 }}>
      <div
        ref={squareRef}
        onPointerDown={onPointerDown}
        style={{
          position: "relative",
          width: "100%", height: 280, borderRadius: 10, cursor: "crosshair",
          background: `
            linear-gradient(to top, #000, rgba(0,0,0,0)),
            linear-gradient(to right, #fff, hsl(${h} 100% 50%))
          `,
          userSelect: "none"
        }}
        aria-label="Spectrum"
      >
        <div style={indicatorStyle} />
      </div>

      <input
        type="range" min={0} max={360} value={Math.round(h)} onChange={onHueChange}
        style={{
          width: "100%", height: 10, borderRadius: 10, appearance: "none",
          background: "linear-gradient(to right, #f00, #ff0, #0f0, #0ff, #00f, #f0f, #f00)",
          outline: "none"
        }}
      />
    </div>
  );
}

/* ===================== Sampler (big swatch grid) ===================== */
function SamplerGrid({ value, onChange }) {
  const hues = Array.from({ length: 12 }, (_, i) => i * 30); // 0..330
  const lightness = [92,86,80,74,68,62,56,50,44,38,32,26];
  const grays = Array.from({ length: 12 }, (_, i) => {
    const l = 100 - (i * 100 / 11);
    return hslToHex(0, 0, Math.round(l));
  });

  return (
    <div style={{ display: "flex", gap: 12 }}>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(12, 24px)", gap: 6 }}>
        {grays.map((hex) => (
          <button
            key={`g-${hex}`} type="button" onClick={() => onChange(hex)} title={hex}
            style={{
              width: 24, height: 24, borderRadius: 4,
              border: value === hex ? "2px solid var(--primary)" : "1px solid var(--line)",
              background: hex
            }}
          />
        ))}
        {lightness.map((l, ri) =>
          hues.map((h) => {
            const hex = hslToHex(h, 80, l);
            return (
              <button
                key={`${ri}-${h}`} type="button" onClick={() => onChange(hex)} title={hex}
                style={{
                  width: 24, height: 24, borderRadius: 4,
                  border: value === hex ? "2px solid var(--primary)" : "1px solid var(--line)",
                  background: hex
                }}
              />
            );
          })
        )}
      </div>
      <div style={{ flex: 1, minHeight: 280, borderRadius: 10, background: value, border: "1px solid var(--line)" }} />
    </div>
  );
}

/* ===================== Image picker (click to sample) ===================== */
function ImagePicker({ value, onChange }) {
  const [imgUrl, setImgUrl] = React.useState(null);
  const [imgName, setImgName] = React.useState("");
  const imgRef = React.useRef(null);
  const canvasRef = React.useRef(null);

  React.useEffect(() => () => { if (imgUrl) URL.revokeObjectURL(imgUrl); }, [imgUrl]);

  const onFile = (e) => {
    const f = e.target.files?.[0];
    if (!f) return;
    const url = URL.createObjectURL(f);
    if (imgUrl) URL.revokeObjectURL(imgUrl);
    setImgUrl(url);
    setImgName(f.name);
  };

  const clearImage = () => {
    if (imgUrl) URL.revokeObjectURL(imgUrl);
    setImgUrl(null);
    setImgName("");
  };

  const onImgClick = (e) => {
    const img = imgRef.current;
    const canvas = canvasRef.current;
    if (!img || !canvas) return;
    const rect = img.getBoundingClientRect();
    const scaleX = img.naturalWidth / rect.width;
    const scaleY = img.naturalHeight / rect.height;
    const x = Math.floor((e.clientX - rect.left) * scaleX);
    const y = Math.floor((e.clientY - rect.top) * scaleY);
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;
    const ctx = canvas.getContext("2d");
    ctx.drawImage(img, 0, 0);
    const data = ctx.getImageData(x, y, 1, 1).data;
    const hex = rgbToHex(data[0], data[1], data[2]);
    onChange(hex);
  };

  return (
    <div style={{ display: "grid", gap: 10 }}>
      <div className="file-upload">
        <input id="badgeSampleImg" type="file" accept="image/*" onChange={onFile} />
        <label htmlFor="badgeSampleImg" className="btn btn-upload">Chọn ảnh</label>
        <span className="file-name">{imgName || "Chưa chọn ảnh"}</span>
        {imgUrl && (
          <button type="button" className="btn" onClick={clearImage} style={{ marginLeft: 8 }}>
            Xóa ảnh
          </button>
        )}
      </div>

      {imgUrl ? (
        <img
          ref={imgRef} src={imgUrl} alt=""
          style={{ maxWidth: "100%", maxHeight: 320, borderRadius: 10, border: "1px solid var(--line)", cursor: "crosshair" }}
          onClick={onImgClick} title="Click lên ảnh để lấy màu"
        />
      ) : (
        <div style={{ padding: 12, border: "1px dashed var(--line)", borderRadius: 10 }}>
          Chọn ảnh để lấy màu theo điểm bạn click.
        </div>
      )}

      <span
        className="badge mono"
        style={{ background: value, color: bestTextColor(value), width: "fit-content" }}
        title={value}
      >
        {value}
      </span>

      <canvas ref={canvasRef} style={{ display: "none" }} />
    </div>
  );
}

/* ===================== Combined picker with tabs ===================== */
export function ColorPickerTabs({ value, onChange }) {
  const [tab, setTab] = React.useState("sampler"); // sampler | spectrum | image
  return (
    <div>
      <div className="row" style={{ gap: 8, marginBottom: 10 }}>
        <button type="button" className={`btn ${tab === "sampler" ? "primary" : ""}`} onClick={() => setTab("sampler")}>Màu mẫu</button>
        <button type="button" className={`btn ${tab === "spectrum" ? "primary" : ""}`} onClick={() => setTab("spectrum")}>Bảng màu</button>
        <button type="button" className={`btn ${tab === "image" ? "primary" : ""}`} onClick={() => setTab("image")}>Ảnh màu</button>
      </div>

      {tab === "sampler" && <SamplerGrid value={value} onChange={onChange} />}
      {tab === "spectrum" && <SpectrumPicker value={value} onChange={onChange} />}
      {tab === "image" && <ImagePicker value={value} onChange={onChange} />}
    </div>
  );
}

export default ColorPickerTabs;
