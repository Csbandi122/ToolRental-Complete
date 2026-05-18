import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { Database, FileText, ImagePlus, Mail, Save, ShieldCheck, Star, TestTubeDiagonal } from "lucide-react";
import { fetchSettings, saveSettings, testSqlConnection } from "@/api/client";
import type {
  ApplicationSettings,
  SettingsFileDescriptor,
  SettingsFiles,
  SettingsResponse,
  SqlConnectionSettings,
} from "@/types";

type SettingsFormState = {
  sql: SqlConnectionSettings;
  application: ApplicationSettings;
};

type FileFieldState = {
  descriptor: SettingsFileDescriptor;
  selectedFile: File | null;
  clear: boolean;
};

type BannerState = {
  kind: "success" | "error" | "info";
  message: string;
} | null;

const emptyDescriptor = (key: string): SettingsFileDescriptor => ({
  key,
  fileName: "",
  storedPath: "",
  exists: false,
  canServe: false,
  downloadUrl: null,
  previewUrl: null,
  status: "Nincs fájl feltöltve.",
});

const initialFormState: SettingsFormState = {
  sql: {
    server: "",
    port: 1433,
    database: "",
    userId: "",
    password: "",
    trustServerCertificate: true,
  },
  application: {
    companyName: "Kerékpár Bérlő Kft.",
    emailSmtp: "",
    smtpPort: 587,
    senderEmail: "",
    emailPassword: "",
    emailPasswordConfigured: false,
    emailPasswordNeedsReset: false,
    senderName: "",
    ccAddress: "",
    emailSubject: "Bérlési szerződés",
    reviewEmailSubject: "Értékelje szolgáltatásunkat!",
    googleReview: "",
    defaultRentalDays: 1,
    reviewEmailDelayDays: 3,
  },
};

const fileKeys: Array<keyof SettingsFiles> = [
  "companyLogo",
  "templateContract",
  "aszfFile",
  "contractEmailTemplate",
  "reviewEmailTemplate",
  "invoiceXml",
];

const fileLabels: Record<keyof SettingsFiles, { title: string; description: string; accept: string; clearField: string }> = {
  companyLogo: {
    title: "Céglogó",
    description: "Kép fájl: .jpg, .png, .svg, .webp",
    accept: ".jpg,.jpeg,.png,.bmp,.gif,.webp,.svg",
    clearField: "clearCompanyLogo",
  },
  templateContract: {
    title: "Szerződés sablon",
    description: "Word fájl: .docx",
    accept: ".docx",
    clearField: "clearTemplateContract",
  },
  aszfFile: {
    title: "ÁSZF PDF",
    description: "PDF fájl: .pdf",
    accept: ".pdf",
    clearField: "clearAszfFile",
  },
  contractEmailTemplate: {
    title: "Szerződés email template",
    description: "HTML fájl: .html, .htm",
    accept: ".html,.htm",
    clearField: "clearContractEmailTemplate",
  },
  reviewEmailTemplate: {
    title: "Értékelés email template",
    description: "HTML fájl: .html, .htm",
    accept: ".html,.htm",
    clearField: "clearReviewEmailTemplate",
  },
  invoiceXml: {
    title: "Számla XML sablon",
    description: "XML fájl: .xml",
    accept: ".xml",
    clearField: "clearInvoiceXml",
  },
};

function createInitialFilesState(): Record<keyof SettingsFiles, FileFieldState> {
  return {
    companyLogo: { descriptor: emptyDescriptor("companyLogo"), selectedFile: null, clear: false },
    templateContract: { descriptor: emptyDescriptor("templateContract"), selectedFile: null, clear: false },
    aszfFile: { descriptor: emptyDescriptor("aszfFile"), selectedFile: null, clear: false },
    contractEmailTemplate: { descriptor: emptyDescriptor("contractEmailTemplate"), selectedFile: null, clear: false },
    reviewEmailTemplate: { descriptor: emptyDescriptor("reviewEmailTemplate"), selectedFile: null, clear: false },
    invoiceXml: { descriptor: emptyDescriptor("invoiceXml"), selectedFile: null, clear: false },
  };
}

function Banner({ banner }: { banner: BannerState }) {
  if (!banner) return null;

  const classes = banner.kind === "success"
    ? "border-emerald-200 bg-emerald-50 text-emerald-800"
    : banner.kind === "error"
      ? "border-rose-200 bg-rose-50 text-rose-800"
      : "border-sky-200 bg-sky-50 text-sky-800";

  return (
    <div className={`rounded-2xl border px-4 py-3 text-sm font-medium ${classes}`}>
      {banner.message}
    </div>
  );
}

