export default function HomePage() {
  return (
    <section className="rounded-[2.25rem] border border-slate-200/80 bg-white/85 p-8 shadow-[0_28px_60px_rgba(15,23,42,0.07)] backdrop-blur">
      <div className="max-w-3xl">
        <p className="text-xs font-semibold uppercase tracking-[0.22em] text-sky-700">Új kezdőoldal</p>
        <h2 className="mt-3 text-3xl font-semibold tracking-tight text-slate-950">Itt lesz majd a főoldal.</h2>
        <p className="mt-4 text-base leading-8 text-slate-600">
          Az eszközkezelő már külön főmenüpontként működik, a beállítások pedig webes működésre lettek
          átalakítva. Innen tudjuk majd tovább bővíteni a rendszert a következő modulokkal.
        </p>
      </div>
    </section>
  );
}
