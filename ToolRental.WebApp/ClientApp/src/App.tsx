import { BrowserRouter, Routes, Route, NavLink, Navigate, useLocation, Outlet } from "react-router-dom";
import { House, Settings2, Wrench, Bike, Table, Tags } from "lucide-react";
import HomePage from "@/pages/HomePage";
import DevicesPage from "@/pages/DevicesPage";
import DeviceManagementPage from "@/pages/DeviceManagementPage";
import DeviceTypesPage from "@/pages/DeviceTypesPage";
import SettingsPage from "@/pages/SettingsPage";

const topLevelNavItems = [
  { to: "/", icon: House, label: "Főoldal" },
  { to: "/devices", icon: Wrench, label: "Eszközkezelő" },
  { to: "/settings", icon: Settings2, label: "Beállítások" },
];

const deviceNavItems = [
  { to: "/devices", icon: Bike, label: "Kártyás nézet" },
  { to: "/devices/manage", icon: Table, label: "Adatkezelő" },
  { to: "/devices/types", icon: Tags, label: "Eszköztípusok" },
];

function DeviceModuleLayout() {
  return (
    <div className="space-y-6">
      <div className="rounded-[2rem] border border-slate-200/80 bg-white/80 p-5 shadow-[0_20px_45px_rgba(15,23,42,0.06)] backdrop-blur">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.22em] text-sky-700">Elkészült modul</p>
            <h2 className="mt-2 text-2xl font-semibold text-slate-900">Eszközkezelő</h2>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-600">
              Az eddig elkészült részek itt maradnak együtt, csak most már külön főmenüpontként.
            </p>
          </div>

          <nav className="flex flex-wrap gap-2">
            {deviceNavItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === "/devices"}
                className={({ isActive }) =>
                  `inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                    isActive
                      ? "bg-slate-900 text-white"
                      : "bg-slate-100 text-slate-700 hover:bg-slate-200"
                  }`
                }
              >
                <item.icon className="h-4 w-4" />
                {item.label}
              </NavLink>
            ))}
          </nav>
        </div>
      </div>

      <Outlet />
    </div>
  );
}

function Shell() {
  const location = useLocation();
  const isDeviceRoute = location.pathname.startsWith("/devices");
  const isSettingsRoute = location.pathname.startsWith("/settings");

  const pageTitle = isDeviceRoute
    ? "Eszközkezelő"
    : isSettingsRoute
      ? "Beállítások"
      : "Főoldal";

  const pageSubtitle = isDeviceRoute
    ? "A jelenlegi webapp mostantól egy nagyobb admin felület első modulja."
    : isSettingsRoute
      ? "Desktop logika, de webszerű beállításkezeléssel és szerveroldali fájlfeltöltéssel."
      : "Innen fog később elindulni a teljes rendszer főoldala.";

  return (
    <div className="min-h-screen bg-[radial-gradient(circle_at_top_right,_rgba(255,207,122,0.18),_transparent_28%),linear-gradient(180deg,_#f5f7fb_0%,_#eef3f8_100%)] text-slate-900">
      <div className="mx-auto flex min-h-screen w-full max-w-[1600px]">
        <aside className="hidden w-[280px] shrink-0 border-r border-slate-200/80 bg-slate-950 px-5 py-7 text-white lg:flex lg:flex-col">
          <div className="flex items-center gap-4">
            <div className="grid h-12 w-12 place-items-center rounded-2xl bg-gradient-to-br from-amber-300 to-orange-500 text-sm font-black tracking-[0.24em] text-slate-950">
              TR
            </div>
            <div>
              <div className="text-base font-semibold">ToolRental</div>
              <div className="text-sm text-slate-400">Webes admin</div>
            </div>
          </div>

          <nav className="mt-10 space-y-2">
            {topLevelNavItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === "/"}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-2xl px-4 py-3 text-sm font-medium transition-colors ${
                    isActive
                      ? "bg-white/10 text-white"
                      : "text-slate-400 hover:bg-white/5 hover:text-white"
                  }`
                }
              >
                <item.icon className="h-4 w-4" />
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="mt-auto rounded-[1.75rem] border border-white/10 bg-white/5 p-5">
            <p className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Jelenlegi állapot</p>
            <p className="mt-3 text-sm leading-6 text-slate-300">
              A főoldal most szándékosan üres, az első kész modul az eszközkezelő, és most elkészült mellé a beállításkezelés is.
            </p>
          </div>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="border-b border-slate-200/80 bg-white/75 px-5 py-5 backdrop-blur md:px-8">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
              <div className="min-w-0">
                <p className="text-xs font-semibold uppercase tracking-[0.22em] text-sky-700">ToolRental admin shell</p>
                <h1 className="mt-2 text-3xl font-semibold tracking-tight text-slate-950">{pageTitle}</h1>
                <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-600">{pageSubtitle}</p>
              </div>

              <div className="flex flex-wrap gap-2 lg:hidden">
                {topLevelNavItems.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.to === "/"}
                    className={({ isActive }) =>
                      `inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                        isActive
                          ? "bg-slate-900 text-white"
                          : "bg-white text-slate-700 shadow-sm ring-1 ring-slate-200"
                      }`
                    }
                  >
                    <item.icon className="h-4 w-4" />
                    {item.label}
                  </NavLink>
                ))}
              </div>
            </div>
          </header>

          <main className="flex-1 px-5 py-6 md:px-8">
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/devices" element={<DeviceModuleLayout />}>
                <Route index element={<DevicesPage />} />
                <Route path="manage" element={<DeviceManagementPage />} />
                <Route path="types" element={<DeviceTypesPage />} />
              </Route>
              <Route path="/settings" element={<SettingsPage />} />
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </main>
        </div>
      </div>
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <Shell />
    </BrowserRouter>
  );
}
