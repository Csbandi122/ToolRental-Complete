import { useState, useEffect, useRef } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { createDevice, updateDevice } from "@/api/client";
import type { Device, DeviceType } from "@/types";
import { Bike, Upload } from "lucide-react";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  device: Device | null;
  deviceTypes: DeviceType[];
  onSaved: () => void;
}

export default function DeviceDialog({ open, onOpenChange, device, deviceTypes, onSaved }: Props) {
  const [name, setName] = useState("");
  const [typeId, setTypeId] = useState("");
  const [serial, setSerial] = useState("");
  const [price, setPrice] = useState("");
  const [rentPrice, setRentPrice] = useState("");
  const [available, setAvailable] = useState(true);
  const [notes, setNotes] = useState("");
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const fileRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (open) {
      if (device) {
        setName(device.deviceName);
        setTypeId(String(device.deviceType));
        setSerial(device.serial || "");
        setPrice(device.price > 0 ? String(device.price) : "");
        setRentPrice(String(device.rentPrice));
        setAvailable(device.available);
        setNotes(device.notes || "");
        setPreviewUrl(device.picture);
      } else {
        setName("");
        setTypeId("");
        setSerial("");
        setPrice("");
        setRentPrice("");
        setAvailable(true);
        setNotes("");
        setPreviewUrl(null);
      }
      setImageFile(null);
      setError("");
    }
  }, [open, device]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setImageFile(file);
      setPreviewUrl(URL.createObjectURL(file));
    }
  };

  const handleSubmit = async () => {
    if (!name.trim()) { setError("Az eszköz neve kötelező."); return; }
    if (!typeId) { setError("Az eszköztípus kiválasztása kötelező."); return; }
    const rp = Number(rentPrice);
    if (isNaN(rp) || rp < 0) { setError("A bérlési ár kötelező és nem lehet negatív."); return; }
    const p = price ? Number(price) : 0;
    if (isNaN(p) || p < 0) { setError("A vételár nem lehet negatív."); return; }

    setSaving(true);
    setError("");

    const formData = new FormData();
    formData.append("deviceName", name.trim());
    formData.append("deviceType", typeId);
    formData.append("serial", serial.trim());
    formData.append("price", String(p));
    formData.append("rentPrice", String(rp));
    formData.append("available", String(available));
    formData.append("notes", notes.trim());
    if (imageFile) formData.append("image", imageFile);

    try {
      if (device) {
        await updateDevice(device.id, formData);
      } else {
        await createDevice(formData);
      }
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Hiba történt a mentés során.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{device ? "Eszköz szerkesztése" : "Új eszköz"}</DialogTitle>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          <div className="grid gap-1.5">
            <Label htmlFor="deviceName">Eszköz neve *</Label>
            <Input id="deviceName" value={name} onChange={(e) => setName(e.target.value)} maxLength={200} />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="deviceType">Eszköz típusa *</Label>
            <select
              id="deviceType"
              value={typeId}
              onChange={(e) => setTypeId(e.target.value)}
              className="h-9 rounded-md border border-input bg-transparent px-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            >
              <option value="">Válasszon típust...</option>
              {deviceTypes.map((t) => (
                <option key={t.id} value={String(t.id)}>{t.typeName}</option>
              ))}
            </select>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="serial">Sorozatszám</Label>
            <Input id="serial" value={serial} onChange={(e) => setSerial(e.target.value)} maxLength={100} />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="price">Vételár (Ft)</Label>
              <Input id="price" type="number" min="0" value={price} onChange={(e) => setPrice(e.target.value)} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="rentPrice">Bérlési ár (Ft/nap) *</Label>
              <Input id="rentPrice" type="number" min="0" value={rentPrice} onChange={(e) => setRentPrice(e.target.value)} />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Checkbox id="available" checked={available} onCheckedChange={(c) => setAvailable(c === true)} />
            <Label htmlFor="available" className="cursor-pointer">Elérhető bérlésre</Label>
          </div>

          <div className="grid gap-1.5">
            <Label>Eszköz képe</Label>
            <div className="flex items-center gap-3">
              <div className="h-24 w-24 rounded-md border bg-muted flex items-center justify-center overflow-hidden shrink-0">
                {previewUrl ? (
                  <img src={previewUrl} alt="Előnézet" className="h-full w-full object-cover" />
                ) : (
                  <Bike className="h-8 w-8 text-muted-foreground/30" />
                )}
              </div>
              <div>
                <Button type="button" variant="outline" size="sm" onClick={() => fileRef.current?.click()}>
                  <Upload className="h-4 w-4 mr-1" /> Tallózás...
                </Button>
                <input
                  ref={fileRef}
                  type="file"
                  accept=".jpg,.jpeg,.png,.bmp,.gif,.webp"
                  className="hidden"
                  onChange={handleFileChange}
                />
                {imageFile && <p className="text-xs text-muted-foreground mt-1">{imageFile.name}</p>}
              </div>
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="notes">Megjegyzések</Label>
            <Textarea id="notes" value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={1000} rows={3} />
          </div>

          {error && <p className="text-sm text-red-600 bg-red-50 rounded-md px-3 py-2">{error}</p>}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Mégsem</Button>
          <Button onClick={handleSubmit} disabled={saving} className="bg-green-700 hover:bg-green-800">
            {saving ? "Mentés..." : "Mentés"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
