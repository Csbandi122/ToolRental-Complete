import { BrowserRouter, Routes, Route, NavLink } from "react-router-dom";
import { Bike, Table, Tags } from "lucide-react";
import DevicesPage from "@/pages/DevicesPage";
import DeviceManagementPage from "@/pages/DeviceManagementPage";
import DeviceTypesPage from "@/pages/DeviceTypesPage";

const navItems = [
  { to: "/", icon: Bike, label: "Eszközök" },
  { to: "/devices/manage", icon: Table, label: "Adatkezelő" },
  { to: "/device-types", icon: Tags, label: "Eszköztípusok" },
];

export default function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen flex flex-col">
        <header className="bg-green-700 text-white shadow-md">
          <div className="max-w-7xl mx-auto px-4 py-3 flex items-center gap-8">
            <div className="flex items-center gap-2">
              <Bike className="h-7 w-7" />
              <h1 className="text-xl font-bold tracking-tight">Kerékpár Bérlő Rendszer</h1>
            </div>
            <nav className="flex gap-1">
              {navItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === "/"}
                  className={({ isActive }) =>
                    `flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
                      isActive
                        ? "bg-white/20 text-white"
                        : "text-green-100 hover:bg-white/10 hover:text-white"
                    }`
                  }
                >
                  <item.icon className="h-4 w-4" />
                  {item.label}
                </NavLink>
              ))}
            </nav>
          </div>
        </header>

        <main className="flex-1 max-w-7xl w-full mx-auto px-4 py-6">
          <Routes>
            <Route path="/" element={<DevicesPage />} />
            <Route path="/devices/manage" element={<DeviceManagementPage />} />
            <Route path="/device-types" element={<DeviceTypesPage />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}
