import type { Device, DeviceType, DeviceTypeCreateUpdate } from "@/types";

const BASE = "/api";

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `HTTP ${res.status}`);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

export async function fetchDevices(params?: {
  search?: string;
  typeId?: number;
  available?: boolean;
}): Promise<Device[]> {
  const query = new URLSearchParams();
  if (params?.search) query.set("search", params.search);
  if (params?.typeId != null) query.set("typeId", String(params.typeId));
  if (params?.available != null) query.set("available", String(params.available));
  const qs = query.toString();
  const res = await fetch(`${BASE}/devices${qs ? `?${qs}` : ""}`);
  return handleResponse<Device[]>(res);
}

export async function fetchDevice(id: number): Promise<Device> {
  const res = await fetch(`${BASE}/devices/${id}`);
  return handleResponse<Device>(res);
}

export async function createDevice(data: FormData): Promise<Device> {
  const res = await fetch(`${BASE}/devices`, { method: "POST", body: data });
  return handleResponse<Device>(res);
}

export async function updateDevice(id: number, data: FormData): Promise<Device> {
  const res = await fetch(`${BASE}/devices/${id}`, { method: "PUT", body: data });
  return handleResponse<Device>(res);
}

export async function deleteDevice(id: number): Promise<void> {
  const res = await fetch(`${BASE}/devices/${id}`, { method: "DELETE" });
  return handleResponse<void>(res);
}

export async function fetchDeviceTypes(): Promise<DeviceType[]> {
  const res = await fetch(`${BASE}/devicetypes`);
  return handleResponse<DeviceType[]>(res);
}

export async function createDeviceType(data: DeviceTypeCreateUpdate): Promise<DeviceType> {
  const res = await fetch(`${BASE}/devicetypes`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  return handleResponse<DeviceType>(res);
}

export async function updateDeviceType(id: number, data: DeviceTypeCreateUpdate): Promise<DeviceType> {
  const res = await fetch(`${BASE}/devicetypes/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  return handleResponse<DeviceType>(res);
}

export async function deleteDeviceType(id: number): Promise<void> {
  const res = await fetch(`${BASE}/devicetypes/${id}`, { method: "DELETE" });
  return handleResponse<void>(res);
}
