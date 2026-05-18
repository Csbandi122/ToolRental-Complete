import { useState, useEffect, useCallback } from "react";
import { Plus, Pencil, Trash2, Search, ChevronUp, ChevronDown } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { fetchDevices, fetchDeviceTypes, deleteDevice } from "@/api/client";
import type { Device, DeviceType } from "@/types";
import DeviceDialog from "@/components/DeviceDialog";

type SortKey = "id" | "deviceName" | "deviceTypeName" | "serial" | "price" | "rentPrice" | "available" | "rentCount";

export default function DeviceManagementPage() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [deviceTypes, setDeviceTypes] = useState<DeviceType[]>([]);
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<number | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editDevice, setEditDevice] = useState<Device | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>("deviceName");
  const [sortAsc, setSortAsc] = useState(true);

  const loadData = useCallback(async () => {
    const [d, t] = await Promise.all([fetchDevices(), fetchDeviceTypes()]);
    setDevices(d);
    setDeviceTypes(t);
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  const filtered = devices.filter((d) => {
    if (!search) return true;
    const s = search.toLowerCase();
    return (
      d.deviceName.toLowerCase().includes(s) ||
      d.serial?.toLowerCase().includes(s) ||
      d.deviceTypeName.toLowerCase().includes(s) ||
      (d.notes?.toLowerCase().includes(s) ?? false)
    );
  });

  const sorted = [...filtered].sort((a, b) => {
    let cmp = 0;
    const av = a[sortKey], bv = b[sortKey];
    if (typeof av === "string" && typeof bv === "string") cmp = av.localeCompare(bv, "hu");
    else if (typeof av === "number" && typeof bv === "number") cmp = av - bv;
    else if (typeof av === "boolean" && typeof bv === "boolean") cmp = (av ? 1 : 0) - (bv ? 1 : 0);
    return sortAsc ? cmp : -cmp;
  });

  const toggleSort = (key: SortKey) => {
    if (sortKey === key) setSortAsc(!sortAsc);
    else { setSortKey(key); setSortAsc(true); }
  };

  const SortIcon = ({ col }: { col: SortKey }) => {
    if (sortKey !== col) return null;
    return sortAsc ? <ChevronUp className="h-3 w-3 inline ml-0.5" /> : <ChevronDown className="h-3 w-3 inline ml-0.5" />;
  };

  const handleEdit = () => {
    const dev = devices.find((d) => d.id === selected);
    if (dev) { setEditDevice(dev); setDialogOpen(true); }
  };

  const handleDelete = async () => {
    if (selected == null) return;
    if (!confirm("Biztosan törölni szeretné ezt az eszközt?")) return;
    try {
      await deleteDevice(selected);
      setSelected(null);
      loadData();
    } catch (err) {
      alert(err instanceof Error ? err.message : "Hiba a törlés során.");
    }
  };

  const handleSaved = () => {
    setDialogOpen(false);
    setEditDevice(null);
    loadData();
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold">Adatkezelő - Eszközök</h2>
      </div>

      <div className="flex gap-2 mb-4">
        <Button onClick={() => { setEditDevice(null); setDialogOpen(true); }} className="bg-green-700 hover:bg-green-800" size="sm">
          <Plus className="h-4 w-4 mr-1" /> Új eszköz
        </Button>
        <Button onClick={handleEdit} disabled={selected == null} variant="outline" size="sm">
          <Pencil className="h-4 w-4 mr-1" /> Szerkesztés
        </Button>
        <Button onClick={handleDelete} disabled={selected == null} variant="outline" size="sm" className="text-red-600 hover:text-red-700">
          <Trash2 className="h-4 w-4 mr-1" /> Törlés
        </Button>
        <div className="flex-1" />
        <div className="relative w-64">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input placeholder="Keresés..." value={search} onChange={(e) => setSearch(e.target.value)} className="pl-9" />
        </div>
      </div>

      <div className="border rounded-lg overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-16 cursor-pointer" onClick={() => toggleSort("id")}>ID <SortIcon col="id" /></TableHead>
              <TableHead className="cursor-pointer" onClick={() => toggleSort("deviceName")}>Eszköz neve <SortIcon col="deviceName" /></TableHead>
              <TableHead className="cursor-pointer" onClick={() => toggleSort("deviceTypeName")}>Típus <SortIcon col="deviceTypeName" /></TableHead>
              <TableHead className="cursor-pointer" onClick={() => toggleSort("serial")}>Sorozatszám <SortIcon col="serial" /></TableHead>
              <TableHead className="text-right cursor-pointer" onClick={() => toggleSort("price")}>Vételár <SortIcon col="price" /></TableHead>
              <TableHead className="text-right cursor-pointer" onClick={() => toggleSort("rentPrice")}>Bérlési ár <SortIcon col="rentPrice" /></TableHead>
              <TableHead className="text-center cursor-pointer" onClick={() => toggleSort("available")}>Elérhető <SortIcon col="available" /></TableHead>
              <TableHead className="text-right cursor-pointer" onClick={() => toggleSort("rentCount")}>Bérlések <SortIcon col="rentCount" /></TableHead>
              <TableHead>Megjegyzés</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sorted.length === 0 ? (
              <TableRow>
                <TableCell colSpan={9} className="text-center py-8 text-muted-foreground">Nincs találat</TableCell>
              </TableRow>
            ) : sorted.map((d) => (
              <TableRow
                key={d.id}
                className={`cursor-pointer ${selected === d.id ? "bg-blue-50" : ""}`}
                onClick={() => setSelected(d.id)}
                onDoubleClick={() => { setEditDevice(d); setDialogOpen(true); }}
              >
                <TableCell className="font-mono text-sm">{d.id}</TableCell>
                <TableCell className="font-medium">{d.deviceName}</TableCell>
                <TableCell>{d.deviceTypeName}</TableCell>
                <TableCell className="text-muted-foreground">{d.serial}</TableCell>
                <TableCell className="text-right">{d.price > 0 ? `${d.price.toLocaleString("hu-HU")} Ft` : "-"}</TableCell>
                <TableCell className="text-right font-medium">{d.rentPrice.toLocaleString("hu-HU")} Ft</TableCell>
                <TableCell className="text-center">{d.available ? "✓" : "✗"}</TableCell>
                <TableCell className="text-right">{d.rentCount}</TableCell>
                <TableCell className="max-w-[200px] truncate text-muted-foreground text-sm">{d.notes}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

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
