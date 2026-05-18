import { useState, useEffect, useCallback } from "react";
import { Plus, Pencil, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { fetchDeviceTypes, createDeviceType, updateDeviceType, deleteDeviceType } from "@/api/client";
import type { DeviceType } from "@/types";

export default function DeviceTypesPage() {
  const [types, setTypes] = useState<DeviceType[]>([]);
  const [selected, setSelected] = useState<number | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editType, setEditType] = useState<DeviceType | null>(null);
  const [typeName, setTypeName] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const loadTypes = useCallback(async () => {
    const data = await fetchDeviceTypes();
    setTypes(data);
  }, []);

  useEffect(() => { loadTypes(); }, [loadTypes]);

  const openAdd = () => {
    setEditType(null);
    setTypeName("");
    setError("");
    setDialogOpen(true);
  };

  const openEdit = () => {
    const t = types.find((t) => t.id === selected);
    if (t) {
      setEditType(t);
      setTypeName(t.typeName);
      setError("");
      setDialogOpen(true);
    }
  };

  const handleSave = async () => {
    if (!typeName.trim()) { setError("A típus neve kötelező."); return; }
    setSaving(true);
    setError("");
    try {
      if (editType) {
        await updateDeviceType(editType.id, { typeName: typeName.trim() });
      } else {
        await createDeviceType({ typeName: typeName.trim() });
      }
      setDialogOpen(false);
      loadTypes();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Hiba történt.");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (selected == null) return;
    if (!confirm("Biztosan törölni szeretné ezt a típust?")) return;
    try {
      await deleteDeviceType(selected);
      setSelected(null);
      loadTypes();
    } catch (err) {
      alert(err instanceof Error ? err.message : "Hiba a törlés során.");
    }
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold">Eszköztípusok</h2>
      </div>

      <div className="flex gap-2 mb-4">
        <Button onClick={openAdd} className="bg-green-700 hover:bg-green-800" size="sm">
          <Plus className="h-4 w-4 mr-1" /> Új típus
        </Button>
        <Button onClick={openEdit} disabled={selected == null} variant="outline" size="sm">
          <Pencil className="h-4 w-4 mr-1" /> Szerkesztés
        </Button>
        <Button onClick={handleDelete} disabled={selected == null} variant="outline" size="sm" className="text-red-600 hover:text-red-700">
          <Trash2 className="h-4 w-4 mr-1" /> Törlés
        </Button>
      </div>

      <div className="border rounded-lg overflow-hidden max-w-xl">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-16">ID</TableHead>
              <TableHead>Típus neve</TableHead>
              <TableHead className="text-right">Eszközök száma</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {types.length === 0 ? (
              <TableRow>
                <TableCell colSpan={3} className="text-center py-8 text-muted-foreground">Még nincsenek típusok</TableCell>
              </TableRow>
            ) : types.map((t) => (
              <TableRow
                key={t.id}
                className={`cursor-pointer ${selected === t.id ? "bg-blue-50" : ""}`}
                onClick={() => setSelected(t.id)}
                onDoubleClick={() => { setEditType(t); setTypeName(t.typeName); setError(""); setDialogOpen(true); }}
              >
                <TableCell className="font-mono text-sm">{t.id}</TableCell>
                <TableCell className="font-medium">{t.typeName}</TableCell>
                <TableCell className="text-right">{t.deviceCount}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>{editType ? "Típus szerkesztése" : "Új típus"}</DialogTitle>
          </DialogHeader>
          <div className="grid gap-3 py-2">
            <div className="grid gap-1.5">
              <Label htmlFor="typeName">Típus neve *</Label>
              <Input
                id="typeName"
                value={typeName}
                onChange={(e) => setTypeName(e.target.value)}
                maxLength={100}
                onKeyDown={(e) => { if (e.key === "Enter") handleSave(); }}
              />
            </div>
            {error && <p className="text-sm text-red-600 bg-red-50 rounded-md px-3 py-2">{error}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>Mégsem</Button>
            <Button onClick={handleSave} disabled={saving} className="bg-green-700 hover:bg-green-800">
              {saving ? "Mentés..." : "Mentés"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
