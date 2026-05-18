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

export interface SqlConnectionSettings {
  server: string;
  port: number;
  database: string;
  userId: string;
  password: string;
  trustServerCertificate: boolean;
}

export interface SettingsFileDescriptor {
  key: string;
  fileName: string;
  storedPath: string;
  exists: boolean;
  canServe: boolean;
  downloadUrl: string | null;
  previewUrl: string | null;
  status: string;
}

export interface ApplicationSettings {
  companyName: string;
  emailSmtp: string;
  smtpPort: number;
  senderEmail: string;
  emailPassword: string;
  emailPasswordConfigured: boolean;
  emailPasswordNeedsReset: boolean;
  senderName: string;
  ccAddress: string;
  emailSubject: string;
  reviewEmailSubject: string;
  googleReview: string;
  defaultRentalDays: number;
  reviewEmailDelayDays: number;
}

export interface SettingsFiles {
  companyLogo: SettingsFileDescriptor;
  templateContract: SettingsFileDescriptor;
  aszfFile: SettingsFileDescriptor;
  contractEmailTemplate: SettingsFileDescriptor;
  reviewEmailTemplate: SettingsFileDescriptor;
  invoiceXml: SettingsFileDescriptor;
}

export interface SettingsResponse {
  sql: SqlConnectionSettings;
  databaseStatus: {
    canConnect: boolean;
    message: string;
  };
  application: ApplicationSettings;
  files: SettingsFiles;
}
