const API_BASE = process.env.REACT_APP_API_URL; 

export async function post(path, body) {
  const res = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  const data = await res.json().catch(()=> ({}));
  if (!res.ok) throw { status: res.status, data };
  return data;
}