import { ConnectionStatus } from "../features/health/connection-status";

export default function Dashboard() {
  return (
    <>
      <p className="eyebrow">Workspace overview</p>
      <h1>Dashboard</h1>
      <p className="text-slate-600">Your starting point for stock research and portfolio review.</p>
      <ConnectionStatus />
    </>
  );
}
