export interface Device {
  id: number;
  deviceName: string;
  deviceType: number;
  deviceTypeName: string;
  serial: string;
  price: number;
  rentPrice: number;
  available: boolean;
  picture: string | null;
  rentCount: number;
  notes: string | null;
  reservedUntil: string | null;
}

export interface DeviceCreateUpdate {
  deviceName: string;
  deviceType: number;
  serial?: string;
  price: number;
  rentPrice: number;
  available: boolean;
  notes?: string;
}

export interface DeviceType {
  id: number;
  typeName: string;
  deviceCount: number;
}

export interface DeviceTypeCreateUpdate {
  typeName: string;
}
