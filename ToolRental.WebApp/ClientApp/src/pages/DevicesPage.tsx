import { useState, useEffect, useCallback } from "react";
import { Plus, Search, Bike, Pencil } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { fetchDevices, fetchDeviceTypes } from "@/api/client";
import type { Device, DeviceType } from "@/types";
import DeviceDialog from "@/components/DeviceDialog";

export default function DevicesPage() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [deviceTypes, setDeviceTypes] = useState<DeviceType[]>([]);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState<string>("all");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editDevice, setEditDevice] = useState<Device | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const loadData = useCallback(async () => {
    const [d, t] = await Promise.all([fetchDevices(), fetchDeviceTypes()]);
    setDevices(d);
    setDeviceTypes(t);
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  const filtered = devices.filter((d) => {
    if (typeFilter !== "all" && d.deviceType !== Number(typeFilter)) return false;
    if (!debouncedSearch) return true;
    const s = debouncedSearch.toLowerCase();
    return (
      d.deviceName.toLowerCase().includes(s) ||
      d.serial?.toLowerCase().includes(s) ||
      d.deviceTypeName.toLowerCase().includes(s)
    );
  });

  const handleSaved = () => {
    setDialogOpen(false);
    setEditDevice(null);
    loadData();
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold">Eszközök</h2>
        <Button onClick={() => { setEditDevice(null); setDialogOpen(true); }} className="bg-green-700 hover:bg-green-800">
          <Plus className="h-4 w-4 mr-1" /> Új eszköz
        </Button>
      </div>

      <div className="flex gap-3 mb-6">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Keresés..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>
        <select
          value={typeFilter}
          onChange={(e) => setTypeFilter(e.target.value)}
          className="h-9 rounded-md border border-input bg-transparent px-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        >
          <option value="all">Összes típus</option>
          {deviceTypes.map((t) => (
            <option key={t.id} value={String(t.id)}>{t.typeName}</option>
          ))}
        </select>
      </div>

      {filtered.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground">
          <Bike className="h-12 w-12 mx-auto mb-3 opacity-30" />
          <p>Nincs találat</p>
        </div>
      ) : (
        <div className="grid grid-cols-[repeat(auto-fill,minmax(180px,1fr))] gap-4">
          {filtered.map((device) => (
            <div
              key={device.id}
              className="group border rounded-lg overflow-hidden bg-card hover:shadow-md transition-shadow cursor-pointer"
              onClick={() => { setEditDevice(device); setDialogOpen(true); }}
            >
              <div className="aspect-square bg-muted flex items-center justify-center overflow-hidden">
                {device.picture ? (
                  <img
                    src={device.picture}
                    alt={device.deviceName}
                    className="w-full h-full object-cover"
                  />
                ) : (
                  <Bike className="h-16 w-16 text-muted-foreground/30" />
                )}
              </div>
              <div className="p-3">
                <div className="flex items-start justify-between gap-1">
                  <p className="font-semibold text-sm leading-tight line-clamp-2">{device.deviceName}</p>
                  <Pencil className="h-3.5 w-3.5 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity shrink-0 mt-0.5" />
                </div>
                <p className="text-xs text-muted-foreground mt-0.5">{device.deviceTypeName}</p>
                <p className="text-sm font-bold text-green-700 mt-1">
                  {device.rentPrice.toLocaleString("hu-HU")} Ft/nap
                </p>
                <div className="mt-1.5">
                  {device.available ? (
                    <Badge variant="secondary" className="bg-green-100 text-green-800 text-xs">Elérhető</Badge>
                  ) : (
                    <Badge variant="secondary" className="bg-red-100 text-red-800 text-xs">Nem elérhető</Badge>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      <DeviceDialog
        open={dialogOpen}
        onOpenChange={(open) => { setDialogOpen(open); if (!open) setEditDevice(null); }}
        device={editDevice}
        deviceTypes={deviceTypes}
        onSaved={handleSaved}
      />
    </div>
  );
}