function SectionCard({
  icon: Icon,
  title,
  description,
  children,
}: {
  icon: typeof Database;
  title: string;
  description: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-[2rem] border border-slate-200/80 bg-white/85 p-6 shadow-[0_20px_45px_rgba(15,23,42,0.06)]">
      <div className="mb-5 flex items-start gap-4">
        <div className="grid h-11 w-11 shrink-0 place-items-center rounded-2xl bg-slate-950 text-white">
          <Icon className="h-5 w-5" />
        </div>
        <div>
          <h3 className="text-lg font-semibold text-slate-950">{title}</h3>
          <p className="mt-1 text-sm leading-6 text-slate-600">{description}</p>
        </div>
      </div>

      <div className="grid gap-4">{children}</div>
    </section>
  );
}

function FileCard({
  fileKey,
  state,
  onFileChange,
  onClear,
}: {
  fileKey: keyof SettingsFiles;
  state: FileFieldState;
  onFileChange: (key: keyof SettingsFiles, file: File | null) => void;
  onClear: (key: keyof SettingsFiles) => void;
}) {
  const meta = fileLabels[fileKey];
  const objectUrl = useMemo(
    () => (state.selectedFile ? URL.createObjectURL(state.selectedFile) : null),
    [state.selectedFile],
  );

  useEffect(() => {
    return () => {
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [objectUrl]);

  const previewUrl = objectUrl ?? (!state.clear ? state.descriptor.previewUrl : null);

  return (
    <div className="rounded-2xl border border-slate-200 bg-slate-50/70 p-4">
      <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
        <div>
          <h4 className="text-sm font-semibold text-slate-900">{meta.title}</h4>
          <p className="mt-1 text-sm text-slate-600">{meta.description}</p>
        </div>

        {fileKey === "companyLogo" && previewUrl ? (
          <img
            src={previewUrl}
            alt="Céglogó előnézet"
            className="h-20 w-20 rounded-2xl border border-slate-200 bg-white object-cover"
          />
        ) : null}
      </div>

      <div className="mt-4 text-sm">
        <div className="font-medium text-slate-800">
          {state.clear ? "A fájl a következő mentéskor törlődik." : state.descriptor.status}
        </div>
        {!state.clear && state.descriptor.storedPath ? (
          <div className="mt-1 break-all text-slate-500">{state.descriptor.storedPath}</div>
        ) : null}
        {state.selectedFile ? (
          <div className="mt-2 text-slate-600">Kiválasztva: {state.selectedFile.name}</div>
        ) : null}
      </div>

      <div className="mt-4 flex flex-wrap gap-3">
        <label className="inline-flex cursor-pointer items-center gap-2 rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white">
          <ImagePlus className="h-4 w-4" />
          Feltöltés
          <input
            type="file"
            className="sr-only"
            accept={meta.accept}
            onChange={(event) => onFileChange(fileKey, event.target.files?.[0] ?? null)}
          />
        </label>

        <button
          type="button"
          onClick={() => onClear(fileKey)}
          className="rounded-full border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-100"
        >
          Fájl törlése
        </button>

        {!state.clear && state.descriptor.downloadUrl ? (
          <a
            href={state.descriptor.downloadUrl}
            target="_blank"
            rel="noreferrer"
            className="rounded-full border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-100"
          >
            Megnyitás
          </a>
        ) : null}
      </div>
    </div>
  );
}

export default function SettingsPage() {
  const [formState, setFormState] = useState<SettingsFormState>(initialFormState);
  const [fileState, setFileState] = useState<Record<keyof SettingsFiles, FileFieldState>>(createInitialFilesState());
  const [banner, setBanner] = useState<BannerState>({ kind: "info", message: "Beállítások betöltése..." });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [clearEmailPassword, setClearEmailPassword] = useState(false);

  useEffect(() => {
    loadSettings();
  }, []);

  async function loadSettings() {
    setLoading(true);
    try {
      const data = await fetchSettings();
      applySettings(data);
      setBanner({
        kind: data.databaseStatus.canConnect ? "success" : "error",
        message: data.databaseStatus.message,
      });
    } catch (error) {
      setBanner({
        kind: "error",
        message: error instanceof Error ? error.message : "A beállítások betöltése nem sikerült.",
      });
    } finally {
      setLoading(false);
    }
  }

  function applySettings(data: SettingsResponse) {
    setFormState({
      sql: data.sql,
      application: data.application,
    });

    setFileState({
      companyLogo: { descriptor: data.files.companyLogo, selectedFile: null, clear: false },
      templateContract: { descriptor: data.files.templateContract, selectedFile: null, clear: false },
      aszfFile: { descriptor: data.files.aszfFile, selectedFile: null, clear: false },
      contractEmailTemplate: { descriptor: data.files.contractEmailTemplate, selectedFile: null, clear: false },
      reviewEmailTemplate: { descriptor: data.files.reviewEmailTemplate, selectedFile: null, clear: false },
      invoiceXml: { descriptor: data.files.invoiceXml, selectedFile: null, clear: false },
    });

    setClearEmailPassword(false);
  }

  function updateSql<K extends keyof SqlConnectionSettings>(key: K, value: SqlConnectionSettings[K]) {
    setFormState((current) => ({
      ...current,
      sql: {
        ...current.sql,
        [key]: value,
      },
    }));
  }

  function updateApplication<K extends keyof ApplicationSettings>(key: K, value: ApplicationSettings[K]) {
    setFormState((current) => ({
      ...current,
      application: {
        ...current.application,
        [key]: value,
      },
    }));
  }

  function handleFileChange(key: keyof SettingsFiles, file: File | null) {
    setFileState((current) => ({
      ...current,
      [key]: {
        ...current[key],
        selectedFile: file,
        clear: false,
      },
    }));
  }

  function clearFile(key: keyof SettingsFiles) {
    setFileState((current) => ({
      ...current,
      [key]: {
        ...current[key],
        selectedFile: null,
        clear: true,
      },
    }));
  }

  async function handleTestSqlConnection() {
    setTesting(true);
    try {
      const result = await testSqlConnection(formState.sql);
      setBanner({ kind: "success", message: result.message });
    } catch (error) {
      setBanner({
        kind: "error",
        message: error instanceof Error ? error.message : "A kapcsolat tesztelése sikertelen.",
      });
    } finally {
      setTesting(false);
    }
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);

    try {
      const payload = new FormData();

      payload.append("sqlServer", formState.sql.server);
      payload.append("sqlPort", String(formState.sql.port));
      payload.append("sqlDatabase", formState.sql.database);
      payload.append("sqlUserId", formState.sql.userId);
      payload.append("sqlPassword", formState.sql.password);
      payload.append("sqlTrustServerCertificate", String(formState.sql.trustServerCertificate));

      payload.append("companyName", formState.application.companyName);
      payload.append("emailSmtp", formState.application.emailSmtp);
      payload.append("smtpPort", String(formState.application.smtpPort));
      payload.append("senderEmail", formState.application.senderEmail);
      payload.append("emailPassword", formState.application.emailPassword);
      payload.append("senderName", formState.application.senderName);
      payload.append("ccAddress", formState.application.ccAddress);
      payload.append("emailSubject", formState.application.emailSubject);
      payload.append("reviewEmailSubject", formState.application.reviewEmailSubject);
      payload.append("googleReview", formState.application.googleReview);
      payload.append("defaultRentalDays", String(formState.application.defaultRentalDays));
      payload.append("reviewEmailDelayDays", String(formState.application.reviewEmailDelayDays));
      payload.append("clearEmailPassword", String(clearEmailPassword));

      fileKeys.forEach((key) => {
        payload.append(fileLabels[key].clearField, String(fileState[key].clear));
        if (fileState[key].selectedFile) {
          payload.append(key, fileState[key].selectedFile as Blob);
        }
      });

      const result = await saveSettings(payload);
      setBanner({ kind: "success", message: result.message });
      await loadSettings();
    } catch (error) {
      setBanner({
        kind: "error",
        message: error instanceof Error ? error.message : "A mentés nem sikerült.",
      });
    } finally {
      setSaving(false);
    }
  }

  const emailPasswordHint = formState.application.emailPassword
    ? "Új email jelszó lesz mentve."
    : clearEmailPassword
      ? "Az email jelszó a következő mentéskor törlődni fog."
      : formState.application.emailPasswordNeedsReset
        ? "A jelenlegi jelszó még Windows DPAPI formában van a közös adatbázisban, ezért itt nem olvasható vissza. Ha weben is használnád, add meg újra."
        : formState.application.emailPasswordConfigured
          ? "Jelenleg van mentett email jelszó. Ha nem írsz be újat, a rendszer megtartja."
          : "Nincs mentett email jelszó.";

  return (
    <form className="space-y-6" onSubmit={handleSave}>
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.22em] text-sky-700">Windows logika, webes használat</p>
          <h2 className="mt-2 text-2xl font-semibold text-slate-950">Beállítások</h2>
        </div>

        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            onClick={handleTestSqlConnection}
            disabled={testing}
            className="inline-flex items-center gap-2 rounded-full border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-100 disabled:cursor-default disabled:opacity-70"
          >
            <TestTubeDiagonal className="h-4 w-4" />
            {testing ? "Tesztelés..." : "SQL kapcsolat tesztelése"}
          </button>

          <button
            type="submit"
            disabled={saving || loading}
            className="inline-flex items-center gap-2 rounded-full bg-slate-950 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-slate-800 disabled:cursor-default disabled:opacity-70"
          >
            <Save className="h-4 w-4" />
            {saving ? "Mentés..." : "Mentés"}
          </button>
        </div>
      </div>

      <Banner banner={banner} />

      <div className="grid gap-6 xl:grid-cols-2">
        <SectionCard
          icon={Database}
          title="SQL kapcsolat"
          description="A linuxos webszerver kapcsolatát itt tudod ugyanúgy felületről kezelni, mint a desktop appban."
        >
          <div className="grid gap-4 md:grid-cols-2">
            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">Szerver neve / IP cím *</span>
              <input
                value={formState.sql.server}
                onChange={(event) => updateSql("server", event.target.value)}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>

            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">Port *</span>
              <input
                type="number"
                value={formState.sql.port}
                onChange={(event) => updateSql("port", Number(event.target.value))}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>
          </div>

          <label className="grid gap-2 text-sm">
            <span className="font-medium text-slate-800">Adatbázis neve *</span>
            <input
              value={formState.sql.database}
              onChange={(event) => updateSql("database", event.target.value)}
              className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
            />
          </label>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">Felhasználónév *</span>
              <input
                value={formState.sql.userId}
                onChange={(event) => updateSql("userId", event.target.value)}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>

            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">Jelszó *</span>
              <input
                type="password"
                value={formState.sql.password}
                onChange={(event) => updateSql("password", event.target.value)}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>
          </div>

          <label className="inline-flex items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
            <input
              type="checkbox"
              checked={formState.sql.trustServerCertificate}
              onChange={(event) => updateSql("trustServerCertificate", event.target.checked)}
              className="h-4 w-4 rounded border-slate-300"
            />
            TrustServerCertificate használata
          </label>
        </SectionCard>

        <SectionCard
          icon={ImagePlus}
          title="Cég adatok"
          description="A logót most már feltöltéssel menti a rendszer, nem kézi fájlútvonallal."
        >
          <label className="grid gap-2 text-sm">
            <span className="font-medium text-slate-800">Cégnév *</span>
            <input
              value={formState.application.companyName}
              onChange={(event) => updateApplication("companyName", event.target.value)}
              className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
            />
          </label>

          <FileCard
            fileKey="companyLogo"
            state={fileState.companyLogo}
            onFileChange={handleFileChange}
            onClear={clearFile}
          />
        </SectionCard>

        <SectionCard
          icon={Mail}
          title="Email kapcsolat"
          description="A desktop app mezői maradnak, de a webes működéshez igazítva."
        >
          <div className="grid gap-4 md:grid-cols-2">
            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">SMTP szerver</span>
              <input
                value={formState.application.emailSmtp}
                onChange={(event) => updateApplication("emailSmtp", event.target.value)}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>

            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">SMTP port</span>
              <input
                type="number"
                value={formState.application.smtpPort}
                onChange={(event) => updateApplication("smtpPort", Number(event.target.value))}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>
          </div>

          <label className="grid gap-2 text-sm">
            <span className="font-medium text-slate-800">Email cím</span>
            <input
              type="email"
              value={formState.application.senderEmail}
              onChange={(event) => updateApplication("senderEmail", event.target.value)}
              className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
            />
          </label>

          <label className="grid gap-2 text-sm">
            <span className="font-medium text-slate-800">Email jelszó</span>
            <input
              type="password"
              value={formState.application.emailPassword}
              onChange={(event) => {
                setClearEmailPassword(false);
                updateApplication("emailPassword", event.target.value);
              }}
              className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
            />
          </label>

          <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm leading-6 text-slate-600">
            {emailPasswordHint}
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">Küldő neve</span>
              <input
                value={formState.application.senderName}
                onChange={(event) => updateApplication("senderName", event.target.value)}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>

            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">CC címzett</span>
              <input
                value={formState.application.ccAddress}
                onChange={(event) => updateApplication("ccAddress", event.target.value)}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>
          </div>

          <button
            type="button"
            onClick={() => {
              updateApplication("emailPassword", "");
              setClearEmailPassword((current) => !current);
            }}
            className="inline-flex items-center gap-2 self-start rounded-full border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-100"
          >
            <ShieldCheck className="h-4 w-4" />
            Mentéskor törölje az email jelszót
          </button>
        </SectionCard>

        <SectionCard
          icon={FileText}
          title="Dokumentum sablonok"
          description="A szerződés és az ÁSZF most már közvetlenül a webes felületen tölthető fel."
        >
          <FileCard
            fileKey="templateContract"
            state={fileState.templateContract}
            onFileChange={handleFileChange}
            onClear={clearFile}
          />
          <FileCard
            fileKey="aszfFile"
            state={fileState.aszfFile}
            onFileChange={handleFileChange}
            onClear={clearFile}
          />
        </SectionCard>

        <SectionCard
          icon={Mail}
          title="Email template-ek"
          description="A szerződéses és értékelő emailek tárgyait és HTML sablonjait itt tudod kezelni."
        >
          <label className="grid gap-2 text-sm">
            <span className="font-medium text-slate-800">Szerződés email tárgy</span>
            <input
              value={formState.application.emailSubject}
              onChange={(event) => updateApplication("emailSubject", event.target.value)}
              className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
            />
          </label>

          <FileCard
            fileKey="contractEmailTemplate"
            state={fileState.contractEmailTemplate}
            onFileChange={handleFileChange}
            onClear={clearFile}
          />

          <label className="grid gap-2 text-sm">
            <span className="font-medium text-slate-800">Értékelés email tárgy</span>
            <input
              value={formState.application.reviewEmailSubject}
              onChange={(event) => updateApplication("reviewEmailSubject", event.target.value)}
              className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
            />
          </label>

          <FileCard
            fileKey="reviewEmailTemplate"
            state={fileState.reviewEmailTemplate}
            onFileChange={handleFileChange}
            onClear={clearFile}
          />
        </SectionCard>

        <SectionCard
          icon={Star}
          title="Marketing és számla"
          description="A későbbi webes email és számlázás funkciókhoz minden szükséges mező elő van készítve."
        >
          <label className="grid gap-2 text-sm">
            <span className="font-medium text-slate-800">Google értékelés link</span>
            <input
              value={formState.application.googleReview}
              onChange={(event) => updateApplication("googleReview", event.target.value)}
              className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
            />
          </label>

          <FileCard
            fileKey="invoiceXml"
            state={fileState.invoiceXml}
            onFileChange={handleFileChange}
            onClear={clearFile}
          />
        </SectionCard>

        <SectionCard
          icon={ShieldCheck}
          title="Alapértelmezések"
          description="Ugyanazok az alapbeállítások, mint a desktop verzióban."
        >
          <div className="grid gap-4 md:grid-cols-2">
            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">Alapértelmezett bérlési napok</span>
              <input
                type="number"
                min={1}
                value={formState.application.defaultRentalDays}
                onChange={(event) => updateApplication("defaultRentalDays", Number(event.target.value))}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>

            <label className="grid gap-2 text-sm">
              <span className="font-medium text-slate-800">Értékelő email késleltetés (nap)</span>
              <input
                type="number"
                min={0}
                value={formState.application.reviewEmailDelayDays}
                onChange={(event) => updateApplication("reviewEmailDelayDays", Number(event.target.value))}
                className="h-11 rounded-2xl border border-slate-200 bg-slate-50 px-4"
              />
            </label>
          </div>
        </SectionCard>
      </div>

      {loading ? (
        <div className="rounded-2xl border border-slate-200 bg-white/85 px-4 py-3 text-sm text-slate-600">
          Beállítások betöltése...
        </div>
      ) : null}
    </form>
  );
}
